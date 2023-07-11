Shader "ConvolutionBloom/ImageKernel"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MaxClamp ("KernelMaxClamp", float) = 5
        _MinClamp ("KernelMinClamp", float) = 1
        _Power ("Power", float) = 1
        _Scaler ("Scaler", float) = 1
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
            float _Power;
            float _Scaler;

            half4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                uv.x = fmod(uv.x + 0.5,1.0);
                uv.y = fmod(uv.y + 0.5,1.0);
                half4 col = tex2D(_MainTex, uv);
                
                float dis = length(i.uv - 0.5);
                float scale = pow(dis,_Power);
                scale =  clamp(scale,_MinClamp,_MaxClamp)/_Scaler;
               
                col = col * scale;
                
                return col;
            }
            ENDHLSL
        }
    }
}
