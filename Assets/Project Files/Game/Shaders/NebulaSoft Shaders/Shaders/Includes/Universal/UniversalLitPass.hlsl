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

    // Runs per every vertex
    v2f UniversalVertex(appdata input) 
    {
        UNITY_SETUP_INSTANCE_ID(input);

        // Structure that passed down to the fragment function
        v2f output = (v2f)0;

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
        color.rgb *= GetMainLighting(input.positionWS.xyz, input.screenPos, ambientOcclusion);//GetShadows(input.positionWS, input.screenPos);
        color.rgb += GetAdditionalLighting(input.positionWS.xyz, input.screenPos, ambientOcclusion).rgb;

        // Applying emission
        color.rgb += GetEmmision(input.uv).rgb;

        // Applying fog
        color.rgb = MixFog(color.rgb, InitializeInputDataFog(float4(input.positionWS.xyz, 1.0), input.fogFactor));

        return color;
    }

#endif