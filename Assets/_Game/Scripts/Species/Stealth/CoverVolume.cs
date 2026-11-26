using UnityEngine;

namespace Dino.Species
{
    /// <summary>
    /// Marks tall grass, ferns or deep shade where dinos can hide. Put it on a trigger collider
    /// (the map thread places these over grass patches). Stealthy species and small hatchlings
    /// standing still or sneaking inside are only spotted face to face.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CoverVolume : MonoBehaviour
    {
        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }
    }
}
