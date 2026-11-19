using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Caballete con el lienzo fijo a su cuerpo (tutorial): el lienzo conserva su propio Rigidbody cinematico y, al solaparse con la
    /// madera, empujaba y volcaba al caballete. Al arrancar se ignoran las colisiones entre las piezas del caballete y las del lienzo.
    /// </summary>
    public class EaselCollisionFilter : MonoBehaviour
    {
        private void Awake()
        {
            Rigidbody self = GetComponent<Rigidbody>();
            var all = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].attachedRigidbody != self) continue;
                for (int j = 0; j < all.Length; j++)
                {
                    if (all[j].attachedRigidbody == self || all[j].attachedRigidbody == null) continue;
                    Physics.IgnoreCollision(all[i], all[j], true);
                }
            }
        }
    }
}
