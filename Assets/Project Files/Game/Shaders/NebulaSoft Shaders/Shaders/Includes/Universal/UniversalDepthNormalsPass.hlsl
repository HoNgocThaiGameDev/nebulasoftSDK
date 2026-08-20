#ifndef UNIVERSAL_DEPTH_NORMALS_PASS
#define UNIVERSAL_DEPTH_NORMALS_PASS

	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

    #include "Includes/Universal/UniversalParams.hlsl"
    #include "Includes/Foliage.hlsl"

    #if defined(_PARALLAXMAP) && !defined(SHADER_API_GLES)
    #define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
    #endif

    #if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
    #define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
    #endif


struct Attributes1
{
    float4 positionOS     : POSITION;
    float4 tangentOS      : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float3 normal       : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings1
{
    float4 positionCS   : SV_POSITION;
    float2 uv       : TEXCOORD1;
    float3 normalWS     : TEXCOORD2;
    float4 tangentWS    : TEXCOORD3;
    float3 viewDirWS    : TEXCOORD4;
    float3 viewDirTS    : TEXCOORD5;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

    Varyings1 DepthNormalsVertex1(Attributes1 input)
    {
        Varyings1 output = (Varyings1)0;

        float4 positionWS = float4(TransformObjectToWorld(input.positionOS.xyz), 1);
        input.positionOS -= GetFoliageOffset(positionWS.xyz, input.texcoord);
        positionWS.xyz = TransformObjectToWorld(input.positionOS.xyz);

        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

        VertexNormalInputs normalInput = GetVertexNormalInputs(input.normal, input.tangentOS);

        output.normalWS = half3(normalInput.normalWS);
        #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
            float sign = input.tangentOS.w * float(GetOddNegativeScale());
            half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
        #endif

        #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
            output.tangentWS = tangentWS;
        #endif

        #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
            half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS.xyz).xyz;
            half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
            output.viewDirTS = viewDirTS;
        #endif

        return output;
    }

    void DepthNormalsFragment1(
        Varyings1 input
        , out half4 outNormalWS : SV_Target0
    #ifdef _WRITE_RENDERING_LAYERS
        , out float4 outRenderingLayers : SV_Target1
    #endif
    )
    {
        #if defined(LOD_FADE_CROSSFADE)
            LODFadeCrossFade(input.positionCS);
        #endif

        #if defined(_GBUFFER_NORMALS_OCT)
            float3 normalWS = normalize(input.normalWS);
            float2 octNormalWS = PackNormalOctQuadEncode(normalWS);           // values between [-1, +1], must use fp32 on some platforms
            float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);   // values between [ 0,  1]
            half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);      // values between [ 0,  1]
            outNormalWS = half4(packedNormalWS, 0.0);
        #else
            #if defined(_NORMALMAP) || defined(_DETAIL)
                float sgn = input.tangentWS.w;      // should be either +1 or -1
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                float3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);

                #if defined(_DETAIL)
                    half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv).a;
                    float2 detailUv = input.uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                    normalTS = ApplyDetailNormal(detailUv, normalTS, detailMask);
                #endif

                float3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
            #else
                float3 normalWS = input.normalWS;
            #endif

            outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
        #endif

        #ifdef _WRITE_RENDERING_LAYERS
            uint renderingLayers = GetMeshRenderingLayer();
            outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
        #endif
    }

#endif