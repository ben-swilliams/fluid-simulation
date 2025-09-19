Shader "Custom/Particle" {
    Properties {

    }

    SubShader {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            StructuredBuffer<float2> Positions;
            float size;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata_full v, uint id : SV_InstanceID) {
                float3 centreWorld = float3(Positions[id], 0);
                float3 centreObj = mul(unity_WorldToObject, float4(centreWorld, 1)).xyz;
                float3 vertObj = centreObj + v.vertex * size;

                v2f o;

                o.pos = UnityObjectToClipPos(vertObj);
                o.uv = v.texcoord;

                return o;
            }

            float4 frag (v2f i) : SV_Target {
                return float4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
}