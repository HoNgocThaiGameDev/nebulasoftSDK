#ifndef W_TOON
#define W_TOON

#ifdef TOON_ON
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Includes/Lighting.hlsl"    
#endif

    float4 GetToon(float4 positionWS, float3 normalWS)
    {
#ifdef TOON_ON
        half3 lightDir = LightDirection(positionWS.xyz);
        half tempToon = Remap(dot(normalWS, lightDir), half2(-1, 1), half2(0, 1));

        half3 rampTemp =  lerp(_RampMin, _RampMax, tempToon).rgb;
        half3 rampTextTemp = tex2D(_RampTex, half2(tempToon, 0.5)).rgb;

        half3 toon = rampTemp * rampTextTemp;

       return half4(toon, 1);
#endif
        return 1;
    }

#endif