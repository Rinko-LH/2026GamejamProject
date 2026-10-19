Shader "RoomDemo/Sprite Projected Shadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("精灵纹理", 2D) = "white" {}
        _Color ("阴影颜色", Color) = (0.18,0.15,0.2,0.2)
        _BlurUVX ("横向模糊采样", Vector) = (0,0,0,0)
        _BlurUVY ("纵向模糊采样", Vector) = (0,0,0,0)
        _UVBounds ("精灵纹理边界", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _BlurUVX;
                float4 _BlurUVY;
                float4 _UVBounds;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            half SampleAlpha(float2 uv)
            {
                float inside = step(_UVBounds.x, uv.x) * step(_UVBounds.y, uv.y)
                    * step(uv.x, _UVBounds.z) * step(uv.y, _UVBounds.w);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * inside;
            }
            half4 frag(Varyings input) : SV_Target
            {
                // 5×5 高斯近似：柔化轮廓与原图中的细小透明纹理。
                static const float weights[5] = { 1.0, 4.0, 6.0, 4.0, 1.0 };
                half alpha = 0;
                [unroll] for (int y = 0; y < 5; y++)
                    [unroll] for (int x = 0; x < 5; x++)
                        alpha += SampleAlpha(input.uv + _BlurUVX.xy * ((x - 2) * 0.5)
                            + _BlurUVY.xy * ((y - 2) * 0.5)) * (weights[x] * weights[y] / 256.0);
                alpha *= _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
