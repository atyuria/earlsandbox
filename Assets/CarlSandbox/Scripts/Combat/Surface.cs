using UnityEngine;

namespace Carl
{
    public enum SurfaceKind { Hard, Dirt }

    /// <summary>What a piece of the world is made of, where it matters (bullets into earth throw dirt, not sparks).</summary>
    public class Surface : MonoBehaviour
    {
        public SurfaceKind kind = SurfaceKind.Hard;
    }
}
