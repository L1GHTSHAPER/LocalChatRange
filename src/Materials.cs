using UnityEngine;
using UnityEngine.Rendering;

namespace LocalChatRange
{
    /// <summary>
    /// Builds unlit, transparent, vertex-coloured materials from shaders that ship with every Unity player.
    /// The game uses URP; shaders without a LightMode tag are drawn by URP as SRPDefaultUnlit.
    /// </summary>
    internal static class Materials
    {
        const string InternalColored = "Hidden/Internal-Colored";
        const string SpritesDefault = "Sprites/Default";
        const string UrpUnlit = "Universal Render Pipeline/Unlit";

        // Drawn after the game's own transparent objects (water) so the area stays visible on top of them.
        public const int Queue = (int)RenderQueue.Transparent + 50;

        static string _shaderInUse;

        public static string ShaderInUse => _shaderInUse ?? "none";

        public static Material Create(string name)
        {
            Shader shader = null;
            foreach (string candidate in new[] { InternalColored, SpritesDefault, UrpUnlit })
            {
                shader = Shader.Find(candidate);
                if (shader != null)
                {
                    _shaderInUse = candidate;
                    break;
                }
            }
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = Queue
            };

            if (_shaderInUse == InternalColored)
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.SetInt("_Cull", (int)CullMode.Off);
                material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
                material.SetFloat("_ZBias", -1f);
            }
            else if (_shaderInUse == UrpUnlit)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return material;
        }

        public static void SetColor(Material material, Color color)
        {
            if (material != null)
                material.color = color;
        }

        /// <summary>Draws on top of all geometry (only supported by Hidden/Internal-Colored).</summary>
        public static void SetAlwaysOnTop(Material material, bool onTop)
        {
            if (material != null && material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)(onTop ? CompareFunction.Always : CompareFunction.LessEqual));
        }

        public static void ConfigureRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
        }
    }
}
