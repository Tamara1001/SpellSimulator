using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>Selección sin estado de las etiquetas que quedan dentro del cono de mirada.</summary>
    public static class LabGazeTags
    {
        public const float ShowAngleDeg = 25f;
        public const float HideAngleDeg = 32f;
        public const float MaxDistance = 4f;
        public const int MaxVisible = 4;

        /// <summary>Menor puntuación significa mayor prioridad; fuera de rango devuelve infinito.</summary>
        public static float Score(float angleDeg, float distance)
        {
            if (!(angleDeg >= 0f) || !(distance >= 0f) || distance > MaxDistance || angleDeg > ShowAngleDeg)
                return float.PositiveInfinity;
            return angleDeg + distance * 0.001f;
        }

        /// <summary>Selecciona por prioridad con umbral de entrada/salida y desempate estable por índice.</summary>
        public static int Select(Vector3 eye, Vector3 forward, Vector3[] positions, int count, bool[] wasVisible,
            int[] selected, float[] scores, int limit = MaxVisible)
        {
            if (positions == null || wasVisible == null || selected == null || scores == null) return 0;
            int n = Mathf.Min(count, Mathf.Min(positions.Length, wasVisible.Length));
            int cap = Mathf.Min(Mathf.Min(limit, MaxVisible), Mathf.Min(selected.Length, scores.Length));
            if (cap <= 0 || n <= 0) return 0;
            int found = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 delta = positions[i] - eye;
                float distance = delta.magnitude;
                float angle = distance > 1e-5f ? Vector3.Angle(forward, delta) : 0f;
                float maxAngle = wasVisible[i] ? HideAngleDeg : ShowAngleDeg;
                bool withinAngle = wasVisible[i] ? angle < maxAngle : angle <= maxAngle;
                float score = distance <= MaxDistance && withinAngle ? angle + distance * 0.001f : float.PositiveInfinity;
                if (float.IsInfinity(score)) continue;
                if (found == cap && (score > scores[cap - 1] || (score == scores[cap - 1] && i > selected[cap - 1]))) continue;
                int at = found;
                if (at > cap - 1) at = cap - 1;
                while (at > 0 && (scores[at - 1] > score || (scores[at - 1] == score && selected[at - 1] > i))) at--;
                if (found < cap) found++;
                if (at >= cap) continue;
                for (int k = found - 1; k > at; k--) { scores[k] = scores[k - 1]; selected[k] = selected[k - 1]; }
                scores[at] = score;
                selected[at] = i;
            }
            return found;
        }
    }
}
