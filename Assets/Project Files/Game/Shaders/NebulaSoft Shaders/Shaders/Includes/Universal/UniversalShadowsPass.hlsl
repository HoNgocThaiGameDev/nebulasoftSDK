#ifndef UNIVERSAL_SHADOWS_PASS
#define UNIVERSAL_SHADOWS_PASS

    half3 LerpWhiteTo(half3 b, half t)
    {
        half oneMinusT = 1 - t;
        return half3(oneMinusT, oneMinusT, oneMinusT) + b * t;
    }

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

    #include "Includes/Universal/UniversalParams.hlsl"
    #include "Includes/Foliage.hlsl"

    float3 _LightDirection;
    float3 _LightPosition;

    float4 GetShadowPositionHClip(appdata input)
    {
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif

        float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif

        return positionCS;
    }

    v2f shadowVert(appdata input)
    {
        v2f output = (v2f) 0;

        // Getting world position of the vertex
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

        // Applying foliage offset and wind
        input.positionOS -= GetFoliageOffset(positionWS, input.texcoord);
    
        output.positionCS = GetShadowPositionHClip(input);
        return output;
    }

    half4 shadowFrag(v2f input) : SV_TARGET
    {
        #if defined(LOD_FADE_CROSSFADE)
            LODFadeCrossFade(input.positionCS);
        #endif

        return 0;
    }

#endif