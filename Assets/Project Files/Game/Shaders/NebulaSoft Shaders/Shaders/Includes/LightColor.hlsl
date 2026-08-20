#ifndef W_LIGHT_COLOR
#define W_LIGHT_COLOR

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Includes/Lighting.hlsl"

    uniform half _ShadowsIntensity;

#ifndef PARTICLES

#ifndef _SHADOWS_NONE

    float3 GetShadow(float shadowAtten, float ambientOcclusion)
    {
        shadowAtten *= ambientOcclusion;

        half4 shadowTemp = half4(lerp(_Color.rgb, _SColor.rgb, _SColor.a), 1);

#ifdef USE_GLOBAL_MULTIPLIERS
        return lerp(shadowTemp, _Color, lerp(shadowAtten, 1, 1 - _ShadowsIntensity)).xyz;
#else
        return lerp(shadowTemp, _Color, shadowAtten).xyz;
#endif
    }

#endif

    float3 GetMainLighting(float3 positionWS, float4 screenPos, float ambientOcclusion)
	{
		half4 shadowCoord = TransformWorldToShadowCoord(positionWS);
		Light mainLight = GetMainLight(shadowCoord, positionWS, 1);

		half3 lightColor = mainLight.color;
        half lightColorInfluence = _LightColorOn ? _LightColorInfluence : 0;

        lightColor = lerp(1, lightColor, lightColorInfluence);

#ifndef _SHADOWS_NONE
		float shadowAtten = mainLight.shadowAttenuation;
        lightColor *= GetShadow(shadowAtten, ambientOcclusion);
#endif
        return lightColor;
	}

	float3 GetAdditionalLighting(float3 positionWS, float4 screenPos, float ambientOcclusion)
	{
        half4 shadowCoord = TransformWorldToShadowCoord(positionWS);

		half3 color = 0;
        half lightColorInfluence = _LightColorOn ? _LightColorInfluence : 0;

        int count = GetAdditionalLightsCount();
		for(int i = 0; i < count; i++)
		{
			Light additionalLight = GetAdditionalLight(i, positionWS);

			float3 lightColor = lerp(0, additionalLight.color * additionalLight.distanceAttenuation, lightColorInfluence);

#ifndef _SHADOWS_NONE
			float shadowAtten = AdditionalLightRealtimeShadow(i, positionWS, additionalLight.direction) * additionalLight.distanceAttenuation;
            lightColor *= GetShadow(shadowAtten, ambientOcclusion);
#endif

            color += lightColor;
		}

        return color;
	}

    float4 GetShadows(float4 positionWS, float4 screenPos)
    {
#ifndef _SHADOWS_NONE

#ifdef _SHADOWS_PIXEL
        half shadowAtten = ShadowAtten(positionWS.xyz).r;
#elif _SHADOWS_VERTEX
        half shadowAtten = positionWS.w;
#endif

        AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(screenPos.xy / screenPos.w);
        half indirectAmbientOcclusion = aoFactor.indirectAmbientOcclusion;
        half directAmbientOcclusion = aoFactor.directAmbientOcclusion;

        shadowAtten *= indirectAmbientOcclusion * directAmbientOcclusion;

        half4 shadowTemp = half4(lerp(_Color.rgb, _SColor.rgb, _SColor.a), 1);

#ifdef USE_GLOBAL_MULTIPLIERS
        return lerp(shadowTemp, _Color, lerp(shadowAtten, 1, 1 - _ShadowsIntensity));
#else
        return lerp(shadowTemp, _Color, shadowAtten);
#endif
    
#endif
        return 1;
    }

#endif

    float4 GetLightColor(half4 positionWS)
    {
        half4 lightColor = half4(LightColor(positionWS.xyz), 1);
        half influence = _LightColorOn ? _LightColorInfluence : 0;

        lightColor = lerp(1, lightColor, influence);

        return lightColor;
    }

#endif