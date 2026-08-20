#ifndef W_RIM
#define W_RIM

#ifdef RIM_ON
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Includes/Lighting.hlsl"
    #include "Includes/RimLighting.hlsl"
#endif

uniform half _RimIntensity;

    float4 GetRim(float4 positionWS, float3 normalWS, float3 viewDir)
    {
#ifdef RIM_ON

        half4 dirRim = half4(RimDirLighting(_RimMin, _RimMax, _RimColor, viewDir, normalWS, positionWS.xyz), 0);
        half4 rim = half4(RimLighting(_RimMin, _RimMax, _RimColor, viewDir, normalWS), 0);

        half4 realRim = lerp(rim, dirRim, _DirRim);

#ifdef USE_GLOBAL_MULTIPLIERS
        return lerp(0, realRim, _RimIntensity);
#else
        return realRim;
#endif

#endif
        return 0;
    }

#endif