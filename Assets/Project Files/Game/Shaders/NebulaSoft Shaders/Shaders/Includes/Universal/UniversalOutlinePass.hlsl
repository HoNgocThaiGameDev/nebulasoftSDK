#ifndef UNIVERSAL_OUTLINE_PASS
#define UNIVERSAL_OUTLINE_PASS

	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

    #include "Includes/Universal/UniversalParams.hlsl"
	#include "Includes/Foliage.hlsl"

	v2f OutlineVertex(appdata input)
    {
        // Structure that passed down to the fragment function
        v2f output = (v2f)0;

        // Getting world position of the vertex
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

        // Applying foliage offset and wind
        input.positionOS -= GetFoliageOffset(positionWS, input.uv);

#if defined(OUTLINE_ON)
        float4 clipPosition = TransformObjectToHClip(input.positionOS.xyz * _OutlineScale);
        const float3 clipNormal = mul((float3x3)UNITY_MATRIX_VP, mul((float3x3)UNITY_MATRIX_M, input.normalOS));
        const half outlineWidth = _OutlineWidth;
        const half cameraDistanceImpact = lerp(clipPosition.w, 4.0, _CameraDistanceImpact);
        const float2 aspectRatio = float2(_ScreenParams.x / _ScreenParams.y, 1);
        const float2 offset = normalize(clipNormal.xy) / aspectRatio * outlineWidth * cameraDistanceImpact * 0.005;
        clipPosition.xy += offset;
        const half outlineDepthOffset = _OutlineDepthOffset;

#if UNITY_REVERSED_Z
        clipPosition.z -= outlineDepthOffset * 0.1;
#else
        clipPosition.z += outlineDepthOffset * 0.1 * (1.0 - UNITY_NEAR_CLIP_VALUE);
#endif
        output.positionCS = clipPosition;
        output.normalCS = clipNormal;

        output.fogCoord = ComputeFogFactor(output.positionCS.z);
#endif
        return output;
    }

    half4 OutlineFragment(v2f i) : SV_TARGET
    {
        half4 color = _OutlineColor;
        color.rgb = MixFog(color.rgb, i.fogCoord);
        return color;
    }

#endif