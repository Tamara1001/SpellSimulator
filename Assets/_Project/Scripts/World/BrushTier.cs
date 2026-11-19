using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.World
{
    /// <summary>
    /// Le da a una pluma o pincel del mundo el ancho, el nombre y el gasto de uno de los cinco modelos del catalogo (<see cref="BrushRules.Catalog"/>).
    /// Asi el modelo 3D (lapiz, pincel, pincel plano, brocha) y el comportamiento coinciden sin tocar el ToolDefinition.
    /// </summary>
    [RequireComponent(typeof(PhysicalTool))]
    public class BrushTier : MonoBehaviour
    {
        [SerializeField, Range(0, 4), Tooltip("Indice en BrushRules.Catalog (0 pluma fina ... 4 brocha).")]
        private int index = 1;

        public int Index => index;

        public void Configure(int index) => this.index = Mathf.Clamp(index, 0, BrushRules.Catalog.Length - 1);

        private void Start()
        {
            if (TryGetComponent(out PhysicalTool tool)) tool.ApplyBrush(BrushRules.Catalog[Mathf.Clamp(index, 0, BrushRules.Catalog.Length - 1)]);
        }
    }
}
