using System;
using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Ubica al jugador local: en la terraza de la torre al empezar el tutorial, en el nexo si lo saltea y de vuelta en el nexo
    /// cuando reaparece (hoy: si se cae del mundo; cualquier muerte futura llama a <see cref="Respawn"/>).
    /// Las posiciones salen de <see cref="SpawnRules"/> (logica pura).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerSpawn : MonoBehaviour
    {
        /// <summary>Clave de PlayerPrefs: 1 si el jugador ya salteo o termino el tutorial alguna vez.</summary>
        public const string TutorialSeenKey = "tutorial.seen";

        // Revision barata de la caida (cada 0,5 s, sin allocations).
        private const float CheckPeriod = 0.5f;

        /// <summary>El jugador reaparecio en el nexo.</summary>
        public static event Action<PlayerSpawn> Respawned;

        private Rigidbody body;
        private float checkTimer;

        public static bool TutorialSeen
        {
            get => PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1;
            set => PlayerPrefs.SetInt(TutorialSeenKey, value ? 1 : 0);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            // La caida del jugador la maneja este componente (vuelve al nexo): un Respawnable lo devolveria a su pose inicial.
            if (TryGetComponent(out World.Respawnable legacy)) legacy.enabled = false;
        }

        private void FixedUpdate()
        {
            checkTimer += Time.fixedDeltaTime;
            if (checkTimer < CheckPeriod) return;
            checkTimer = 0f;
            if (SpawnRules.FellOut(body.position.y)) Respawn();
        }

        /// <summary>Empieza la partida: en la terraza (tutorial) o en el nexo (salteado).</summary>
        public void Begin(bool skipTutorial)
        {
            Vector3 position = SpawnRules.Start(skipTutorial, out float yaw);
            Place(position, yaw);
            // Nadie empieza con algo en la mano (en VR un agarre accidental antes del tutorial dejaba un lapiz equipado).
            if (TryGetComponent(out PlayerInteractor interactor)) interactor.ClearHands();
            if (skipTutorial) TutorialSeen = true;
            World.TowerTutorial tutorial = World.TowerTutorial.Instance;
            if (tutorial != null)
            {
                if (skipTutorial) tutorial.Skip();
                else tutorial.Begin();
            }
            Debug.Log(skipTutorial ? "[Spawn] tutorial salteado: el jugador aparece en el nexo" : "[Spawn] el jugador empieza el tutorial en la terraza", this);
        }

        /// <summary>Reaparicion: vuelve al nexo.</summary>
        public void Respawn()
        {
            Place(SpawnRules.Nexus, SpawnRules.NexusYaw);
            Debug.Log("[Spawn] el jugador reaparece en el nexo", this);
            Respawned?.Invoke(this);
        }

        private void Place(Vector3 position, float yaw)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            if (body == null) body = GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }
    }
}
