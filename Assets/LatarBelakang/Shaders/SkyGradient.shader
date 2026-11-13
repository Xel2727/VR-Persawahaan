// Skybox gradient sederhana untuk latar sawah (murah untuk VR, 1 draw call)
Shader "LatarBelakang/SkyGradient"
{
    Properties
    {
        _MainTex ("Gradient", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Exposure ("Exposure", Float) = 1
        _Horizon ("Horizon Sharpness", Range(0.5, 4)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Tint;
            float _Exposure;
            float _Horizon;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                // baris bawah tekstur = putih (horizon), baris atas = biru tua (zenith)
                float2 uv = float2(0.5, saturate(d.y * 1.05));
                fixed4 c = tex2D(_MainTex, uv) * _Tint;
                c.rgb *= _Exposure;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
