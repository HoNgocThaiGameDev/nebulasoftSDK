#ifndef UNIVERSAL_DEPTH_INIT
#define UNIVERSAL_DEPTH_INIT
	#pragma target 2.0

    // -------------------------------------
    // Shader Stages
    #pragma vertex DepthOnlyVertex1
    #pragma fragment DepthOnlyFragment1

    // -------------------------------------
    // Material Keywords
    #pragma shader_feature_local _ALPHATEST_ON
    #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

    // -------------------------------------
    // Unity defined keywords
    #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

    #pragma shader_feature_local EMISSION_ON
    #pragma shader_feature_local SPECULAR_ON
    #pragma shader_feature_local RIM_ON
    #pragma shader_feature_local _SHADOWS_NONE _SHADOWS_VERTEX _SHADOWS_PIXEL
    #pragma shader_feature_local TOON_ON
    #pragma shader_feature_local FOLIAGE_ON
    #pragma shader_feature_local WIND_ON
    #pragma shader_feature_local USE_GLOBAL_MULTIPLIERS

    struct appdata
    {
        float4 position     : POSITION;
        float2 texcoord     : TEXCOORD0;
    };

    struct v2f
    {
        #if defined(_ALPHATEST_ON)
            float2 uv       : TEXCOORD0;
        #endif
        float4 positionCS   : SV_POSITION;
    };

#endif