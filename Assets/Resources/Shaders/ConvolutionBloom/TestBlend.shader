Shader "Unlit/TestBlend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DrawColor ("Draw Color", Color) = (1,1,1,1)
        _DrawPoint ("Draw Point", Vector) = (0,0,0,0)
        _LastDrawPoint ("Last Draw Point", Vector) = (0,0,0,0)
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
            float4 _DrawColor;
            float4 _DrawPoint;
            float4 _LastDrawPoint;

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // half4 col = tex2D(_MainTex, uv);
                float2 p0 = _LastDrawPoint.xy;
                float2 p1 = _DrawPoint.xy;

                //distance from uv to line p0-p1
                float2 vec0 = p1 - p0;
                float2 vec1 = uv - p0;
                float dist = length(vec0) < 0.0001 ?
                    length(p1) :
                    abs(vec0.x * vec1.y - vec0.y * vec1.x) / length(vec0);
                float prj = dot(vec0, vec1);
                if(prj < 0) dist = length(vec1);
                if(prj >= dot(vec0,vec0)) dist = length(uv - p1);
                // float2 px = length(vec0) == 0 ? p1 : p0 + dot(vec0, vec1) / dot(vec0, vec0) * vec0;
                // float dist = length(uv - px);
                // // if(length(vec0)) dist = length(uv - p0);
                // // float dist = min(length(uv - p0), length(uv - p1));

                float brushSize = _DrawPoint.z;
                float factor = exp(-dist * dist / (brushSize * brushSize));
                half4 col = _DrawPoint.w ? _DrawColor * factor : half4(0, 0, 0, 0);
                return col;
            }
            ENDHLSL
        }
    }
}