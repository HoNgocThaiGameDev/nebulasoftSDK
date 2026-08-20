#ifndef NEBULASOFT_LIGHTING
#define NEBULASOFT_LIGHTING

float3 ShadowAtten(float3 positionWS)
{
	float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
	Light mainLight = GetMainLight(shadowCoord);

	float atten = mainLight.shadowAttenuation;

	int count = GetAdditionalLightsCount();

	for(int i = 0; i < count; i++)
	{
		Light additionalLight = GetAdditionalLight(i, positionWS);
		
		atten += AdditionalLightRealtimeShadow(i, positionWS, additionalLight.direction) * additionalLight.distanceAttenuation;
	}

	return atten;
}

half3 LightColor(half3 positionWS)
{
	half4 shadowCoord = TransformWorldToShadowCoord(positionWS);
	Light mainLight = GetMainLight(shadowCoord, positionWS, 1);

	half3 color = mainLight.color;

	int count = GetAdditionalLightsCount();

	for(int i = 0; i < count; i++)
	{
		Light additionalLight = GetAdditionalLight(i, positionWS);

		color += additionalLight.color * additionalLight.distanceAttenuation;
	}

	return color;
}

half3 LightDirection(half3 positionWS) {
	half4 shadowCoord = TransformWorldToShadowCoord(positionWS);
	Light mainLight = GetMainLight(shadowCoord);

	return _CustomLightDirectionOn ? normalize(_CustomLightDirection) : mainLight.direction;
}

float3 LightColor(float3 positionWS)
{
	float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
	Light mainLight = GetMainLight(shadowCoord, positionWS, 1);

	float3 color = mainLight.color;

	int count = GetAdditionalLightsCount();

	for(int i = 0; i < count; i++)
	{
		Light additionalLight = GetAdditionalLight(i, positionWS);

		color += additionalLight.color * additionalLight.distanceAttenuation;
	}

	return color;
}

float3 LightDirection(float3 positionWS) {
	float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
	Light mainLight = GetMainLight(shadowCoord);

	return _CustomLightDirectionOn ? normalize(_CustomLightDirection) : mainLight.direction;
}

#endif
