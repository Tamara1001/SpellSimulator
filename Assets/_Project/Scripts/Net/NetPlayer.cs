using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Avatar de red de un jugador (F1). Cada cliente tiene el suyo: el DUENO lo mueve copiando su cuerpo local (raiz, cabeza y manos) y
    /// los demas lo ven interpolado. En el dueno el avatar esta oculto (se ve con su propio rig). Los demas se registran en
    /// <see cref="PlayerRegistry"/> como jugadores remotos, asi las reglas de mundo (presupuesto, cristal, portales) los cuentan.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetPlayer : NetworkBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform handMain;
        [SerializeField] private Transform handSecondary;
        [SerializeField] private TextMesh nameLabel;
        [SerializeField] private Renderer[] bodyRenderers;

        private readonly NetworkVariable<Vector3> headPos = new NetworkVariable<Vector3>(new Vector3(0f, 0.7f, 0f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Quaternion> headRot = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector3> mainHandPos = new NetworkVariable<Vector3>(new Vector3(0.3f, 0.2f, 0.3f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector3> secondaryHandPos = new NetworkVariable<Vector3>(new Vector3(-0.3f, 0.2f, 0.3f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private RemotePlayerContext remoteContext;
        private Transform driver;           // a quien copia el dueno (null = jugador local del registro)
        private Camera labelCamera;
        private static MaterialPropertyBlock block;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private static readonly Vector3 StandingHead = new Vector3(0f, 0.7f, 0f);

        public Transform Head => head;
        public string DisplayName => playerName.Value.ToString();

        /// <summary>Solo pruebas: hace que el dueno copie a este transform en vez de al jugador local.</summary>
        public void SetDriver(Transform t) => driver = t;

        public override void OnNetworkSpawn()
        {
            Tint();
            if (IsOwner)
            {
                PlayerRegistry.LocalOwnerId = (int)OwnerClientId;
                playerName.Value = new FixedString32Bytes(NetSession.PlayerName);
                SetVisible(false);
            }
            else
            {
                remoteContext = new RemotePlayerContext(this);
                PlayerRegistry.Register(remoteContext);
                SetVisible(true);
                playerName.OnValueChanged += OnNameChanged;
                ShowName(playerName.Value.ToString());
            }
        }

        public override void OnNetworkDespawn()
        {
            if (remoteContext != null) { PlayerRegistry.Unregister(remoteContext); remoteContext = null; }
            playerName.OnValueChanged -= OnNameChanged;
            if (IsOwner) PlayerRegistry.LocalOwnerId = 0;
        }

        private void OnNameChanged(FixedString32Bytes previous, FixedString32Bytes now) => ShowName(now.ToString());

        private void ShowName(string text) { if (nameLabel != null) nameLabel.text = text; }

        private void SetVisible(bool on)
        {
            if (bodyRenderers != null) for (int i = 0; i < bodyRenderers.Length; i++) if (bodyRenderers[i] != null) bodyRenderers[i].enabled = on;
            if (nameLabel != null) nameLabel.gameObject.SetActive(on);
        }

        private void Tint()
        {
            if (bodyRenderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            Color c = Color.HSVToRGB(Mathf.Repeat((int)OwnerClientId * 0.173f + 0.05f, 1f), 0.55f, 0.95f);
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null) continue;
                bodyRenderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColor, c);
                bodyRenderers[i].SetPropertyBlock(block);
            }
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsOwner) DriveFromSource(); else FollowNetwork();
        }

        private void DriveFromSource()
        {
            Transform root, eyes, main = null, second = null;
            if (driver != null) { root = driver; eyes = driver; }
            else
            {
                IPlayerContext local = PlayerRegistry.Local;
                if (local == null || local.Root == null) return;
                root = local.Root; eyes = local.Head; main = local.MainHand; second = local.SecondaryHand;
            }
            // el avatar se pega al cuerpo local; la raiz de red lleva posicion y giro en Y (ClientNetworkTransform)
            transform.SetPositionAndRotation(root.position, Quaternion.Euler(0f, root.eulerAngles.y, 0f));
            if (driver == null && UI.MainMenu.IsActive)
            {
                // en el menu la camara orbita la torre: la cabeza queda en su sitio y las manos al costado
                headPos.Value = StandingHead;
                headRot.Value = Quaternion.identity;
                mainHandPos.Value = new Vector3(0.3f, 0.2f, 0.3f);
                secondaryHandPos.Value = new Vector3(-0.3f, 0.2f, 0.3f);
                return;
            }
            headPos.Value = transform.InverseTransformPoint(eyes.position);
            headRot.Value = Quaternion.Inverse(transform.rotation) * eyes.rotation;
            if (main != null) mainHandPos.Value = transform.InverseTransformPoint(main.position);
            if (second != null) secondaryHandPos.Value = transform.InverseTransformPoint(second.position);
        }

        private void FollowNetwork()
        {
            float k = 1f - Mathf.Exp(-18f * Time.deltaTime);
            if (head != null) { head.localPosition = Vector3.Lerp(head.localPosition, headPos.Value, k); head.localRotation = Quaternion.Slerp(head.localRotation, headRot.Value, k); }
            if (handMain != null) handMain.localPosition = Vector3.Lerp(handMain.localPosition, mainHandPos.Value, k);
            if (handSecondary != null) handSecondary.localPosition = Vector3.Lerp(handSecondary.localPosition, secondaryHandPos.Value, k);
            if (nameLabel != null)
            {
                if (labelCamera == null) { var l = PlayerRegistry.Local; labelCamera = l != null && l.Head != null ? l.Head.GetComponent<Camera>() : Camera.main; }
                if (labelCamera != null)
                {
                    Transform t = nameLabel.transform;
                    t.position = (head != null ? head.position : transform.position) + Vector3.up * 0.38f;
                    t.rotation = Quaternion.LookRotation(t.position - labelCamera.transform.position);
                }
            }
        }
    }

    /// <summary>Contexto de un jugador remoto (otro cliente) para <see cref="PlayerRegistry"/>.</summary>
    public sealed class RemotePlayerContext : IPlayerContext
    {
        private readonly NetPlayer player;
        public RemotePlayerContext(NetPlayer p) { player = p; }
        public int OwnerId => player != null ? (int)player.OwnerClientId : -1;
        public bool IsLocal => false;
        public Transform Root => player != null ? player.transform : null;
        public Transform Head => player != null ? player.Head : null;
        public Transform MainHand => null;
        public Transform SecondaryHand => null;
        public bool IsAlive => player != null && player.IsSpawned;
    }
}
