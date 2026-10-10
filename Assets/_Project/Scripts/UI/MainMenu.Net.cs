using UnityEngine;
using UnityEngine.UIElements;
using SpellSimulator.Net;

namespace SpellSimulator.UI
{
    /// <summary>
    /// Parte multijugador del menu principal (F1 de MULTIPLAYER_PLAN): hoja con el nombre, "Crear sala" (muestra el codigo para compartir)
    /// y "Unirse" con un codigo. Al conectar entra directo al juego. Si ya hay sala, la hoja ofrece salir de ella.
    /// </summary>
    public partial class MainMenu
    {
        private VisualElement netPanel;
        private Button multiplayer, netHost, netJoin, netLeave, netBack;
        private TextField netName, netCode;
        private Label netStatus, multiplayerSub;
        private float netRevealAt = -1f, netHideAt = -1f;
        private bool netBusy;
        private bool netStarted; // el usuario apreto Crear sala / Unirse: al conectar se entra solo al juego

        private void BindNet()
        {
            netPanel = menu.Q("net-panel");
            multiplayer = menu.Q<Button>("multiplayer");
            multiplayerSub = menu.Q<Label>("multiplayer-sub");
            netHost = menu.Q<Button>("net-host");
            netJoin = menu.Q<Button>("net-join");
            netLeave = menu.Q<Button>("net-leave");
            netBack = menu.Q<Button>("net-back");
            netName = menu.Q<TextField>("net-name");
            netCode = menu.Q<TextField>("net-code");
            netStatus = menu.Q<Label>("net-status");

            multiplayer.clicked += OpenNet;
            netBack.clicked += CloseNet;
            netHost.clicked += HostClicked;
            netJoin.clicked += JoinClicked;
            netLeave.clicked += LeaveClicked;
            menu.Q("net-backdrop").RegisterCallback<ClickEvent>(_ => CloseNet());
            netCode.RegisterValueChangedCallback(e =>
            {
                string up = e.newValue.ToUpperInvariant();
                if (up != e.newValue) netCode.SetValueWithoutNotify(up);
            });
            foreach (var b in new[] { netHost, netJoin, netLeave, netBack })
                b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledSelf) b.Focus(); });
        }

        public void OpenNet()
        {
            if (state != State.Menu) return;
            state = State.Net;
            netHideAt = -1f;
            netName.SetValueWithoutNotify(NetSession.PlayerName);
            RefreshNetView();
            if (NetSession.Exists) NetSession.Instance.Changed += RefreshNetView;
            netPanel.RemoveFromClassList("mm-credits--hidden");
            netPanel.AddToClassList("mm-credits--fading");
            netRevealAt = Time.unscaledTime + 0.03f;
        }

        public void CloseNet()
        {
            if (state != State.Net) return;
            state = State.Menu;
            if (NetSession.Exists) NetSession.Instance.Changed -= RefreshNetView;
            netPanel.AddToClassList("mm-credits--fading");
            netHideAt = Time.unscaledTime + 0.25f;
            multiplayer.Focus();
        }

        private void HideNetNow()
        {
            if (netPanel == null) return;
            netRevealAt = netHideAt = -1f;
            if (NetSession.Exists) NetSession.Instance.Changed -= RefreshNetView;
            netPanel.AddToClassList("mm-credits--hidden");
            netPanel.RemoveFromClassList("mm-credits--fading");
        }

        /// <summary>Anima la hoja (se llama desde Update del menu).</summary>
        private void TickNet(float now)
        {
            if (netRevealAt > 0f && now >= netRevealAt) { netRevealAt = -1f; netPanel.RemoveFromClassList("mm-credits--fading"); }
            if (netHideAt > 0f && now >= netHideAt) { netHideAt = -1f; if (state != State.Net) HideNetNow(); }
        }

        private void RefreshNetView()
        {
            NetSession s = NetSession.Exists ? NetSession.Instance : null;
            bool connected = s != null && s.IsConnected;
            bool connecting = s != null && s.State == NetState.Connecting;
            netBusy = connecting;
            if (connected && netStarted && state == State.Net) { EnterGameFromNet(); return; }

            netHost.style.display = connected ? DisplayStyle.None : DisplayStyle.Flex;
            netJoin.style.display = connected ? DisplayStyle.None : DisplayStyle.Flex;
            netCode.style.display = connected ? DisplayStyle.None : DisplayStyle.Flex;
            netName.SetEnabled(!connected && !connecting);
            netHost.SetEnabled(!connecting);
            netJoin.SetEnabled(!connecting);
            netLeave.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;

            if (connected)
            {
                netStatus.text = (s.IsHost ? "Sos el anfitrión. Código de la sala: " : "Estás en la sala ") + s.JoinCode;
                multiplayerSub.text = "Sala " + s.JoinCode + " · tocá para salir";
            }
            else if (connecting) { netStatus.text = "Conectando…"; }
            else if (s != null && s.State == NetState.Failed)
            {
                netStatus.text = "No se pudo conectar: " + s.LastError;
                multiplayerSub.text = "Sin conexión: " + s.LastError;
            }
            else
            {
                netStatus.text = "";
                multiplayerSub.text = "Crear una sala o unirse con un código";
            }
        }

        private void SaveName() => NetSession.PlayerName = netName.value;

        private async void HostClicked()
        {
            if (netBusy) return;
            SaveName();
            NetSession s = NetSession.Instance;
            s.Changed -= RefreshNetView; s.Changed += RefreshNetView;
            netStarted = true;
            bool ok = await s.HostAsync();
            if (!ok) netStarted = false;
            else if (state == State.Net) EnterGameFromNet();
        }

        private async void JoinClicked()
        {
            if (netBusy) return;
            if (string.IsNullOrWhiteSpace(netCode.value)) { netStatus.text = "Escribí el código de la sala."; return; }
            SaveName();
            NetSession s = NetSession.Instance;
            s.Changed -= RefreshNetView; s.Changed += RefreshNetView;
            netStarted = true;
            bool ok = await s.JoinAsync(netCode.value);
            if (!ok) netStarted = false;
            else if (state == State.Net) EnterGameFromNet();
        }

        private void LeaveClicked()
        {
            if (NetSession.Exists) NetSession.Instance.Leave();
        }

        /// <summary>Se perdio la conexion (el anfitrion se fue o cerro la sala): vuelve al menu y muestra el aviso en la hoja.</summary>
        private void OnNetLost(string reason)
        {
            netStarted = false;
            if (state == State.Playing) ShowMenu();
            if (state == State.Menu) OpenNet();
        }

        private void EnterGameFromNet()
        {
            if (state != State.Net) return;
            netStarted = false;
            if (NetSession.Exists) NetSession.Instance.Changed -= RefreshNetView;
            HideNetNow();
            state = State.Menu;
            EnterSandbox();
        }
    }
}
