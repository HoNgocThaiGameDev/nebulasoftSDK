#ifndef UNIVERSAL_DEPTH_NORMALS_INIT
#define UNIVERSAL_DEPTH_NORMALS_INIT
	
	#pragma target 2.0

    // -------------------------------------
    // Shader Stages
    #pragma vertex DepthNormalsVertex1
    #pragma fragment DepthNormalsFragment1

    // -------------------------------------
    // Material Keywords
    #pragma shader_feature_local _NORMALMAP
    #pragma shader_feature_local _PARALLAXMAP
    #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
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

    struct Attributes
    {
        float4 positionOS   : POSITION;
        float4 tangentOS    : TANGENT;
        float2 texcoord     : TEXCOORD0;
        float3 normal       : NORMAL;
    };

    struct Varyings
    {
        float4 positionCS  : SV_POSITION;
        #if defined(REQUIRES_UV_INTERPOLATOR)
        float2 uv          : TEXCOORD1;
        #endif
        half3 normalWS     : TEXCOORD2;

        #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        half4 tangentWS    : TEXCOORD4;    // xyz: tangent, w: sign
        #endif

        half3 viewDirWS    : TEXCOORD5;

        #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        half3 viewDirTS    : TEXCOORD8;
        #endif
    };

#endif