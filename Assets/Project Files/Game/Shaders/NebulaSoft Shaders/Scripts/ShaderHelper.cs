using UnityEngine;

namespace NebulaSoft
{
    public static class ShaderHelper
    {
        private const string OUTLINE_KEYWORD = "OUTLINE_ON";
        private const string OUTLINE_PASS_NAME = "SRPDefaultUnlit";

        private static readonly int OUTLINE_WIDTH_HASH = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OUTLINE_COLOR_HASH = Shader.PropertyToID("_OutlineColor");

        public static void SetOutlineState(this Material material, bool state)
        {
            if (state)
            {
                material.EnableKeyword(OUTLINE_KEYWORD);
            }
            else
            {
                material.DisableKeyword(OUTLINE_KEYWORD);
            }

            material.SetShaderPassEnabled(OUTLINE_PASS_NAME, state);
        }

        public static void SetOutlineWidth(this Material material, float value)
        {
            material.SetFloat(OUTLINE_WIDTH_HASH, value);
        }

        public static void SetOutlineWidth(this Renderer renderer, MaterialPropertyBlock propertyBlock, float value)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(OUTLINE_WIDTH_HASH, value);
            renderer.SetPropertyBlock(propertyBlock);
        }

        public static void SetOutlineColor(this Material material, Color color)
        {
            material.SetColor(OUTLINE_COLOR_HASH, color);
        }

        public static void SetOutlineColor(this Renderer renderer, MaterialPropertyBlock propertyBlock, Color color)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(OUTLINE_COLOR_HASH, color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
