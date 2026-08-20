#ifndef NEBULASOFT_RIM_LIGHTING
#define NEBULASOFT_RIM_LIGHTING

half3 RimLighting(half rimMin, half rimMax, half4 rimColor, half3 viewDirection, half3 worldNormal) {
	half3 viewDir = normalize(viewDirection);
	half rimTemp = smoothstep(rimMin, rimMax, 1 - saturate(dot(viewDir, worldNormal)));

	return clamp(rimColor.rgb * rimColor.a * rimTemp, half3(0, 0, 0), half3(100, 100, 100));
}

half3 RimDirLighting(half rimMin, half rimMax, half4 rimColor, half3 viewDirection, half3 worldNormal, half3 worldPos) {
	half3 viewDir = normalize(viewDirection);
	half rimTemp = smoothstep(rimMin, rimMax, 1 - saturate(dot(viewDir, worldNormal)));

	half3 lightDir = LightDirection(worldPos);

	return clamp(rimColor.rgb * rimColor.a * rimTemp, half3(0, 0, 0), half3(100, 100, 100)) * (saturate(dot(lightDir, worldNormal)));
}

float3 RimLighting(float rimMin, float rimMax, float4 rimColor, float3 viewDirection, float3 worldNormal) {
	float3 viewDir = normalize(viewDirection);
	float rimTemp = smoothstep(rimMin, rimMax, 1 - saturate(dot(viewDir, worldNormal)));

	return clamp(rimColor.rgb * rimColor.a * rimTemp, float3(0, 0, 0), float3(100, 100, 100));
}

#endif
