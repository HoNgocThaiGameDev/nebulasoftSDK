#ifndef UNIVERSAL_DEPTH_PASS
#define UNIVERSAL_DEPTH_PASS

	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	#include "Includes/Universal/UniversalParams.hlsl"
    #include "Includes/Foliage.hlsl"

    v2f DepthOnlyVertex1(appdata input)
    {
        v2f output = (v2f)0;

        float4 positionWS = float4(TransformObjectToWorld(input.position.xyz), 1);
        input.position -= GetFoliageOffset(positionWS.xyz, input.texcoord);

        output.positionCS = TransformObjectToHClip(input.position.xyz);
        return output;
    }

    half DepthOnlyFragment1(v2f input) : SV_TARGET
    {
        return input.positionCS.z;
    }

#endif