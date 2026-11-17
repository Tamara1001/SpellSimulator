using System;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;
using SpellSimulator.World.Map;

namespace SpellSimulator.World
{
    /// <summary>
    /// Torre-tutorial (Docs/MAPA_FLUJO_PLAN.md, §17): mientras el tutorial esta activo, una barrera magica encierra la torre;
    /// cada nivel pasado enciende su farol y al cruzar la puerta la barrera se apaga. Vive en el pie de la torre (su transform
    /// es el origen de las reglas de <see cref="TutorialRules"/>). Lo arranca y lo corta <see cref="PlayerSpawn.Begin"/>.
    /// </summary>
    public class TowerTutorial : MonoBehaviour
    {
        // Revision barata de la posicion del jugador (sin allocations).
        private const float CheckPeriod = 0.2f;

        [SerializeField, Tooltip("Lo que solo existe durante el tutorial: barrera magica y palanca de salteo.")]
        private GameObject activeOnly;
        [SerializeField, Tooltip("Luz de cada farol de nivel (se enciende al pasar el nivel).")]
        private GameObject[] glows = Array.Empty<GameObject>();

        private readonly TutorialProgress progress = new TutorialProgress();
        private float checkTimer;

        public static TowerTutorial Instance { get; private set; }

        /// <summary>Paso un nivel (indice 0..3).</summary>
        public static event Action<int> LevelPassed;
        /// <summary>El jugador salio de la torre: tutorial terminado.</summary>
        public static event Action Completed;

        public bool IsActive => progress.Active;
        public int Passed => progress.Passed;
        public bool BarrierUp => activeOnly != null && activeOnly.activeSelf;

        /// <summary>Lo llama el generador del mapa (MapTutorial).</summary>
        public void Configure(GameObject activeOnlyRoot, GameObject[] levelGlows)
        {
            activeOnly = activeOnlyRoot;
            glows = levelGlows;
        }

        private void Awake()
        {
            Instance = this;
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Empieza (o rearma) el tutorial: barrera puesta y faroles apagados.</summary>
        public void Begin()
        {
            progress.Begin();
            checkTimer = 0f;
            Apply();
        }

        /// <summary>Lo corta (salteo): la barrera se apaga.</summary>
        public void Skip()
        {
            progress.Skip();
            Apply();
        }

        private void Update()
        {
            if (!progress.Active) return;
            checkTimer += Time.deltaTime;
            if (checkTimer < CheckPeriod) return;
            checkTimer = 0f;
            IPlayerContext local = PlayerRegistry.Local;
            if (local == null || local.Root == null) return;
            int before = progress.Passed;
            if (progress.Advance(transform.InverseTransformPoint(local.Root.position)) == 0) return;
            Apply();
            for (int level = before; level < progress.Passed; level++)
            {
                Debug.Log($"[Tutorial] nivel {level + 1} de {TutorialRules.Levels} pasado", this);
                LevelPassed?.Invoke(level);
            }
            if (!progress.Completed) return;
            PlayerSpawn.TutorialSeen = true;
            Debug.Log("[Tutorial] el jugador salio de la torre: la barrera se apaga", this);
            Completed?.Invoke();
        }

        private void Apply()
        {
            if (activeOnly != null && activeOnly.activeSelf != progress.Active) activeOnly.SetActive(progress.Active);
            for (int i = 0; i < glows.Length; i++)
            {
                bool lit = i < progress.Passed;
                if (glows[i] != null && glows[i].activeSelf != lit) glows[i].SetActive(lit);
            }
        }
    }
}
