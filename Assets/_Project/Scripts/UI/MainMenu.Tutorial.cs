using UnityEngine;
using UnityEngine.UIElements;
using SpellSimulator.Player;

namespace SpellSimulator.UI
{
    /// <summary>
    /// Pregunta «¿Saltear el tutorial?» al entrar a Modo Sandbox por primera vez en la sesion (Docs/MAPA_FLUJO_PLAN.md, D23).
    /// «Hacer el tutorial» deja al jugador en la terraza de la torre; «Saltear» lo deja en el nexo. Las demas entradas
    /// (harness, red, VR) siguen llamando a <see cref="EnterSandbox"/> y no preguntan.
    /// </summary>
    public partial class MainMenu
    {
        private VisualElement tutorialPanel;
        private Button tutorialPlay, tutorialSkip;
        private bool tutorialAsked;

        private void OnSandboxClicked()
        {
            if (tutorialAsked) EnterSandbox();
            else OpenTutorialPrompt();
        }

        private void OpenTutorialPrompt()
        {
            if (state != State.Menu) return;
            if (tutorialPanel == null) BuildTutorialPrompt();
            state = State.Tutorial;
            tutorialPanel.style.display = DisplayStyle.Flex;
            // Quien ya lo vio (lo salteo o lo termino) tiene «Saltear» a mano; quien no, el tutorial.
            (PlayerSpawn.TutorialSeen ? tutorialSkip : tutorialPlay).Focus();
        }

        private void CloseTutorialPrompt()
        {
            if (state != State.Tutorial) return;
            tutorialPanel.style.display = DisplayStyle.None;
            state = State.Menu;
            sandbox.Focus();
        }

        /// <summary>Elige hacer (false) o saltear (true) el tutorial y entra a Sandbox. Publico para el harness.</summary>
        public void ChooseTutorial(bool skip)
        {
            if (state == State.Tutorial)
            {
                tutorialPanel.style.display = DisplayStyle.None;
                state = State.Menu;
            }
            if (state != State.Menu) return;
            tutorialAsked = true;
            if (skip) PlayerSpawn.TutorialSeen = true;
            PlayerSpawn spawn = FindFirstObjectByType<PlayerSpawn>();
            // Si ubicar al jugador falla (escena desactualizada, objeto faltante), igual se le entrega el control:
            // sin esto la entrada quedaba cerrada (no se podia mover ni apretar E).
            try { if (spawn != null) spawn.Begin(skip); }
            catch (System.Exception e) { Debug.LogException(e, this); }
            finally { EnterSandbox(); }
        }

        private void HandleTutorialKeys(bool back, bool submit, int move)
        {
            if (back) { CloseTutorialPrompt(); return; }
            bool onSkip = menu.focusController?.focusedElement == tutorialSkip;
            if (move != 0) (onSkip ? tutorialPlay : tutorialSkip).Focus();
            else if (submit) ChooseTutorial(onSkip);
        }

        private void BuildTutorialPrompt()
        {
            tutorialPanel = new VisualElement { name = "tutorial-panel" };
            tutorialPanel.style.position = Position.Absolute;
            tutorialPanel.style.left = 0f; tutorialPanel.style.right = 0f; tutorialPanel.style.top = 0f; tutorialPanel.style.bottom = 0f;
            tutorialPanel.style.alignItems = Align.Center;
            tutorialPanel.style.justifyContent = Justify.Center;
            tutorialPanel.style.backgroundColor = new Color(0.05f, 0.04f, 0.08f, 0.72f);
            tutorialPanel.style.display = DisplayStyle.None;

            var box = new VisualElement();
            box.style.paddingLeft = 36f; box.style.paddingRight = 36f; box.style.paddingTop = 28f; box.style.paddingBottom = 28f;
            box.style.backgroundColor = new Color(0.13f, 0.1f, 0.17f, 0.96f);
            box.style.borderTopLeftRadius = 10f; box.style.borderTopRightRadius = 10f;
            box.style.borderBottomLeftRadius = 10f; box.style.borderBottomRightRadius = 10f;
            box.style.alignItems = Align.Stretch;
            box.style.minWidth = 420f;

            var title = new Label("¿Saltear el tutorial?");
            title.style.fontSize = 30f;
            title.style.color = new Color(0.96f, 0.9f, 0.78f);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 8f;
            var hint = new Label("El tutorial empieza en lo alto de la torre.\nSi lo salteás, aparecés en el nexo.");
            hint.style.fontSize = 17f;
            hint.style.color = new Color(0.8f, 0.76f, 0.7f);
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginBottom = 20f;

            tutorialPlay = new Button(() => ChooseTutorial(false)) { name = "tutorial-play", text = "Hacer el tutorial" };
            tutorialPlay.AddToClassList("mm-button");
            tutorialPlay.AddToClassList("mm-button--primary");
            tutorialSkip = new Button(() => ChooseTutorial(true)) { name = "tutorial-skip", text = "Saltear (ir al nexo)" };
            tutorialSkip.AddToClassList("mm-button");
            // Los botones nacen en runtime fuera del arbol con estilo de .mm-label: el texto salia negro (Button por defecto).
            foreach (Button tb in new[] { tutorialPlay, tutorialSkip })
            {
                tb.style.color = new Color(0.98f, 0.93f, 0.78f);
                tb.style.fontSize = 22f;
                tb.style.unityFontStyleAndWeight = FontStyle.Bold;
                tb.style.unityTextAlign = TextAnchor.MiddleCenter;
                tb.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.7f);
                tb.style.unityTextOutlineWidth = 0.2f;
                tb.style.justifyContent = Justify.Center;
                tb.style.paddingLeft = 22f;
            }
            tutorialPlay.RegisterCallback<PointerEnterEvent>(_ => tutorialPlay.Focus());
            tutorialSkip.RegisterCallback<PointerEnterEvent>(_ => tutorialSkip.Focus());
            tutorialPlay.RegisterCallback<PointerLeaveEvent>(_ => tutorialPlay.Blur());
            tutorialSkip.RegisterCallback<PointerLeaveEvent>(_ => tutorialSkip.Blur());

            box.Add(title);
            box.Add(hint);
            box.Add(tutorialPlay);
            box.Add(tutorialSkip);
            tutorialPanel.Add(box);
            menu.Add(tutorialPanel);
        }
    }
}
