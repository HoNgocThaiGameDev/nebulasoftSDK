#ifndef UNIVERSAL_OUTLINE_INIT
#define UNIVERSAL_OUTLINE_INIT

// Keywords for different features
    #pragma multi_compile OUTLINE_ON
    #pragma multi_compile_fog

    #pragma shader_feature_local EMISSION_ON
    #pragma shader_feature_local SPECULAR_ON
    #pragma shader_feature_local RIM_ON
    #pragma shader_feature_local _SHADOWS_NONE _SHADOWS_VERTEX _SHADOWS_PIXEL
    #pragma shader_feature_local TOON_ON
    #pragma shader_feature_local FOLIAGE_ON
    #pragma shader_feature_local WIND_ON
    #pragma shader_feature_local USE_GLOBAL_MULTIPLIERS

    #pragma vertex OutlineVertex
    #pragma fragment OutlineFragment

    struct appdata
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 positionCS : SV_POSITION;
        float3 normalCS : NORMAL;

        float fogCoord : TEXCOORD1;
    };

#endif