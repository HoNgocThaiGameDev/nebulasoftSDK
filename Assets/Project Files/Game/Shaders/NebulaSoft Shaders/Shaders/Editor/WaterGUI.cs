using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEditor.Rendering;

namespace NebulaSoft.Shader
{
    public class WaterGUI : ShaderGUI
    {
        private static string[] toolBarOptions = { "Off", "On" };

        private Dictionary<ShadowType, string> shadowKeys = new Dictionary<ShadowType, string>()
        {
            { ShadowType.None, "_SHADOWS_NONE" },
            { ShadowType.Vertex, "_SHADOWS_VERTEX" },
            { ShadowType.Pixel, "_SHADOWS_PIXEL" },
        };

        private bool waterPatternContainerIsExpanded;
        private bool waterDepthContainerIsExpanded;
        private bool foamContainerIsExpanded;
        private bool shadingContainerIsExpanded;
        private bool advancedShadingContainerIsExpanded;
        private bool extraContainerIsExpanded;
        private bool debugContainerIsExpanded;
        private ShadowType shadowType;
        private bool propertiesLoaded;
        private bool debugPropertyListInited;
        private List<MaterialProperty> debugPropertyList;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor.targets.Length > 1)
            {
                EditorGUILayout.LabelField("Multi editing is not supported.");
                return;
            }

            Material material = materialEditor.target as Material;

            MaterialProperty _Pattern1 = FindProperty("_Pattern1", properties);
            MaterialProperty _PatternColor1 = FindProperty("_PatternColor1", properties);
            MaterialProperty _PatternSize1 = FindProperty("_PatternSize1", properties);
            MaterialProperty _PatternVelocity1 = FindProperty("_PatternVelocity1", properties);

            MaterialProperty _SecondPatternOn = FindProperty("_SecondPatternOn", properties);
            MaterialProperty _Pattern2 = FindProperty("_Pattern2", properties);
            MaterialProperty _PatternColor2 = FindProperty("_PatternColor2", properties);
            MaterialProperty _PatternSize2 = FindProperty("_PatternSize2", properties);
            MaterialProperty _PatternVelocity2 = FindProperty("_PatternVelocity2", properties);

            MaterialProperty _DistortionOn = FindProperty("_DistortionOn", properties);
            MaterialProperty _PatternDistortionNoise = FindProperty("_PatternDistortionNoise", properties);
            MaterialProperty _DistortionPower = FindProperty("_DistortionPower", properties);
            MaterialProperty _PatternDistortionNoiseScale = FindProperty("_PatternDistortionNoiseScale", properties);
            MaterialProperty _PatternDistortionNoiseVelocity = FindProperty("_PatternDistortionNoiseVelocity", properties);

            MaterialProperty _ShallowColor = FindProperty("_ShallowColor", properties);
            MaterialProperty _DeepColor = FindProperty("_DeepColor", properties);
            MaterialProperty _DepthColorRange = FindProperty("_DepthColorRange", properties);

            MaterialProperty _SecondaryDeepColorOn = FindProperty("_SecondaryDeepColorOn", properties);
            MaterialProperty _DeepColor2 = FindProperty("_DeepColor2", properties);
            MaterialProperty _DeepNoise = FindProperty("_DeepNoise", properties);
            MaterialProperty _DeepNoisePower = FindProperty("_DeepNoisePower", properties);
            MaterialProperty _DeepNoiseVelocity = FindProperty("_DeepNoiseVelocity", properties);
            MaterialProperty _DeepNoiseScale = FindProperty("_DeepNoiseScale", properties);

            MaterialProperty _FoamColor = FindProperty("_FoamColor", properties);
            MaterialProperty _FoamDepthRange = FindProperty("_FoamDepthRange", properties);

            MaterialProperty _FoamNoiseOn = FindProperty("_FoamNoiseOn", properties);
            MaterialProperty _FoamNoise = FindProperty("_FoamNoise", properties);
            MaterialProperty _FoamNoisePower = FindProperty("_FoamNoisePower", properties);
            MaterialProperty _FoamNoiseVelocity = FindProperty("_FoamNoiseVelocity", properties);
            MaterialProperty _FoamNoiseScale = FindProperty("_FoamNoiseScale", properties);

