using UnityEngine;

namespace NebulaSoft
{
    public static class PhysicsHelper
    {
        public static readonly int LAYER_OUTLINE = LayerMask.NameToLayer("Outlined");
        public static readonly int LAYER_GROUND = LayerMask.NameToLayer("Ground");
        public static readonly LayerMask LAYER_MASK_GROUND = LayerMask.GetMask("Ground");
    }
}