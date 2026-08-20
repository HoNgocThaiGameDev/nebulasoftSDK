#ifndef W_NORMALS
#define W_NORMALS

    void GetWorldNormal(float3 normalOS, float4 tangentOS, out float3 normalWS, out float4 tangentWS )
    {
        normalWS = 0;
        tangentWS = 0;
#ifdef WORLD_NORM
        normalWS = TransformObjectToWorldNormal(normalOS);
        float sign = tangentOS.w * GetOddNegativeScale();
		tangentWS = float4(TransformObjectToWorldDir(tangentOS.xyz), sign);

		normalWS = normalize(normalWS);
		tangentWS = normalize(tangentWS);
#endif
    }

    float3 UnpackNormal(sampler2D map, float2 uv)
    {
        float4 packednormal = tex2D(map, uv);
        float3 tangentNormal = 0;

#if defined(UNITY_NO_DXT5nm)
        tangentNormal = normalize(packednormal.xyz * 2 - 1);
#else
        packednormal.x *= packednormal.w;
        tangentNormal.xy = (packednormal.xy * 2 - 1);

        tangentNormal.z = sqrt(1.0 - saturate(dot(tangentNormal.xy, tangentNormal.xy)));
#endif

        return tangentNormal;
    }	

    float3 GetNormalFrag(float3 normalWS, float4 tangentWS, float2 uv)
    {
        float3 tangentNormal = UnpackNormal(_NormalMap, uv);
        
        float sgn = tangentWS.w;
        float3 bitangent = sgn * cross(normalWS.xyz, tangentWS.xyz);
        half3x3 tangentToWorld = half3x3(tangentWS.xyz, bitangent.xyz, normalWS.xyz);

        return TransformTangentToWorld(tangentNormal, tangentToWorld);
    }

#endif