            MaterialProperty _Color = FindProperty("_Color", properties);
            MaterialProperty _Shadows = FindProperty("_Shadows", properties);
            MaterialProperty _SColor = FindProperty("_SColor", properties);

            MaterialProperty _EmissionOn = FindProperty("_EmissionOn", properties);
            MaterialProperty _EmissionColor = FindProperty("_EmissionColor", properties);
            MaterialProperty _EmissionTex = FindProperty("_EmissionTex", properties);
            MaterialProperty _EmissionTexScale = FindProperty("_EmissionTexScale", properties);
            MaterialProperty _EmissionTexVelocity = FindProperty("_EmissionTexVelocity", properties);

            MaterialProperty _LightColorOn = FindProperty("_LightColorOn", properties);
            MaterialProperty _LightColorInfluence = FindProperty("_LightColorInfluence", properties);

            MaterialProperty _ToonOn = FindProperty("_ToonOn", properties);
            MaterialProperty _RampTex = FindProperty("_RampTex", properties);
            MaterialProperty _RampMin = FindProperty("_RampMin", properties);
            MaterialProperty _RampMax = FindProperty("_RampMax", properties);

            MaterialProperty _NormalMapOn = FindProperty("_NormalMapOn", properties);
            MaterialProperty _NormalMap = FindProperty("_NormalMap", properties);
            MaterialProperty _NormalMapScale = FindProperty("_NormalMapScale", properties);
            MaterialProperty _NormalMapVelocity = FindProperty("_NormalMapVelocity", properties);

            MaterialProperty _SecondaryNormalMapOn = FindProperty("_SecondaryNormalMapOn", properties);
            MaterialProperty _SecondaryNormalMap = FindProperty("_SecondaryNormalMap", properties);
            MaterialProperty _SecondaryNormalMapScale = FindProperty("_SecondaryNormalMapScale", properties);
            MaterialProperty _SecondaryNormalMapVelocity = FindProperty("_SecondaryNormalMapVelocity", properties);

            MaterialProperty _SpecularOn = FindProperty("_SpecularOn", properties);
            MaterialProperty _SpecularMap = FindProperty("_SpecularMap", properties);
            MaterialProperty _Specular = FindProperty("_Specular", properties);
            MaterialProperty _Shiness = FindProperty("_Shiness", properties);
            MaterialProperty _SpecularMin = FindProperty("_SpecularMin", properties);
            MaterialProperty _SpecularMax = FindProperty("_SpecularMax", properties);

            MaterialProperty _CustomLightDirectionOn = FindProperty("_CustomLightDirectionOn", properties);
            MaterialProperty _CustomLightDirection = FindProperty("_CustomLightDirection", properties);

            List<MaterialProperty> list = new List<MaterialProperty> {
                _Pattern1, _PatternColor1, _PatternSize1, _PatternVelocity1,_SecondPatternOn, _Pattern2,_PatternColor2,_PatternSize2,_PatternVelocity2,
                _DistortionOn, _PatternDistortionNoise, _DistortionPower, _PatternDistortionNoiseScale, _PatternDistortionNoiseVelocity,
                _ShallowColor, _DeepColor, _DepthColorRange,_SecondaryDeepColorOn, _DeepColor2, _DeepNoise, _DeepNoisePower, _DeepNoiseVelocity,
                _DeepNoiseScale, _FoamColor, _FoamDepthRange, _FoamNoiseOn, _FoamNoise, _FoamNoisePower, _FoamNoiseVelocity, _FoamNoiseScale,
                _Color, _Shadows, _SColor, _EmissionOn, _EmissionColor, _EmissionTex, _EmissionTexScale, _EmissionTexVelocity, _LightColorOn, _LightColorInfluence,
                _ToonOn, _RampTex, _RampMin, _RampMax, _NormalMapOn, _NormalMap, _NormalMapScale, _NormalMapVelocity,
                _SecondaryNormalMapOn, _SecondaryNormalMap, _SecondaryNormalMapScale, _SecondaryNormalMapVelocity,
                _SpecularOn, _Specular, _Shiness, _SpecularMin, _SpecularMax, _SpecularMap,
                _CustomLightDirectionOn, _CustomLightDirection

            };

