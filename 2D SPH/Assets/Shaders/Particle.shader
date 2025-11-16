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

            int billboard;
            float size;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 col : TEXCOORD1;
                float3 normal : NORMAL;
            };

            v2f vert(appdata_full v, uint id : SV_InstanceID) {
                float3 centreWorld = positions[id];

                v2f o;

                if (billboard == 1) {
                    float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                    float3 cameraUp = UNITY_MATRIX_V[1].xyz;

                    float3 vertWorld = centreWorld +
                        cameraRight * v.vertex.x * size * 0.5 +
                        cameraUp * v.vertex.y * size * 0.5;

                    o.pos = mul(UNITY_MATRIX_VP, float4(vertWorld, 1));
                    o.normal = -normalize(_WorldSpaceCameraPos - centreWorld);
                } else {
                    float3 centreObj = mul(unity_WorldToObject, float4(centreWorld, 1)).xyz;
                    float3 vertObj = centreObj + v.vertex * (size * 0.5);

                    o.pos = UnityObjectToClipPos(vertObj);
                    o.normal = normalize(v.vertex);
                }

                o.col = colours[id];
                o.uv = v.texcoord;
                return o;
            }

			float4 frag (v2f i) : SV_Target
			{
				if (billboard == 1) {
					float2 centeredUV = i.uv * 2.0 - 1.0; // Map to [-1, 1]
					float dist = length(centeredUV);

					if (dist > 1.0) discard;

					float edge = 1.0 - smoothstep(0.95, 1.0, dist);

					float3 normal = normalize(float3(centeredUV, sqrt(1.0 - dist * dist)));
					float shading = saturate(dot(_WorldSpaceLightPos0.xyz, normal));
					shading = shading * 0.7 + 0.3;

					return float4(i.col * shading, edge);
				} else {
					float shading = saturate(dot(_WorldSpaceLightPos0.xyz, i.normal));
					shading = shading * 0.7 + 0.3;

					return float4(i.col * shading, 1);
				}
			}
            ENDCG
        }
    }
}