#ifndef UNIVERSAL_LIT_PARAMS
#define UNIVERSAL_LIT_PARAMS

CBUFFER_START(UnityPerMaterial)

    half4 _Color;
    sampler2D _MainTex;

    half4 _EmissionColor;
    sampler2D _EmissionTex;

    sampler2D _FoliageMask;

    float _FoliageHeightDisplacementMultiplier;
    float _MaxTargetDistance;

    sampler2D _WindTex;
    half2 _WindTexSize;
    half2 _WindVelocity;
    half _WindPower;

    half _LightColorOn;
    half _LightColorInfluence;

    half _CustomLightDirectionOn;
	half3 _CustomLightDirection;

    half4 _RimColor;
    half _RimMin;
    half _RimMax;
    half _DirRim;

    sampler2D _SpecularMap;
    half _Specular;
    half _Shiness;
    half4 _SpecularMin;
    half4 _SpecularMax;

    sampler2D _RampTex;
    half4 _RampMin;
    half4 _RampMax;

    sampler2D _NormalMap;
    half4 _SColor;

    half _UseGlobalMultipliers;

    half4 _OutlineColor;
    half _OutlineWidth;
    half _OutlineScale;
    half _OutlineDepthOffset;
    half _CameraDistanceImpact;

CBUFFER_END

#endif