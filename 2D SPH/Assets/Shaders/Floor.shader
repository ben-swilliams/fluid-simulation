Shader "Custom/Floor" {
    Properties {
    }

    SubShader {
        Tags {
            "Queue"="Geometry"
        }

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            float frequency;

            struct v2f {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            v2f vert(appdata_full v, uint id : SV_InstanceID) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float nDotL = max(0, dot(i.normal, lightDir));

                float2 cell = floor(i.worldPos.xz / frequency);
                int2 cellInt = int2(cell);
                float checker = (cellInt.x & 1) ^ (cellInt.y & 1);

                float3 colorA = float3(0.8, 0.8, 0.8);
                float3 colorB = float3(0.2, 0.2, 0.2);

                float3 finalColor = lerp(colorA, colorB, checker);

                return float4(finalColor * nDotL, 1.0);
            }
            ENDCG
        }
    }
}