#ifndef UNIVERSAL_SHADOWS_INIT
#define UNIVERSAL_SHADOWS_INIT

	#pragma prefer_hlslcc gles
	#pragma exclude_renderers d3d11_9x
	#pragma target 2.0

	// -------------------------------------
	// Material Keywords
	#pragma shader_feature _ALPHATEST_ON

	//--------------------------------------
	// GPU Instancing
	#pragma multi_compile_instancing
	#pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

	#pragma shader_feature_local EMISSION_ON
	#pragma shader_feature_local SPECULAR_ON
	#pragma shader_feature_local RIM_ON
	#pragma shader_feature_local _SHADOWS_NONE _SHADOWS_VERTEX _SHADOWS_PIXEL
	#pragma shader_feature_local TOON_ON
	#pragma shader_feature_local FOLIAGE_ON
	#pragma shader_feature_local WIND_ON
	#pragma shader_feature_local USE_GLOBAL_MULTIPLIERS

	#pragma vertex shadowVert
	#pragma fragment shadowFrag

	struct appdata
    {
        float4 positionOS   : POSITION;
        float3 normalOS     : NORMAL;
        float2 texcoord     : TEXCOORD0;
    };

    struct v2f
    {
        #if defined(_ALPHATEST_ON)
            float2 uv       : TEXCOORD0;
        #endif
        float4 positionCS   : SV_POSITION;
    };

    #define W_SHADOW

#endif