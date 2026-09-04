Shader "Hidden/WalkToBiome/Sky"
{
    Properties
    {
        _SkyColor ("Sky", Color) = (0.42, 0.66, 0.95, 1)
        _HorizonColor ("Horizon", Color) = (0.74, 0.82, 0.90, 1)
        _GroundColor ("Ground", Color) = (0.28, 0.26, 0.24, 1)
        _Exposure ("Exposure", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }
        Cull Off
        ZWrite Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _SkyColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float h = dir.y;
                float3 col;
                if (h >= 0.0)
                {
                    float t = saturate(pow(h, 0.55));
                    col = lerp(_HorizonColor.rgb, _SkyColor.rgb, t);
                }
                else
                {
                    float t = saturate(-h * 1.6);
                    col = lerp(_HorizonColor.rgb, _GroundColor.rgb, t);
                }
                return float4(col * _Exposure, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
