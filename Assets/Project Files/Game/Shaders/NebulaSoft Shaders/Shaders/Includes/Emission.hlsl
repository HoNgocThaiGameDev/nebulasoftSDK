#ifndef W_EMISSION
#define W_EMISSION

    float4 GetEmmision(float2 uv)
    {
#ifdef EMISSION_ON
        return tex2D(_EmissionTex, uv) * _EmissionColor;
#endif
        return 0;
    }

#endif