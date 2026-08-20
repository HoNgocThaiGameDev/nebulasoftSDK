Shader "NebulaSoft/Particles"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _BumpMap("Normal Map", 2D) = "bump" {}
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        // -------------------------------------
        // Particle specific
        _SoftParticlesNearFadeDistance("Soft Particles Near Fade", Float) = 0.0
        _SoftParticlesFarFadeDistance("Soft Particles Far Fade", Float) = 1.0
        _CameraNearFadeDistance("Camera Near Fade", Float) = 1.0
        _CameraFarFadeDistance("Camera Far Fade", Float) = 2.0
        _DistortionBlend("Distortion Blend", Range(0.0, 1.0)) = 0.5
        _DistortionStrength("Distortion Strength", Float) = 1.0

        // -------------------------------------
        // Hidden properties - Generic
        _Surface("__surface", Float) = 0.0
        _Blend("__mode", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        // Particle specific
        _ColorMode("_ColorMode", Float) = 0.0
        [HideInInspector] _BaseColorAddSubDiff("_ColorMode", Vector) = (0,0,0,0)
        [ToggleOff] _FlipbookBlending("__flipbookblending", Float) = 0.0
        [ToggleUI] _SoftParticlesEnabled("__softparticlesenabled", Float) = 0.0
        [ToggleUI] _CameraFadingEnabled("__camerafadingenabled", Float) = 0.0
        [ToggleUI] _DistortionEnabled("__distortionenabled", Float) = 0.0
        [HideInInspector] _SoftParticleFadeParams("__softparticlefadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _CameraFadeParams("__camerafadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _DistortionStrengthScaled("Distortion Strength Scaled", Float) = 0.1

        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _FlipbookMode("flipbook", Float) = 0
        [HideInInspector] _Mode("mode", Float) = 0
        [HideInInspector] _Color("color", Color) = (1,1,1,1)

        _LightColorOn("Uses Light Color", float) = 0
        _LightColorInfluence("LightColorinfluence", Range(0, 1)) = 1
    }

    HLSLINCLUDE

    //Particle shaders rely on "write" to CB syntax which is not supported by DXC
    #pragma never_use_dxc

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "PerformanceChecks" = "False"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ------------------------------------------------------------------
        //  Forward pass.
        Pass
        {
            Name "ForwardLit"

            // -------------------------------------
            // Render State Commands
            BlendOp[_BlendOp]
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION

            // -------------------------------------
            // Particle Keywords
            #pragma shader_feature_local _FLIPBOOKBLENDING_ON
            #pragma shader_feature_local _SOFTPARTICLES_ON
            #pragma shader_feature_local _FADING_ON
            #pragma shader_feature_local _DISTORTION_ON
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _ _COLOROVERLAY_ON _COLORCOLOR_ON _COLORADDSUBDIFF_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #pragma instancing_options procedural:ParticleInstancingSetup

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitForwardPass.hlsl"

            half _CustomLightDirectionOn;
		    half3 _CustomLightDirection;

            half4 _Color;
            half _LightColorOn;
            half _LightColorInfluence;
            half4 _SColor;

            sampler2D _SpecularMap;
            half _Specular;
            half _Shiness;
            half4 _SpecularMin;
            half4 _SpecularMax;

            #define PARTICLES;

            #include "Includes/LightColor.hlsl"

            VaryingsParticle vert(AttributesParticle input)
            {
                VaryingsParticle output = (VaryingsParticle)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                half fogFactor = 0.0;
            #if !defined(_FOG_FRAGMENT)
                fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
            #endif

                // position ws is used to compute eye depth in vertFading
                output.positionWS.xyz = vertexInput.positionWS;
                output.positionWS.w = fogFactor;
                output.clipPos = TransformObjectToHClip(input.positionOS);
                output.color = GetParticleColor(input.color);

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

            #ifdef _NORMALMAP
                output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
                output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
                output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
            #else
                output.normalWS = half3(normalInput.normalWS);
                output.viewDirWS = viewDirWS;
            #endif

            #if defined(_FLIPBOOKBLENDING_ON)
            #if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords.xyxy, 0.0);
            #else
                GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords, input.texcoordBlend);
            #endif
            #else
                GetParticleTexcoords(output.texcoord, input.texcoords.xy);
            #endif

            #if defined(_SOFTPARTICLES_ON) || defined(_FADING_ON) || defined(_DISTORTION_ON)
                output.projectedPosition = vertexInput.positionNDC;
            #endif

                return output;
            }

            half4 frag(VaryingsParticle input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                ParticleParams particleParams;
                InitParticleParams(input, particleParams);

                SurfaceData surfaceData;
                InitializeSurfaceData(particleParams, surfaceData);
                InputData inputData;
                InitializeInputData(input, surfaceData, inputData);

                half4 finalColor = UniversalFragmentUnlit(inputData, surfaceData);

                finalColor *= GetLightColor(input.positionWS);

                #if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
                    float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.clipPos);
                    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);
                    finalColor.rgb *= aoFactor.directAmbientOcclusion;
                #endif

                finalColor.rgb = MixFog(finalColor.rgb, inputData.fogCoord);
                finalColor.a = OutputAlpha(finalColor.a, IsSurfaceTypeTransparent(_Surface));

                return finalColor;
            }

            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Depth Only pass.
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ _ALPHATEST_ON
            #pragma shader_feature_local _ _FLIPBOOKBLENDING_ON
            #pragma shader_feature_local_fragment _ _COLOROVERLAY_ON _COLORCOLOR_ON _COLORADDSUBDIFF_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesDepthOnlyPass.hlsl"

            #define PARTICLES;

            VaryingsDepthOnlyParticle vert(AttributesDepthOnlyParticle input)
            {
                VaryingsDepthOnlyParticle output = (VaryingsDepthOnlyParticle)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.vertex.xyz);
                output.clipPos = TransformObjectToHClip(input.vertex);

                #if defined(_ALPHATEST_ON)
                    output.color = GetParticleColor(input.color);

                    #if defined(_FLIPBOOKBLENDING_ON)
                        #if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                            GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords.xyxy, 0.0);
                        #else
                            GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords, input.texcoordBlend);
                        #endif
                    #else
                        GetParticleTexcoords(output.texcoord, input.texcoords.xy);
                    #endif
                #endif

                return output;
            }

            half frag(VaryingsDepthOnlyParticle input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Check if we need to discard...
                #if defined(_ALPHATEST_ON)
                    float2 uv = input.texcoord;
                    half4 vertexColor = input.color;
                    half4 baseColor = _BaseColor;

                    #if defined(_FLIPBOOKBLENDING_ON)
                        float3 blendUv = input.texcoord2AndBlend;
                    #else
                        float3 blendUv = float3(0,0,0);
                    #endif

                    half4 albedo = BlendTexture(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), uv, blendUv) * baseColor;
                    half4 colorAddSubDiff = half4(0, 0, 0, 0);
                    #if defined (_COLORADDSUBDIFF_ON)
                        colorAddSubDiff = _BaseColorAddSubDiff;
                    #endif

                    albedo = MixParticleColor(albedo, vertexColor, colorAddSubDiff);
                    AlphaDiscard(albedo.a, _Cutoff);
                #endif

                return input.clipPos.z;
            }


            ENDHLSL
        }
        // This pass is used when drawing to a _CameraNormalsTexture texture with the forward renderer or the depthNormal prepass with the deferred renderer.
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ _NORMALMAP
            #pragma shader_feature_local _ _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _COLOROVERLAY_ON _COLORCOLOR_ON _COLORADDSUBDIFF_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesDepthNormalsPass.hlsl"

            #define PARTICLES;

            VaryingsDepthNormalsParticle vert(AttributesDepthNormalsParticle input)
            {
                VaryingsDepthNormalsParticle output = (VaryingsDepthNormalsParticle)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.vertex.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normal, input.tangent);

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                #if defined(_NORMALMAP)
                    output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
                    output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
                    output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
                #else
                    output.normalWS = normalInput.normalWS;
                    output.viewDirWS = viewDirWS;
                #endif

                output.clipPos = TransformObjectToHClip(input.vertex);

                #if defined(_ALPHATEST_ON)
                    output.color = GetParticleColor(input.color);
                #endif

                #if defined(_ALPHATEST_ON) || defined(_NORMALMAP)
                    #if defined(_FLIPBOOKBLENDING_ON)
                        #if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                            GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords.xyxy, 0.0);
                        #else
                            GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords, input.texcoordBlend);
                        #endif
                    #else
                        GetParticleTexcoords(output.texcoord, input.texcoords.xy);
                    #endif
                #endif

                return output;
            }

            half4 frag(VaryingsDepthNormalsParticle input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Inputs...
                #if defined(_ALPHATEST_ON) || defined(_NORMALMAP)
                    float2 uv = input.texcoord;

                    #if defined(_FLIPBOOKBLENDING_ON)
                        float3 blendUv = input.texcoord2AndBlend;
                    #else
                        float3 blendUv = float3(0,0,0);
                    #endif
                #endif

                // Check if we need to discard...
                #if defined(_ALPHATEST_ON)
                    half4 vertexColor = input.color;
                    half4 baseColor = _BaseColor;
                    half4 albedo = BlendTexture(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), uv, blendUv) * baseColor;

                    half4 colorAddSubDiff = half4(0, 0, 0, 0);
                    #if defined(_COLORADDSUBDIFF_ON)
                        colorAddSubDiff = _BaseColorAddSubDiff;
                    #endif

                    albedo = MixParticleColor(albedo, vertexColor, colorAddSubDiff);
                    AlphaDiscard(albedo.a, _Cutoff);
                #endif

                // Normals...
                #ifdef _NORMALMAP
                    half3 normalTS = SampleNormalTS(uv, blendUv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
                    float3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz));
                #else
                    float3 normalWS = input.normalWS;
                #endif

                // Output...
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);           // values between [-1, +1], must use fp32 on some platforms
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);   // values between [ 0,  1]
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);      // values between [ 0,  1]
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(normalWS), 0.0);
                #endif
            }


            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Scene view outline pass.
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }

            // -------------------------------------
            // Render State Commands
            BlendOp Add
            Blend One Zero
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #define PARTICLES_EDITOR_META_PASS
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Particle Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _FLIPBOOKBLENDING_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesEditorPass.hlsl"

            #define PARTICLES;

            VaryingsParticle vert(AttributesParticle input)
            {
                VaryingsParticle output = (VaryingsParticle)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                output.clipPos = TransformObjectToHClip(input.positionOS);
                output.color = GetParticleColor(input.color);

            #if defined(_FLIPBOOKBLENDING_ON) && !defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords, input.texcoordBlend);
            #else
                GetParticleTexcoords(output.texcoord, input.texcoords.xy);
            #endif

                return output;
            }

            half4 frag(VaryingsParticle input) : SV_Target
            {
                fragParticleSceneClip(input);
                return float4(_ObjectId, _PassValue, 1, 1);
            }

            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Scene picking buffer pass.
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }

            // -------------------------------------
            // Render State Commands
            BlendOp Add
            Blend One Zero
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #define PARTICLES_EDITOR_META_PASS
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vertParticleEditor
            #pragma fragment fragParticleScenePicking

            // -------------------------------------
            // Particle Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _FLIPBOOKBLENDING_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesEditorPass.hlsl"

            #define PARTICLES;

            VaryingsParticle vert(AttributesParticle input)
            {
                VaryingsParticle output = (VaryingsParticle)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                output.clipPos = TransformObjectToHClip(input.positionOS);
                output.color = GetParticleColor(input.color);

            #if defined(_FLIPBOOKBLENDING_ON) && !defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                GetParticleTexcoords(output.texcoord, output.texcoord2AndBlend, input.texcoords, input.texcoordBlend);
            #else
                GetParticleTexcoords(output.texcoord, input.texcoords.xy);
            #endif

                return output;
            }

            half4 frag(VaryingsParticle input) : SV_Target
            {
                fragParticleSceneClip(input);
                return float4(_ObjectId, _PassValue, 1, 1);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "NebulaSoft.Shader.ParticlesGUI"
}