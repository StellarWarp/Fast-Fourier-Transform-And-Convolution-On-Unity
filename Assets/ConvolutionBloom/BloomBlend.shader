Shader "ConvolutionBloom/Blend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FFT_EXTEND ("FFT EXTEND", Vector) = (0.1, 0.1,0,0)
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        
        Blend One One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _FFT_EXTEND;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            half4 frag(v2f i) : SV_Target
            {
                float2 fft_extend = _FFT_EXTEND.xy;
                float2 uv = (i.uv + fft_extend) * (1 - 2 * fft_extend);
                half4 col = tex2D(_MainTex, uv);
                return col;
            }
            ENDHLSL
        }
    }
}