Shader "NebulaSoft/OutlineCompositeURP"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _Thickness    ("Thickness (pixels)", Range(0.5, 6)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

        Pass
        {
            Name "OutlineComposite"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Bound from script as GLOBAL textures:
            TEXTURE2D(_SceneTex);     SAMPLER(sampler_SceneTex);
            TEXTURE2D(_OutlineMask);  SAMPLER(sampler_OutlineMask);

            float4 _OutlineColor;
            float  _Thickness;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // Fullscreen triangle generated from SV_VertexID
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;

                // 0:(-1,-1)  1:(-1,3)  2:(3,-1)
                float2 pos = float2((vertexID == 2) ? 3.0 : -1.0,
                                    (vertexID == 1) ? 3.0 : -1.0);

                o.positionCS = float4(pos, 0.0, 1.0);
                o.uv = pos * 0.5 + 0.5;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // Flip UVs for BOTH scene + mask in this pipeline
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif

                half4 col = SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, uv);

                float center = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, uv).r;

                float2 pixelSize = _Thickness / _ScreenParams.xy;

                float mL = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, uv + float2(-pixelSize.x, 0)).r;
                float mR = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, uv + float2( pixelSize.x, 0)).r;
                float mU = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, uv + float2(0,  pixelSize.y)).r;
                float mD = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, uv + float2(0, -pixelSize.y)).r;

                float maxN = max(max(mL, mR), max(mU, mD));

                // Outer outline only
                float edge = saturate(maxN - center);

                col.rgb = lerp(col.rgb, _OutlineColor.rgb, edge * _OutlineColor.a);
                col.a = 1;

                return col;
            }
            ENDHLSL
        }
    }
}