            if (!propertiesLoaded)
            {
                waterPatternContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_waterPatternContainerIsExpanded", false);
                waterDepthContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_waterDepthContainerIsExpanded", false);
                foamContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_foamContainerIsExpanded", false);
                shadingContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_shadingContainerIsExpanded", false);
                advancedShadingContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_advancedShadingContainerIsExpanded", false);
                extraContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_extraContainerIsExpanded", false);
                debugContainerIsExpanded = EditorPrefs.GetBool("Editor_UniversalGUI_debugContainerIsExpanded", false);
                propertiesLoaded = true;
            }


            //Begin drawing editor
            waterPatternContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(waterPatternContainerIsExpanded, "Water Pattern");

            if (waterPatternContainerIsExpanded)
            {
                DisplayMainPattern(_Pattern1, _PatternColor1, _PatternSize1, _PatternVelocity1);
                DisplaySecondPattern(material, _SecondPatternOn, _Pattern2, _PatternColor2, _PatternSize2, _PatternVelocity2);
                DisplayDistortion(material, _DistortionOn, _PatternDistortionNoise, _DistortionPower, _PatternDistortionNoiseScale, _PatternDistortionNoiseVelocity);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            waterDepthContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(waterDepthContainerIsExpanded, "Water Depth");

            if (waterDepthContainerIsExpanded)
            {
                DisplayDeepColor(_ShallowColor, _DeepColor, _DepthColorRange);
                DisplaySecondaryDeepColor(material, _SecondaryDeepColorOn, _DeepColor2, _DeepNoise, _DeepNoisePower, _DeepNoiseVelocity, _DeepNoiseScale);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            foamContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(foamContainerIsExpanded, "Foam");

            if (foamContainerIsExpanded)
            {
                DisplayFoam(_FoamColor, _FoamDepthRange);
                DisplayFoamNoise(material, _FoamNoiseOn, _FoamNoise, _FoamNoisePower, _FoamNoiseVelocity, _FoamNoiseScale);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            shadingContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(shadingContainerIsExpanded, "Shading");

            if (shadingContainerIsExpanded)
            {
                DisplayColor(_Color);
                DisplayShadows(material, _Shadows, _SColor);
                DisplayEmission(material, _EmissionOn, _EmissionColor, _EmissionTex, _EmissionTexScale, _EmissionTexVelocity);
                DisplayLightColor(_LightColorOn, _LightColorInfluence);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            advancedShadingContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(advancedShadingContainerIsExpanded, "Advanced Shading");

            if (advancedShadingContainerIsExpanded)
            {
                DisplayToonShader(material, _ToonOn, _RampTex, _RampMin, _RampMax);
                DisplayNormalMap(material, _NormalMapOn, _NormalMap, _NormalMapScale, _NormalMapVelocity);
                DisplaySecondaryNormalMap(material, _SecondaryNormalMapOn, _SecondaryNormalMap, _SecondaryNormalMapScale, _SecondaryNormalMapVelocity);
                DisplaySpecularShader(material, _SpecularOn, _Specular, _Shiness, _SpecularMin, _SpecularMax, _SpecularMap);
                DisplayCustomLightDirection(_CustomLightDirectionOn, _CustomLightDirection);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();


            extraContainerIsExpanded = EditorGUILayoutCustom.BeginFoldoutBoxGroup(extraContainerIsExpanded, "Extra");

            if (extraContainerIsExpanded)
            {
                DisplayQueue(material);
            }

            EditorGUILayoutCustom.EndFoldoutBoxGroup();

            HandleDebugProperties(materialEditor, properties, list);

            //Save properties
            EditorPrefs.SetBool("Editor_UniversalGUI_waterPatternContainerIsExpanded", waterPatternContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_waterDepthContainerIsExpanded", waterDepthContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_foamContainerIsExpanded", foamContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_shadingContainerIsExpanded", shadingContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_advancedShadingContainerIsExpanded", advancedShadingContainerIsExpanded);
            EditorPrefs.SetBool("Editor_UniversalGUI_extraContainerIsExpanded", extraContainerIsExpanded);
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

        private static void DisplayMainPattern(MaterialProperty _Pattern1, MaterialProperty _PatternColor1, MaterialProperty _PatternSize1, MaterialProperty _PatternVelocity1)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Color",
            "Color     = '_PatternColor1'\n" +
            "Texture = '_Pattern1'"
            ));

            _PatternColor1.colorValue = EditorGUILayout.ColorField(_PatternColor1.colorValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Size",
            "Size     = '_PatternSize1'\n" +
            "Texture = '_Pattern1'"
            ));

            _PatternSize1.floatValue = EditorGUILayout.FloatField(_PatternSize1.floatValue);
            EditorGUILayout.EndHorizontal();

            DrawVector2Property(new GUIContent("Velocity", "Velocity = '_PatternVelocity1'\nTexture = '_Pattern1'"), _PatternVelocity1, 0);


            EditorGUILayout.EndVertical();
            _Pattern1.textureValue = (Texture)EditorGUILayout.ObjectField(_Pattern1.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));

            EditorGUILayout.EndHorizontal();
        }

