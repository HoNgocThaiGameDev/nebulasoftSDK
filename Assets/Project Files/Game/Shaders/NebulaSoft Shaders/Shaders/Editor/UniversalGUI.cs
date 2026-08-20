using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NebulaSoft.Shader
{
    public class UniversalGUI : ShaderGUI
    {
        private static string[] toolBarOptions = { "Off", "On" };
        private Dictionary<ShadowType, string> shadowKeys = new Dictionary<ShadowType, string>()
        {
            { ShadowType.None, "_SHADOWS_NONE" },
            { ShadowType.Vertex, "_SHADOWS_VERTEX" },
            { ShadowType.Pixel, "_SHADOWS_PIXEL" },
        };

        private OpacityType opacity;
        private ShadowType shadowType;
        private bool mainContainerIsExpanded;
        private bool shadingContainerIsExpanded;
        private bool outlineContainerIsExpanded;
        private bool advancedContainerIsExpanded;
        private bool queueContainerIsExpanded;
        private bool debugContainerIsExpanded;
        private bool propertiesLoaded;
        private bool debugPropertyListInited;
        private List<MaterialProperty> debugPropertyList;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // Custom code that controls the appearance of the Inspector goes here

            //base.OnGUI(materialEditor, properties);

            if (materialEditor.targets.Length > 1)
            {
                EditorGUILayout.LabelField("Multi editing is not supported.");
                return;
            }

            Material material = materialEditor.target as Material;

            MaterialProperty _Transparent = FindProperty("_Transparent", properties);

            MaterialProperty _Color = FindProperty("_Color", properties);
            MaterialProperty _MainTex = FindProperty("_MainTex", properties);
            MaterialProperty _NormalMap = FindProperty("_NormalMap", properties);
            MaterialProperty _LightColorOn = FindProperty("_LightColorOn", properties);
            MaterialProperty _LightColorInfluence = FindProperty("_LightColorInfluence", properties);

            MaterialProperty _Shadows = FindProperty("_Shadows", properties);
            MaterialProperty _SColor = FindProperty("_SColor", properties);

            MaterialProperty _EmissionOn = FindProperty("_EmissionOn", properties);
            MaterialProperty _Emission = FindProperty("_EmissionColor", properties);
            MaterialProperty _EmissionTex = FindProperty("_EmissionTex", properties);

            MaterialProperty _RimOn = FindProperty("_RimOn", properties);
            MaterialProperty _RimColor = FindProperty("_RimColor", properties);
            MaterialProperty _RimMin = FindProperty("_RimMin", properties);
            MaterialProperty _RimMax = FindProperty("_RimMax", properties);
            MaterialProperty _DirRim = FindProperty("_DirRim", properties);

            MaterialProperty _SpecularOn = FindProperty("_SpecularOn", properties);
            MaterialProperty _Specular = FindProperty("_Specular", properties);
            MaterialProperty _Shiness = FindProperty("_Shiness", properties);
            MaterialProperty _SpecularMin = FindProperty("_SpecularMin", properties);
            MaterialProperty _SpecularMax = FindProperty("_SpecularMax", properties);
            MaterialProperty _SpecularMap = FindProperty("_SpecularMap", properties);

            MaterialProperty _CustomLightDirectionOn = FindProperty("_CustomLightDirectionOn", properties);
            MaterialProperty _CustomLightDirection = FindProperty("_CustomLightDirection", properties);

            MaterialProperty _ToonOn = FindProperty("_ToonOn", properties);
            MaterialProperty _RampTex = FindProperty("_RampTex", properties);
            MaterialProperty _RampMin = FindProperty("_RampMin", properties);
            MaterialProperty _RampMax = FindProperty("_RampMax", properties);

            MaterialProperty _OutlineOn = FindProperty("_OutlineOn", properties);
            MaterialProperty _OutlineWidth = FindProperty("_OutlineWidth", properties);
            MaterialProperty _OutlineColor = FindProperty("_OutlineColor", properties);
            MaterialProperty _OutlineScale = FindProperty("_OutlineScale", properties);
            MaterialProperty _OutlineDepthOffset = FindProperty("_OutlineDepthOffset", properties);
            MaterialProperty _CameraDistanceImpact = FindProperty("_CameraDistanceImpact", properties);

            MaterialProperty _FoliageOn = FindProperty("_FoliageOn", properties);
            MaterialProperty _FoliageHeightDisplacementMultiplier = FindProperty("_FoliageHeightDisplacementMultiplier", properties);
            MaterialProperty _MaxTargetDistance = FindProperty("_MaxTargetDistance", properties);
            MaterialProperty _FoliageMask = FindProperty("_FoliageMask", properties);

            MaterialProperty _WindOn = FindProperty("_WindOn", properties);
            MaterialProperty _WindTexSize = FindProperty("_WindTexSize", properties);
            MaterialProperty _WindVelocity = FindProperty("_WindVelocity", properties);
            MaterialProperty _WindPower = FindProperty("_WindPower", properties);
            MaterialProperty _WindTex = FindProperty("_WindTex", properties);

            MaterialProperty _UseGlobalMultipliers = FindProperty("_UseGlobalMultipliers", properties);

            List<MaterialProperty> list = new List<MaterialProperty> {
                _Transparent, _Color, _MainTex, _NormalMap, _LightColorOn,_LightColorInfluence, _Shadows, _SColor, _EmissionOn, _Emission, _EmissionTex,
                _RimOn, _RimColor, _RimMin, _RimMax,_DirRim, _SpecularOn, _ToonOn, _RampTex, _RampMin, _RampMax,
                _OutlineOn, _Specular, _Shiness, _SpecularMin, _SpecularMax, _SpecularMap, _CustomLightDirectionOn, _CustomLightDirection,
                _OutlineWidth, _OutlineColor,_OutlineScale, _OutlineDepthOffset, _CameraDistanceImpact, _FoliageOn, _FoliageHeightDisplacementMultiplier,
                _MaxTargetDistance, _FoliageMask, _WindOn, _WindTexSize, _WindVelocity, _WindPower, _WindTex,_UseGlobalMultipliers
            };

            if (!propertiesLoaded)
            {
                mainContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_mainContainerIsExpanded", false);
                shadingContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_shadingContainerIsExpanded", false);
                outlineContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_outlineContainerIsExpanded", false);
                advancedContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_advancedContainerIsExpanded", false);
                queueContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_queueContainerIsExpanded", false);
                debugContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_debugContainerIsExpanded", false);
                propertiesLoaded = true;
            }

            // begin drawing editor
            mainContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(mainContainerIsExpanded, "Main");

            if (mainContainerIsExpanded)
            {
                DisplayOpacity(material, _Transparent);
                DisplayColor(_Color, _MainTex, _NormalMap);
                DisplayShadows(material, _Shadows, _SColor);
                DisplayEmission(material, _EmissionOn, _Emission, _EmissionTex);
                DisplayLightColor(_LightColorOn, _LightColorInfluence);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            shadingContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(shadingContainerIsExpanded, "Shading");

            if (shadingContainerIsExpanded)
            {
                DisplayToonShader(material, _ToonOn, _RampTex, _RampMin, _RampMax);
                DisplayRimShader(material, _RimOn, _RimColor, _RimMin, _RimMax, _DirRim);
                DisplaySpecularShader(material, _SpecularOn, _Specular, _Shiness, _SpecularMin, _SpecularMax, _SpecularMap);
                DisplayCustomLightDirection(material, _CustomLightDirectionOn, _CustomLightDirection);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            outlineContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(outlineContainerIsExpanded, "Outline");

            if (outlineContainerIsExpanded)
            {
                DisplayOutline(material, _OutlineOn, _OutlineWidth, _OutlineColor, _OutlineScale, _OutlineDepthOffset, _CameraDistanceImpact);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            advancedContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(advancedContainerIsExpanded, "Advanced");

            if (advancedContainerIsExpanded)
            {
                DisplayFolliage(material, _FoliageOn, _FoliageHeightDisplacementMultiplier, _MaxTargetDistance, _FoliageMask);

                if (Mathf.RoundToInt(_FoliageOn.floatValue) == 1)
                {
                    DisplayWind(material, _WindOn, _WindTexSize, _WindVelocity, _WindPower, _WindTex);
                }

                DisplayUseGlobalMultipliers(material, _UseGlobalMultipliers);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            queueContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(queueContainerIsExpanded, "Queue");

            if (queueContainerIsExpanded)
            {
                DisplayQueue(material);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            HandleDebugProperties(materialEditor, properties, list);
            //save properties
            EditorPrefs.SetBool("Editor_UniversalGUI_mainContainerIsExpanded", mainContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_shadingContainerIsExpanded", shadingContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_outlineContainerIsExpanded", outlineContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_advancedContainerIsExpanded", advancedContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_queueContainerIsExpanded", queueContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_debugContainerIsExpanded", debugContainerIsExpanded);
        }

        private static void DrawVector2Property(GUIContent content, MaterialProperty property, int intendLevel)
        {
            Rect fullRect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.textField);

            for (int i = 0; i < intendLevel; i++)
            {
                EditorGUI.indentLevel++;
            }

            Rect controlRect = EditorGUI.PrefixLabel(fullRect, content);

            for (int i = 0; i < intendLevel; i++)
            {
                EditorGUI.indentLevel--;
            }

            property.vectorValue = EditorGUI.Vector2Field(controlRect, GUIContent.none, property.vectorValue);
        }


        private void DisplayOpacity(Material material, MaterialProperty _Transparent)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel(new GUIContent("Opacity",
                "Uses LOD to swap subshaders\n" +
                "'Opaque'        = 400\n" +
                "'Transparent' = 200"
                ));

            opacity = (OpacityType)_Transparent.floatValue;

            opacity = (OpacityType)EditorGUILayout.EnumPopup(opacity);

            _Transparent.floatValue = (int)opacity;

            if (opacity == OpacityType.Transparent)
            {
                material.SetFloat("_Mode", 3.0f); // From C# enum BlendMode
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0.0f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                //material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetFloat("_Surface", 1.0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else if (opacity == OpacityType.Opaque)
            {
                material.SetFloat("_Mode", 0.0f); // From C# enum BlendMode
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                material.SetFloat("_ZWrite", 1.0f);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                //material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                material.SetFloat("_Surface", 0.0f);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DisplayColor(MaterialProperty _Color, MaterialProperty _MainTex, MaterialProperty _NormalMap)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Color",
                "Texture = '_MainTex'\n" +
                "Normal =  '_NormalMap'\n" +
                "Color    = '_Color'"
                ));
            _MainTex.textureValue = (Texture)EditorGUILayout.ObjectField(_MainTex.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));
            _NormalMap.textureValue = (Texture)EditorGUILayout.ObjectField(_NormalMap.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));
            _Color.colorValue = EditorGUILayout.ColorField(_Color.colorValue, GUILayout.Height(50));
            EditorGUILayout.EndHorizontal();
        }

        private void DisplayShadows(Material material, MaterialProperty _Shadows, MaterialProperty _SColor)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(
                "Shadows", "Uses keywords:\n" +
                "'None'   = '_SHADOWS_NONE' (Fastest)\n" +
                "'Vertex' = '_SHADOWS_VERTEX' (Average)\n" +
                "'Pixel'    = '_SHADOWS_PIXEL' (Slowest)\n\n" +
                "Shadow Color = '_SColor'"
                ));

            shadowType = (ShadowType)_Shadows.floatValue;
            shadowType = (ShadowType)EditorGUILayout.EnumPopup(shadowType);
            _Shadows.floatValue = (int)shadowType;

            foreach (var key in shadowKeys.Keys)
            {
                if (key == shadowType)
                {
                    material.EnableKeyword(shadowKeys[key]);
                }
                else
                {
                    material.DisableKeyword(shadowKeys[key]);
                }
            }

            EditorGUI.BeginDisabledGroup(shadowType == ShadowType.None);
            _SColor.colorValue = EditorGUILayout.ColorField(_SColor.colorValue);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private static void DisplayEmission(Material material, MaterialProperty _EmissionOn, MaterialProperty _Emission, MaterialProperty _EmissionTex)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Emission",
            "Uses keyword 'EMISSION_ON'\n" +
            "Texture = '_EmissionTex'\n" +
            "Color     = '_EmissionColor'"
            ));
            _EmissionOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_EmissionOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_EmissionOn.floatValue) == 1)
            {
                material.EnableKeyword("EMISSION_ON");
                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Texture and Color",
                "Texture = '_EmissionTex'\n" +
                "Color     = '_EmissionColor'"
                ));
                EditorGUI.indentLevel--;

                _EmissionTex.textureValue = (Texture)EditorGUILayout.ObjectField(_EmissionTex.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));
                _Emission.colorValue = EditorGUILayout.ColorField(GUIContent.none, _Emission.colorValue, true, false, true, GUILayout.Height(50), GUILayout.MinWidth(65), GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

            }
            else
            {
                material.DisableKeyword("EMISSION_ON");
            }
        }

        private static void DisplayLightColor(MaterialProperty _LightColorOn, MaterialProperty _LightColorInfluence)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Light color", "Float = '_LightColorOn'\nInfluence = '_LightColorInfluence'"));
            _LightColorOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_LightColorOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_LightColorOn.floatValue) == 1)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Influence", "Influence = '_LightColorInfluence'"));
                EditorGUI.indentLevel--;
                _LightColorInfluence.floatValue = EditorGUILayout.Slider(_LightColorInfluence.floatValue, 0, 1);
                EditorGUILayout.EndHorizontal();

            }
        }


        private static void DisplayToonShader(Material material, MaterialProperty _ToonOn, MaterialProperty _RampTex, MaterialProperty _RampMin, MaterialProperty _RampMax)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Toon",
            "Uses keyword 'TOON_ON'\n" +
            "Min     = '_RampMin'\n" +
            "Max     = '_RampMax'\n" +
            "Texture = '_RampTex'"
            ));
            _ToonOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_ToonOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_ToonOn.floatValue) == 1)
            {
                material.EnableKeyword("TOON_ON");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Min",
                "Min     = '_RampMin'\n" +
                "Texture = '_RampTex'"
                ));
                EditorGUI.indentLevel--;

                _RampMin.colorValue = EditorGUILayout.ColorField(_RampMin.colorValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Max",
                "Max     = '_RampMax'\n" +
                "Texture = '_RampTex'"
                ));
                EditorGUI.indentLevel--;

                _RampMax.colorValue = EditorGUILayout.ColorField(_RampMax.colorValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                _RampTex.textureValue = (Texture)EditorGUILayout.ObjectField(_RampTex.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("TOON_ON");
            }
        }

        private static void DisplayRimShader(Material material, MaterialProperty _RimOn, MaterialProperty _RimColor, MaterialProperty _RimMin, MaterialProperty _RimMax, MaterialProperty _DirRim)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Rim",
            "Uses keyword 'RIM_ON'\n" +
            "Min    = '_RimMin'\n" +
            "Max   = '_RimMax'\n" +
            "Directional strength   = '_DirRim'\n" +
            "Color = '_RimColor'"
            ));
            _RimOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_RimOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_RimOn.floatValue) == 1)
            {
                material.EnableKeyword("RIM_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Min", "Min = '_RimMin'"));
                EditorGUI.indentLevel--;
                _RimMin.floatValue = EditorGUILayout.Slider(_RimMin.floatValue, 0, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Max", "Max = '_RimMax'"));
                EditorGUI.indentLevel--;
                _RimMax.floatValue = EditorGUILayout.Slider(_RimMax.floatValue, 0, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Directional strength", "Directional strength = '_DirRim'"));
                EditorGUI.indentLevel--;
                _DirRim.floatValue = EditorGUILayout.Slider(_DirRim.floatValue, 0, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Color", "Color = '_RimColor'"));
                EditorGUI.indentLevel--;
                _RimColor.colorValue = EditorGUILayout.ColorField(_RimColor.colorValue);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("RIM_ON");
            }
        }

        private static void DisplaySpecularShader(Material material, MaterialProperty _SpecularOn, MaterialProperty _Specular, MaterialProperty _Shiness, MaterialProperty _SpecularMin, MaterialProperty _SpecularMax, MaterialProperty _SpecularMap)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Specular",
            "Uses keyword 'SPECULAR_ON'\n" +
            "Highlights\", \"Highlights = '_Specular'\n" +
            "Shiness = '_Shiness'\n" +
            "Min = '_SpecularMin'\n" +
            "Min = '_SpecularMin'\n" +
            "Max = '_SpecularMax'\n" +
            "Texture = '_SpecularMap'"
            ));

            _SpecularOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_SpecularOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_SpecularOn.floatValue) == 1)
            {
                material.EnableKeyword("SPECULAR_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Highlights", "Highlights = '_Specular'\nTexture = '_SpecularMap'"));
                EditorGUI.indentLevel--;
                _Specular.floatValue = EditorGUILayout.Slider(_Specular.floatValue, 0, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Shiness", "Shiness = '_Shiness'\nTexture = '_SpecularMap'"));
                EditorGUI.indentLevel--;
                _Shiness.floatValue = EditorGUILayout.Slider(_Shiness.floatValue, 0.1f, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Min", "Min = '_SpecularMin'\nTexture = '_SpecularMap'"));
                EditorGUI.indentLevel--;
                _SpecularMin.colorValue = EditorGUILayout.ColorField(_SpecularMin.colorValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Max", "Max = '_SpecularMax'\nTexture = '_SpecularMap'"));
                EditorGUI.indentLevel--;
                _SpecularMax.colorValue = EditorGUILayout.ColorField(_SpecularMax.colorValue);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                _SpecularMap.textureValue = (Texture)EditorGUILayout.ObjectField(_SpecularMap.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("SPECULAR_ON");
            }
        }

        private static void DisplayCustomLightDirection(Material material, MaterialProperty _CustomLightDirectionOn, MaterialProperty _CustomLightDirection)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Custom Light Direction",
            "Uses keyword 'CUSTOM_LIGHT_DIRECTION_ON'\n" +
            "Direction = '_CustomLightDirection'"
            ));

            _CustomLightDirectionOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_CustomLightDirectionOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_CustomLightDirectionOn.floatValue) == 1)
            {
                material.EnableKeyword("CUSTOM_LIGHT_DIRECTION_ON");

                EditorGUI.indentLevel++;
                _CustomLightDirection.vectorValue = EditorGUILayout.Vector3Field(new GUIContent("Direction", "Direction = '_CustomLightDirection'"), _CustomLightDirection.vectorValue);
                EditorGUI.indentLevel--;
            }
            else
            {
                material.DisableKeyword("CUSTOM_LIGHT_DIRECTION_ON");
            }
        }

        private static void DisplayOutline(Material material, MaterialProperty _OutlineOn, MaterialProperty _OutlineWidth, MaterialProperty _OutlineColor, MaterialProperty _OutlineScale, MaterialProperty _OutlineDepthOffset, MaterialProperty _CameraDistanceImpact)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Outline",
            "Uses keyword 'OUTLINE_ON'\n" +
            "Color    = '_OutlineColor'\n" +
            "Width   = '_OutlineWidth'\n" +
            "Scale strength   = '_OutlineScale'\n" +
            "Depth Offset   = '_OutlineDepthOffset'\n" +
            "Distance Impact = '_CameraDistanceImpact'"
            ));
            _OutlineOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_OutlineOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_OutlineOn.floatValue) == 1)
            {
                material.EnableKeyword("OUTLINE_ON");
                material.SetShaderPassEnabled("SRPDefaultUnlit", true);

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Color", "Color = '_OutlineColor'"));
                EditorGUI.indentLevel--;
                _OutlineColor.colorValue = EditorGUILayout.ColorField(_OutlineColor.colorValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Width", "Width = '_OutlineWidth'"));
                EditorGUI.indentLevel--;
                _OutlineWidth.floatValue = Mathf.Clamp(EditorGUILayout.FloatField(_OutlineWidth.floatValue), 0, float.MaxValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Scale", "Scale = '_OutlineScale'"));
                EditorGUI.indentLevel--;
                _OutlineScale.floatValue = Mathf.Clamp(EditorGUILayout.FloatField(_OutlineScale.floatValue), 0, float.MaxValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Depth Offset", "Depth Offset = '_OutlineDepthOffset'"));
                EditorGUI.indentLevel--;
                _OutlineDepthOffset.floatValue = EditorGUILayout.Slider(_OutlineDepthOffset.floatValue, 0.01f, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Distance Impact", "Distance Impact = '_CameraDistanceImpact'"));
                EditorGUI.indentLevel--;
                _CameraDistanceImpact.floatValue = EditorGUILayout.Slider(_CameraDistanceImpact.floatValue, 0.01f, 1);
                EditorGUILayout.EndHorizontal();


            }
            else
            {
                material.DisableKeyword("OUTLINE_ON");
                material.SetShaderPassEnabled("SRPDefaultUnlit", false);
            }
        }

        private static void DisplayFolliage(Material material, MaterialProperty _FoliageOn, MaterialProperty _FoliageHeightDisplacementMultiplier, MaterialProperty _MaxTargetDistance, MaterialProperty _FoliageMask)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Foliage",
            "Uses keyword 'FOLIAGE_ON'\n" +
            "Displacement Height Multiplier = '_FoliageHeightDisplacementMultiplier'\n" +
            "Max Target Distance = '_MaxTargetDistance'\n" +
            "Texture = '_FoliageMask'"
            ));
            _FoliageOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_FoliageOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_FoliageOn.floatValue) == 1)
            {
                material.EnableKeyword("FOLIAGE_ON");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Displacement Distance",
                "Displacement Distance = '_FoliageHeightDisplacementMultiplier'\n" +
                "Texture = '_FoliageMask'"
                ));
                EditorGUI.indentLevel--;

                _FoliageHeightDisplacementMultiplier.floatValue = EditorGUILayout.Slider(_FoliageHeightDisplacementMultiplier.floatValue, 0.01f, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Max Target Distance",
                "Max Target Distance = '_MaxTargetDistance'\n" +
                "Texture = '_FoliageMask'"
                ));
                EditorGUI.indentLevel--;

                _MaxTargetDistance.floatValue = EditorGUILayout.Slider(_MaxTargetDistance.floatValue, 0.01f, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                _FoliageMask.textureValue = (Texture)EditorGUILayout.ObjectField(_FoliageMask.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("FOLIAGE_ON");
            }
        }

        private static void DisplayWind(Material material, MaterialProperty _WindOn, MaterialProperty _WindTexSize, MaterialProperty _WindVelocity, MaterialProperty _WindPower, MaterialProperty _WindTex)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.indentLevel++;
            EditorGUILayout.PrefixLabel(new GUIContent("Wind",
            "Uses keyword 'WIND_ON'\n" +
            "Pattern Size    = '_WindTexSize'\n" +
            "Velocity   = '_WindVelocity'\n" +
            "Power   = '_WindPower'\n" +
            "Texture   = '_WindTex'"
            ));
            EditorGUI.indentLevel--;
            _WindOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_WindOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_WindOn.floatValue) == 1)
            {
                material.EnableKeyword("WIND_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();
                DrawVector2Property(new GUIContent("Pattern Size ", "Pattern Size  = '_WindTexSize'\nTexture = '_WindTex'"), _WindTexSize,2);
                DrawVector2Property(new GUIContent("Velocity ", "Velocity  = '_WindVelocity'\nTexture = '_WindTex'"), _WindVelocity, 2);

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Power", "Power = '_WindPower'\nTexture = '_WindTex'"));
                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;
                _WindPower.floatValue = EditorGUILayout.Slider(_WindPower.floatValue, 0, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                _WindTex.textureValue = (Texture)EditorGUILayout.ObjectField(_WindTex.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();

            }
            else
            {
                material.DisableKeyword("WIND_ON");
            }
        }

        private static void DisplayUseGlobalMultipliers(Material material, MaterialProperty _UseGlobalMultipliers)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Use Global Multipliers", "Uses keyword 'USE_GLOBAL_MULTIPLIERS'"));
            _UseGlobalMultipliers.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_UseGlobalMultipliers.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_UseGlobalMultipliers.floatValue) == 1)
            {
                material.EnableKeyword("USE_GLOBAL_MULTIPLIERS");
            }
            else
            {
                material.DisableKeyword("USE_GLOBAL_MULTIPLIERS");
            }
        }


        private static void DisplayQueue(Material material)
        {
            material.renderQueue = EditorGUILayout.IntField("Render Queue", material.renderQueue);
            material.enableInstancing = EditorGUILayout.Toggle("Enable GPU Instancing", material.enableInstancing);

            if ((material.renderQueue == -1) || (material.renderQueue == 2450))
            {
                material.renderQueue = 2000;
            }
        }

        private void HandleDebugProperties(MaterialEditor materialEditor, MaterialProperty[] properties, List<MaterialProperty> list)
        {
            if (!debugPropertyListInited)
            {
                debugPropertyList = new List<MaterialProperty>();

                for (int i = 0; i < properties.Length; i++)
                {
                    if (list.Contains(properties[i]) || properties[i].displayName == string.Empty)
                    {
                        continue;
                    }

                    debugPropertyList.Add(properties[i]);
                }

                debugPropertyListInited = true;
            }

            if (debugPropertyList.Count > 0)
            {
                debugContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(debugContainerIsExpanded, "Debug");

                if (debugContainerIsExpanded)
                {
                    foreach (MaterialProperty item in debugPropertyList)
                    {
                        materialEditor.DefaultShaderProperty(item, item.displayName);
                    }
                }

                EditorGUILayoutCustom.EndFoldoutBoxGroup();
            }
        }
    }

    public enum OpacityType
    {
        Opaque = 0,
        Transparent = 1
    }

    public enum ShadowType
    {
        None = 0,
        Vertex = 1,
        Pixel = 2,
    }
}