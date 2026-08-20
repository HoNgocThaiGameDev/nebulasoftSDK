#ifndef W_POSITION
#define W_POSITION

	float4 GetPositionWS(float3 positionOS)
    {
    float4 positionWS = float4(TransformObjectToWorld(positionOS), 1);

#ifdef _SHADOWS_VERTEX
    positionWS.w = ShadowAtten(positionWS);
#endif

    return positionWS;
    }

#endif