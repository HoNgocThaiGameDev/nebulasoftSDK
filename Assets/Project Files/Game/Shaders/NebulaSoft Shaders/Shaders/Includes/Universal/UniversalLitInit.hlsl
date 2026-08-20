#ifndef UNIVERSAL_LIT_INIT
#define UNIVERSAL_LIT_INIT

// Keywords for different features
    #pragma shader_feature_local EMISSION_ON
    #pragma shader_feature_local SPECULAR_ON
    #pragma shader_feature_local RIM_ON
    #pragma shader_feature_local _SHADOWS_NONE _SHADOWS_VERTEX _SHADOWS_PIXEL
    #pragma shader_feature_local TOON_ON
    #pragma shader_feature_local FOLIAGE_ON
    #pragma shader_feature_local WIND_ON
    #pragma shader_feature_local USE_GLOBAL_MULTIPLIERS
    #pragma multi_compile_fog

    #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION 

#ifndef _SHADOWS_NONE
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
    #pragma multi_compile _ _SHADOWS_SOFT
    #pragma multi_compile _LIGHT_COOKIES

    #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
    #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
    #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

    #pragma multi_compile _ DIRLIGHTMAP_COMBINED
    #pragma multi_compile _ LIGHTMAP_ON
#endif

    #pragma vertex UniversalVertex
    #pragma fragment UniversalFragment

    struct appdata
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;

        float4 color : COLOR0;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
    };

    struct v2f
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float4 color : COLOR0;

        float4 positionWS: TEXCOORD1;

#define WORLD_NORM
        float3 normalWS : TEXCOORD2;
        float4 tangentWS : TEXCOORD3;

#define VIEW_DIR
        float3 viewDir : TEXCOORD4;

        half  fogFactor : TEXCOORD5;

        float4 screenPos: TEXCOORD6;
    };

#endif