#ifndef W_FOLIAGE
#define W_FOLIAGE

#ifdef FOLIAGE_ON
    uniform float3 _FoliagePositions[10];
    uniform int _FoliagePositionsCount;

    uniform half _WeatherWindMultiplier = 1;
#endif


    float4 GetFoliageOffset(float3 positionWS, float2 uv) 
    {
#ifdef FOLIAGE_ON
        float3 objPos = GetAbsolutePositionWS(UNITY_MATRIX_M._m03_m13_m23);
        objPos.y = 0;

        float2 mask = tex2Dlod(_FoliageMask, float4(uv, 0, 0)).rg;

        float3 value;
        for (int i = 0; i < _FoliagePositionsCount; i++) {
            float3 targetPos = _FoliagePositions[i];
            targetPos.y = 0;

            float3 sub = targetPos - objPos;

            value += saturate(1 - length(sub) / _MaxTargetDistance) * TransformWorldToObjectDir(sub) * mask.g;
        }

        if (length(value) > 1) value = normalize(value);

#ifdef WIND_ON
        half4 windUV = half4(positionWS.xz / _WindTexSize + (_Time.y * _WindVelocity.xy) % 1, 0, 0);
        half windPower = tex2Dlod(_WindTex, windUV).r * _WindPower;

#ifdef USE_GLOBAL_MULTIPLIERS
        float3 wind = TransformWorldToObjectDir(normalize(float3(_WindVelocity.x, 0, _WindVelocity.y))) * windPower * mask.r * _WeatherWindMultiplier;
#else
        float3 wind = TransformWorldToObjectDir(normalize(float3(_WindVelocity.x, 0, _WindVelocity.y))) * windPower * mask.r;
#endif
        value += wind;
#endif

        return float4(value, 0) * _FoliageHeightDisplacementMultiplier;
#endif
        return 0;
    }


#endif