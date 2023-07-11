Shader "ConvolutionBloom/Filter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FFT_EXTEND ("FFT EXTEND", Vector) = (0.1, 0.1,0,0)
        _THRESHOLD ("Threshlod", float)  = 10
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        Blend One Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float4 _FFT_EXTEND;
            float _THRESHOLD;

            float luminance(float3 col)
            {
                return 0.299 * col.x + 0.587 * col.y + 0.114 * col.z;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 fft_extend = _FFT_EXTEND.xy;
                float2 uv = i.uv/(1-2*fft_extend) - fft_extend;
                half4 col = tex2D(_MainTex, uv);
                if(uv.x > 1 || uv.y>1 || uv.x <0 || uv.y <0) col = 0;
                float luma = luminance(col.xyz);
                col *= pow(luma,_THRESHOLD);
                return col;
            }
            ENDHLSL
        }
    }
}
