using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Placa de hielo solida sobre una superficie de agua (reaccion agua + cristal): la crean los lagos (<see cref="WaterVolume"/>)
    /// y las zonas inundadas (<see cref="FloodZone"/>) al congelarse. Sostiene al jugador y a los objetos, es resbaladiza, dura
    /// unos segundos y se derrite (se achica y desaparece). Maximo MaxSheets vivas: la mas vieja se derrite antes.
    /// Se borra al reiniciar el mundo.
    /// </summary>
    public class IceSheet : MonoBehaviour
    {
        public const int MaxSheets = 6;
        public const float MeltSeconds = 1.5f;
        public const float DefaultThickness = 0.1f;

        private static readonly List<IceSheet> all = new List<IceSheet>();
        public static int ActiveCount => all.Count;
        public static IReadOnlyList<IceSheet> All => all;
        /// <summary>Se creo una placa (en red, el anfitrion la anuncia a los clientes).</summary>
        public static event Action<IceSheet> Spawned;

        private float meltAt;
        private float meltStart = -1f;
        private Vector3 fullScale;
        public Vector3 Top { get; private set; }
        public float HalfX { get; private set; }
        public float HalfZ { get; private set; }
        public float Thickness { get; private set; }
        /// <summary>Segundos que le quedan antes de empezar a derretirse (0 si ya se derrite).</summary>
        public float SecondsLeft => meltStart >= 0f ? 0f : Mathf.Max(0f, meltAt - Time.time);

        /// <summary>
        /// Crea una placa con la cara de arriba en <paramref name="top"/> y semiejes <paramref name="halfX"/> x <paramref name="halfZ"/> (m).
        /// </summary>
        public static IceSheet Create(Vector3 top, float halfX, float halfZ, float thickness, float seconds)
        {
            if (halfX <= 0.01f || halfZ <= 0.01f || seconds <= 0f) return null;
            if (Net.NetCast.IsClientOnly && !Net.NetEffectsSync.Applying) return null; // en red solo el anfitrion crea hielo; los clientes reciben el aviso
            all.RemoveAll(s => s == null);
            if (all.Count >= MaxSheets) all[0].BeginMelt();

            var go = new GameObject("Hielo");
            go.transform.position = top + Vector3.up * 0.005f;
            var sheet = go.AddComponent<IceSheet>();

            var visual = WaterMeshes.CreateIce("Placa", go.transform);
            visual.transform.localScale = new Vector3(halfX, thickness, halfZ);

            // Rigidbody cinematico: achicarla al derretirse no reconstruye la escena estatica.
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -thickness * 0.5f, 0f);
            // El octogono inscribe ~92 % del cuadrado: el collider no sobresale del dibujo.
            box.size = new Vector3(halfX * 2f * 0.92f, thickness, halfZ * 2f * 0.92f);
            box.sharedMaterial = WaterMeshes.IcePhysics;

            sheet.fullScale = Vector3.one;
            sheet.meltAt = Time.time + seconds;
            sheet.Top = top; sheet.HalfX = halfX; sheet.HalfZ = halfZ; sheet.Thickness = thickness;
            all.Add(sheet);
            Spawned?.Invoke(sheet);
            return sheet;
        }

        /// <summary>Derrite todas las placas en el acto (reinicio del mundo).</summary>
        public static void ClearAll()
        {
            for (int i = all.Count - 1; i >= 0; i--) if (all[i] != null) Destroy(all[i].gameObject);
            all.Clear();
        }

        /// <summary>Extiende la vida de la placa (otro hechizo de cristal sobre el mismo hielo).</summary>
        public void Extend(float seconds)
        {
            if (meltStart >= 0f) return;
            meltAt = Mathf.Max(meltAt, Time.time + seconds);
        }

        public void BeginMelt()
        {
            if (meltStart < 0f) meltStart = Time.time;
        }

        private void OnEnable() => MagicWorld.WorldReset += OnWorldReset;
        private void OnDisable() => MagicWorld.WorldReset -= OnWorldReset;
        private void OnDestroy() => all.Remove(this);
        private void OnWorldReset() => Destroy(gameObject);

        private void Update()
        {
            if (meltStart < 0f)
            {
                if (Time.time >= meltAt) BeginMelt();
                return;
            }
            float k = 1f - Mathf.Clamp01((Time.time - meltStart) / MeltSeconds);
            transform.localScale = new Vector3(fullScale.x * k, fullScale.y * Mathf.Max(k, 0.2f), fullScale.z * k);
            if (k <= 0f) Destroy(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();
    }
}
