#ifndef W_SPECULAR
#define W_SPECULAR

#ifdef SPECULAR_ON
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Includes/Lighting.hlsl"
#endif

float3 GetViewDir(float4 positionWS)
    {
#ifdef VIEW_DIR
        return GetWorldSpaceViewDir(positionWS.xyz);
#endif
        return 0;
    }

    float4 GetSpecular(float4 positionWS, float3 normalWS, float3 viewDir, float2 uv)
    {
#ifdef SPECULAR_ON
        
        float3 lightDir = LightDirection(positionWS.xyz);

        float3 lightReflectDirection = reflect(-lightDir, normalWS);
        float lightSeeDirection = saturate(dot(lightReflectDirection, normalize(viewDir)));

        if(lightSeeDirection < _Specular) 
        {
            lightSeeDirection = 0; 
        } 
        else 
        {
            lightSeeDirection = Remap(lightSeeDirection, float2(_Specular, 1), float2(0, 1));
        }

        float shininessPower = length(pow(abs(lightSeeDirection), _Shiness));

        float4 specColor = lerp(_SpecularMin, _SpecularMax, lightSeeDirection);

        float3 specularReflection = specColor.rgb * shininessPower * specColor.a;

        float4 specularIntensity = tex2D(_SpecularMap, uv);

        return float4(specularReflection, 0) * specularIntensity;
#endif
        return 0;
    }

#endif