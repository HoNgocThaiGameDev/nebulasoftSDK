#ifndef UNIVERSAL_LIT_PASS
#define UNIVERSAL_LIT_PASS

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "Includes/Universal/UniversalParams.hlsl"
#include "Includes/Utils.hlsl"
#include "Includes/Normals.hlsl"
#include "Includes/Emission.hlsl"
#include "Includes/Toon.hlsl"
#include "Includes/LightColor.hlsl"
#include "Includes/Position.hlsl" 
#include "Includes/Specular.hlsl"
#include "Includes/Rim.hlsl"
#include "Includes/Foliage.hlsl"

    // Decal uniforms
sampler2D _DecalTex;
float4 _DecalSize; // xy = size in world units (X,Z)
float4 _DecalOffset; // xy = offset in world units (X,Z) from object pivot
float _DecalIntensity; // 0..1

    // Runs per every vertex
v2f UniversalVertex(appdata input)
{
    UNITY_SETUP_INSTANCE_ID(input);

        // Structure that passed down to the fragment function
    v2f output = (v2f) 0;

        // Getting world position of the vertex
    output.positionWS = float4(TransformObjectToWorld(input.positionOS.xyz), 1);

        // Storing shadow attenuation value inside positionWS.w if the shadow is per vertex
#ifdef _SHADOWS_VERTEX
        output.positionWS.w = ShadowAtten(output.positionWS);
#endif
        // Applying foliage offset and wind
    input.positionOS -= GetFoliageOffset(output.positionWS.xyz, input.uv);
    
        // Recalculating world position after evaluating curve and foliage;
    output.positionWS.xyz = TransformObjectToWorld(input.positionOS.xyz);

        // Calcualting normals. The result is stored in the last two parameters
    GetWorldNormal(input.normalOS, input.tangentOS, output.normalWS, output.tangentWS);

        // Calculating the direction from camera to the vertex
    output.viewDir = GetViewDir(output.positionWS);

        // Getting clip space position
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

        // Just passing uv through
    output.uv = input.uv;

        // Just passing color through
    output.color = input.color;

        // calculating fog
    output.fogFactor = ComputeFogFactor(output.positionCS.z);

    output.screenPos = ComputeScreenPos(output.positionCS);

    return output;
}

float4 UniversalFragment(v2f input) : COLOR
{
        // Geetting initial plain color
    half4 color = tex2D(_MainTex, input.uv) * _Color * input.color;

        // Calculating final detailed normals
    input.normalWS = GetNormalFrag(input.normalWS, input.tangentWS, input.uv);

        // Getting light color into account (cookies included)
    color *= GetLightColor(input.positionWS);

        // Adding specular reflections
    color += GetSpecular(input.positionWS, input.normalWS, input.viewDir, input.uv);

        // Adding shading with toon
    color *= GetToon(input.positionWS, input.normalWS);

        // Adding rim
    color += GetRim(input.positionWS, input.normalWS, input.viewDir);

    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(input.screenPos.xy / input.screenPos.w);
    half indirectAmbientOcclusion = aoFactor.indirectAmbientOcclusion;
    half directAmbientOcclusion = aoFactor.directAmbientOcclusion;

    float ambientOcclusion = aoFactor.indirectAmbientOcclusion * aoFactor.directAmbientOcclusion;

        // Calculaing shadows
    color.rgb *= GetMainLighting(input.positionWS.xyz, input.screenPos, ambientOcclusion); //GetShadows(input.positionWS, input.screenPos);
    color.rgb += GetAdditionalLighting(input.positionWS.xyz, input.screenPos, ambientOcclusion).rgb;

        // Applying emission
    color.rgb += GetEmmision(input.uv).rgb;

        // ===========================
        // Top-projected decal overlay
        // ===========================
        {
            // Early-out if essentially disabled
        if (_DecalIntensity > 0.0001)
        {
                // World-space origin of the object (pivot)
            float3 objectOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));

                // Center of decal in world XZ
            float2 decalCenterWS_XZ = objectOriginWS.xz + _DecalOffset.xy;

                // This pixel's world XZ
            float2 pixelWS_XZ = input.positionWS.xz;

                // Convert to local -0.5..0.5 space based on size, then to 0..1 UV
            float2 halfSize = max(_DecalSize.xy * 0.5, float2(0.0001, 0.0001));
            float2 localXZ = (pixelWS_XZ - decalCenterWS_XZ) / (halfSize * 2.0); // -0.5..0.5
            float2 decalUV = localXZ + 0.5; // 0..1

                // Rectangle mask: 1 inside [0..1], 0 outside
            float2 inside = step(0.0, decalUV) * step(decalUV, 1.0);
            float insideMask = inside.x * inside.y;

                // Fade based on how top-facing the surface is (avoid vertical sides)
            float3 normalWS = normalize(input.normalWS);
            float topMask = saturate(dot(normalWS, float3(0.0, 1.0, 0.0)));

                // Sample decal
            float4 decalSample = tex2D(_DecalTex, decalUV);

                // Final alpha for blending
            float decalAlpha = decalSample.a * _DecalIntensity * insideMask * topMask;

                // Blend decal color on top of final lit color
            color.rgb = lerp(color.rgb, decalSample.rgb, decalAlpha);
        }
    }

        // Applying fog
    color.rgb = MixFog(color.rgb, InitializeInputDataFog(float4(input.positionWS.xyz, 1.0), input.fogFactor));

    return color;
}

#endif