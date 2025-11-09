Shader "Custom/Particle" {
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
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            StructuredBuffer<float3> positions;
            StructuredBuffer<float3> colours;

            float size;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 col : TEXCOORD1;
                float3 normal : NORMAL;
            };

            v2f vert(appdata_full v, uint id : SV_InstanceID) {
                float3 centreWorld =  positions[id];
                float3 centreObj = mul(unity_WorldToObject, float4(centreWorld, 1)).xyz;
                float3 vertObj = centreObj + v.vertex * (size * 0.5);

                v2f o;
                o.pos = UnityObjectToClipPos(vertObj);
                o.col = colours[id];
                o.uv = v.texcoord;
                o.normal = normalize(v.vertex);
                return o;
            }

			float4 frag (v2f i) : SV_Target
			{
				float shading = saturate(dot(_WorldSpaceLightPos0.xyz, i.normal));
				shading = shading * 0.7 + 0.3;
				
				return float4(i.col * shading, 1);
			}
            ENDCG
        }
    }
}