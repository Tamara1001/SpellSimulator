namespace SpellSimulator.Compiler
{
    public enum ElementType
    {
        None,
        Fire,
        Air
    }

    /// <summary>
    /// Representa un hechizo compilado y listo para ser ejecutado por el VFX Graph.
    /// Implementado como un 'struct' inmutable para evitar generar basura (GC Alloc)
    /// cada vez que se compila y transfiere un hechizo entre módulos.
    /// </summary>
    public struct SpellDescriptor
    {
        public readonly ElementType primaryElement;
        public readonly float scaleMultiplier;
        public readonly bool isSignedAndActive;
        
        public SpellDescriptor(ElementType element, float scale, bool isActive)
        {
            this.primaryElement = element;
            this.scaleMultiplier = scale;
            this.isSignedAndActive = isActive;
        }
    }
}