        private static void DisplaySecondPattern(Material material, MaterialProperty _SecondPatternOn, MaterialProperty _Pattern2, MaterialProperty _PatternColor2, MaterialProperty _PatternSize2, MaterialProperty _PatternVelocity2)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Second patern",
            "Uses keyword 'SECOND_PATTERN_ON'\n" +
            "Color     = '_PatternColor2'\n" +
            "Size     = '_PatternSize2'\n" +
            "Velocity     = '_PatternVelocity2'\n" +
            "Texture = '_Pattern2'"
            ));
            _SecondPatternOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_SecondPatternOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_SecondPatternOn.floatValue) == 1)
            {
                material.EnableKeyword("SECOND_PATTERN_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Color",
                "Color     = '_PatternColor2'\n" +
                "Texture = '_Pattern2'"
                ));

                EditorGUI.indentLevel--;

                _PatternColor2.colorValue = EditorGUILayout.ColorField(_PatternColor2.colorValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Size",
                "Size     = '_PatternSize2'\n" +
                "Texture = '_Pattern2'"
                ));
                EditorGUI.indentLevel--;

                _PatternSize2.floatValue = EditorGUILayout.FloatField(_PatternSize2.floatValue);
                EditorGUILayout.EndHorizontal();

                DrawVector2Property(new GUIContent("Velocity", "Velocity = '_PatternVelocity2'\nTexture = '_Pattern2'"), _PatternVelocity2, 1);


                EditorGUILayout.EndVertical();
                _Pattern2.textureValue = (Texture)EditorGUILayout.ObjectField(_Pattern2.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("SECOND_PATTERN_ON");
            }
        }

        private static void DisplayDistortion(Material material, MaterialProperty _DistortionOn, MaterialProperty _PatternDistortionNoise, MaterialProperty _DistortionPower, MaterialProperty _PatternDistortionNoiseScale, MaterialProperty _PatternDistortionNoiseVelocity)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Distortion",
            "Uses keyword 'DISTORTION_ON'\n" +
            "Power     = '_DistortionPower'\n" +
            "Noise Scale     = '_PatternDistortionNoiseScale'\n" +
            "Noise Velocity     = '_PatternDistortionNoiseVelocity'\n" +
            "Texture = '_PatternDistortionNoise'"
            ));
            _DistortionOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_DistortionOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_DistortionOn.floatValue) == 1)
            {
                material.EnableKeyword("DISTORTION_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Power",
                "Power     = '_DistortionPower'\n" +
                "Texture = '_PatternDistortionNoise'"
                ));
                EditorGUI.indentLevel--;

                _DistortionPower.floatValue = EditorGUILayout.FloatField(_DistortionPower.floatValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Noise Scale",
                "Noise Scale     = '_PatternDistortionNoiseScale'\n" +
                "Texture = '_PatternDistortionNoise'"
                ));
                EditorGUI.indentLevel--;

                _PatternDistortionNoiseScale.floatValue = EditorGUILayout.FloatField(_PatternDistortionNoiseScale.floatValue);
                EditorGUILayout.EndHorizontal();

                DrawVector2Property(new GUIContent("Noise Velocity", "Noise Velocity = '_PatternDistortionNoiseVelocity'\nTexture = '_PatternDistortionNoise'"), _PatternDistortionNoiseVelocity, 1);


                EditorGUILayout.EndVertical();
                _PatternDistortionNoise.textureValue = (Texture)EditorGUILayout.ObjectField(_PatternDistortionNoise.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("DISTORTION_ON");
            }
        }

        private static void DisplayDeepColor(MaterialProperty _ShallowColor, MaterialProperty _DeepColor, MaterialProperty _DepthColorRange)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Shallow Color", "Shallow Color     = '_ShallowColor'"));
            _ShallowColor.colorValue = EditorGUILayout.ColorField(_ShallowColor.colorValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Deep Color", "Deep Color     = '_DeepColor'"));
            _DeepColor.colorValue = EditorGUILayout.ColorField(_DeepColor.colorValue);
            EditorGUILayout.EndHorizontal();

            DrawVector2Property(new GUIContent("Color Range", "Color Range = '_DepthColorRange'"), _DepthColorRange, 0);
        }

        private static void DisplaySecondaryDeepColor(Material material, MaterialProperty _SecondaryDeepColorOn, MaterialProperty _DeepColor2, MaterialProperty _DeepNoise, MaterialProperty _DeepNoisePower, MaterialProperty _DeepNoiseVelocity, MaterialProperty _DeepNoiseScale)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Secondary Deep Color",
            "Uses keyword 'SECONDARY_DEEP_COLOR_ON'\n" +
            "Color     = '_DeepColor2'\n" +
            "Power     = '_DeepNoisePower'\n" +
            "Noise Scale     = '_DeepNoiseScale'\n" +
            "Noise Velocity     = '_DeepNoiseVelocity'\n" +
            "Texture = '_DeepNoise'"
            ));
            _SecondaryDeepColorOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_SecondaryDeepColorOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_SecondaryDeepColorOn.floatValue) == 1)
            {
                material.EnableKeyword("SECONDARY_DEEP_COLOR_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Color",
                "Color     = '_DeepColor2'\n" +
                "Texture = '_DeepNoise'"
                ));
                EditorGUI.indentLevel--;

                _DeepColor2.colorValue = EditorGUILayout.ColorField(_DeepColor2.colorValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Power",
                "Power     = '_DeepNoisePower'\n" +
                "Texture = '_DeepNoise'"
                ));
                EditorGUI.indentLevel--;

                _DeepNoisePower.floatValue = EditorGUILayout.FloatField(_DeepNoisePower.floatValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Noise Scale",
                "Noise Scale     = '_DeepNoiseScale'\n" +
                "Texture = '_DeepNoise'"
                ));
                EditorGUI.indentLevel--;

                _DeepNoiseScale.floatValue = EditorGUILayout.FloatField(_DeepNoiseScale.floatValue);
                EditorGUILayout.EndHorizontal();

                DrawVector2Property(new GUIContent("Noise Velocity", "Noise Velocity = '_DeepNoiseVelocity'\nTexture = '_DeepNoise'"), _DeepNoiseVelocity, 1);


                EditorGUILayout.EndVertical();
                _DeepNoise.textureValue = (Texture)EditorGUILayout.ObjectField(_DeepNoise.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("SECONDARY_DEEP_COLOR_ON");
            }
        }

        private static void DisplayFoam(MaterialProperty _FoamColor, MaterialProperty _FoamDepthRange)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Foam Color", "Foam Color     = '_FoamColor'"));
            _FoamColor.colorValue = EditorGUILayout.ColorField(_FoamColor.colorValue);
            EditorGUILayout.EndHorizontal();

            DrawVector2Property(new GUIContent("Depth Range", "Depth Range = '_FoamDepthRange'"), _FoamDepthRange, 0);
        }

        private static void DisplayFoamNoise(Material material, MaterialProperty _FoamNoiseOn, MaterialProperty _FoamNoise, MaterialProperty _FoamNoisePower, MaterialProperty _FoamNoiseVelocity, MaterialProperty _FoamNoiseScale)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Foam Noise",
            "Uses keyword 'FOAM_NOISE_ON'\n" +
            "Power     = '_FoamNoisePower'\n" +
            "Noise Scale     = '_FoamNoiseScale'\n" +
            "Noise Velocity     = '_FoamNoiseVelocity'\n" +
            "Texture = '_FoamNoise'"
            ));
            _FoamNoiseOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_FoamNoiseOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_FoamNoiseOn.floatValue) == 1)
            {
                material.EnableKeyword("FOAM_NOISE_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Power",
                "Power     = '_FoamNoisePower'\n" +
                "Texture = '_FoamNoise'"
                ));
                EditorGUI.indentLevel--;

                _FoamNoisePower.floatValue = EditorGUILayout.FloatField(_FoamNoisePower.floatValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Noise Scale",
                "Noise Scale     = '_FoamNoiseScale'\n" +
                "Texture = '_FoamNoise'"
                ));
                EditorGUI.indentLevel--;

                _FoamNoiseScale.floatValue = EditorGUILayout.FloatField(_FoamNoiseScale.floatValue);
                EditorGUILayout.EndHorizontal();

                DrawVector2Property(new GUIContent("Noise Velocity", "Noise Velocity = '_FoamNoiseVelocity'\nTexture = '_FoamNoise'"), _FoamNoiseVelocity, 1);


                EditorGUILayout.EndVertical();
                _FoamNoise.textureValue = (Texture)EditorGUILayout.ObjectField(_FoamNoise.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("FOAM_NOISE_ON");
            }
        }

        private static void DisplayColor(MaterialProperty _Color)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Color", "Color     = '_Color'"));
            _Color.colorValue = EditorGUILayout.ColorField(_Color.colorValue);
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

        private static void DisplayEmission(Material material, MaterialProperty _EmissionOn, MaterialProperty _EmissionColor, MaterialProperty _EmissionTex, MaterialProperty _EmissionTexScale, MaterialProperty _EmissionTexVelocity)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Emission",
            "Uses keyword 'EMISSION_ON'\n" +
            "Color     = '_EmissionColor'\n" +
            "Power     = '_DeepNoisePower'\n" +
            "Scale     = '_DeepNoiseScale'\n" +
            "Velocity     = '_DeepNoiseVelocity'\n" +
            "Texture = '_EmissionTex'"
            ));
            _EmissionOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_EmissionOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_EmissionOn.floatValue) == 1)
            {
                material.EnableKeyword("EMISSION_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Color",
                "Color     = '_EmissionColor'\n" +
                "Texture = '_EmissionTex'"
                ));
                EditorGUI.indentLevel--;

                _EmissionColor.colorValue = EditorGUILayout.ColorField(_EmissionColor.colorValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Scale",
                "Scale     = '_EmissionTexScale'\n" +
                "Texture = '_EmissionTex'"
                ));
                EditorGUI.indentLevel--;

                _EmissionTexScale.floatValue = EditorGUILayout.FloatField(_EmissionTexScale.floatValue);
                EditorGUILayout.EndHorizontal();

                DrawVector2Property(new GUIContent("Velocity", "Velocity = '_EmissionTexVelocity'\nTexture = '_EmissionTex'"), _EmissionTexVelocity, 1);


                EditorGUILayout.EndVertical();
                _EmissionTex.textureValue = (Texture)EditorGUILayout.ObjectField(_EmissionTex.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));

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

        private static void DisplayNormalMap(Material material, MaterialProperty _NormalMapOn, MaterialProperty _NormalMap, MaterialProperty _NormalMapScale, MaterialProperty _NormalMapVelocity)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Normal Map",
            "Uses keyword 'NORMAL_MAP_ON'\n" +
            "Scale     = '_NormalMapScale'\n" +
            "Velocity     = '_NormalMapVelocity'\n" +
            "Texture = '_NormalMap'"
            ));
            _NormalMapOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_NormalMapOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_NormalMapOn.floatValue) == 1)
            {
                material.EnableKeyword("NORMAL_MAP_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Scale",
                "Scale     = '_NormalMapScale'\n" +
                "Texture = '_NormalMap'"
                ));
                EditorGUI.indentLevel--;

                _NormalMapScale.floatValue = EditorGUILayout.FloatField(_NormalMapScale.floatValue);
                EditorGUILayout.EndHorizontal();

                DrawVector2Property(new GUIContent("Velocity", "Velocity = '_NormalMapVelocity'\nTexture = '_NormalMap'"), _NormalMapVelocity, 1);


                EditorGUILayout.EndVertical();
                _NormalMap.textureValue = (Texture)EditorGUILayout.ObjectField(_NormalMap.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("NORMAL_MAP_ON");
            }
        }

        private static void DisplaySecondaryNormalMap(Material material, MaterialProperty _SecondaryNormalMapOn, MaterialProperty _SecondaryNormalMap, MaterialProperty _SecondaryNormalMapScale, MaterialProperty _SecondaryNormalMapVelocity)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Secondary Normal Map",
            "Uses keyword 'SECONDARY_NORMAL_MAP_ON'\n" +
            "Scale     = '_SecondaryNormalMapScale'\n" +
            "Velocity     = '_SecondaryNormalMapVelocity'\n" +
            "Texture = '_SecondaryNormalMap'"
            ));
            _SecondaryNormalMapOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_SecondaryNormalMapOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_SecondaryNormalMapOn.floatValue) == 1)
            {
                material.EnableKeyword("SECONDARY_NORMAL_MAP_ON");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Scale",
                "Scale     = '_SecondaryNormalMapScale'\n" +
                "Texture = '_SecondaryNormalMap'"
                ));
                EditorGUI.indentLevel--;

                _SecondaryNormalMapScale.floatValue = EditorGUILayout.FloatField(_SecondaryNormalMapScale.floatValue);
                EditorGUILayout.EndHorizontal();

                DrawVector2Property(new GUIContent("Velocity", "Velocity = '_SecondaryNormalMapVelocity'\nTexture = '_SecondaryNormalMap'"), _SecondaryNormalMapVelocity, 1);


                EditorGUILayout.EndVertical();
                _SecondaryNormalMap.textureValue = (Texture)EditorGUILayout.ObjectField(_SecondaryNormalMap.textureValue, typeof(Texture), false, GUILayout.Height(50), GUILayout.Width(50));

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                material.DisableKeyword("SECONDARY_NORMAL_MAP_ON");
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

        private static void DisplayCustomLightDirection(MaterialProperty _CustomLightDirectionOn, MaterialProperty _CustomLightDirection)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Custom Light Direction",
            "Uses keyword '_CUSTOMLIGHTDIRECTIONON_ON'\n" +
            "Direction = '_CustomLightDirection'"
            ));
            _CustomLightDirectionOn.floatValue = GUILayout.Toolbar(Mathf.RoundToInt(_CustomLightDirectionOn.floatValue), toolBarOptions);
            EditorGUILayout.EndHorizontal();

            if (Mathf.RoundToInt(_CustomLightDirectionOn.floatValue) == 1)
            {
                EditorGUI.indentLevel++;
                _CustomLightDirection.vectorValue = EditorGUILayout.Vector3Field(new GUIContent("Direction", "Direction = '_CustomLightDirection'"), _CustomLightDirection.vectorValue);
                EditorGUI.indentLevel--;
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
}
