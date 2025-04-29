Shader "ConvolutionBloom/PsfRemap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MaxClamp ("KernelMaxClamp", float) = 5
        _MinClamp ("KernelMinClamp", float) = 1
        _FFT_EXTEND ("FFT EXTEND", Vector) = (0.1, 0.1,0,0)
        _ScreenX ("Screen X", Int) = 0
        _ScreenY ("Screen Y", Int) = 0
        _Power ("Power", float) = 1
        _Scaler ("Scaler", float) = 1
        _GrayScale ("GrayScale", int) = 0
        _EnableRemap ("EnableRemap", int) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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

            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float _MaxClamp;
            float _MinClamp;
            float4 _FFT_EXTEND;
            int _ScreenX;
            int _ScreenY;
            float _Power;
            float _Scaler;
            int _GrayScale;
            int _EnableRemap;

            float luminance(float3 col)
            {
                return dot(col, float3(0.299, 0.587, 0.114));
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                uv.x = fmod(uv.x + 0.5,1.0);
                uv.y = fmod(uv.y + 0.5,1.0);
                float2 fft_extend = _FFT_EXTEND.xy;
                float2 img_map_size = 1 - 2 * fft_extend;
                float2 screen_size = float2(_ScreenX,_ScreenY);
                float2 kenrel_map_size = sqrt(_ScreenX*_ScreenY) * img_map_size / screen_size;
                uv = (uv - (1 - kenrel_map_size) * 0.5)/kenrel_map_size;
                // uv = (uv-0.5)*0.5+0.5;
                half4 col;
                if(uv.x > 1 || uv.y > 1 || uv.x < 0 || uv.y < 0)
                    col = 0;
                else
                    col = tex2D(_MainTex, uv);
                
                float scale = _Scaler;
                // if(_EnableRemap)
                // {
                //     float2 dst = float2(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));
                //     scale = pow(1-length(dst),_Power);
                //     scale = clamp(scale,_MinClamp,_MaxClamp)/_Scaler;
                // }
                // float scale = 1/_Scaler;
                if (_GrayScale)
                {
                    return half4(clamp(luminance(col.rgb)*scale,_MinClamp,_MaxClamp),0,0,0);
                }
               
                col = col * scale;
                
                return clamp(col,_MinClamp,_MaxClamp);
            }
            ENDHLSL
        }
    }
}
