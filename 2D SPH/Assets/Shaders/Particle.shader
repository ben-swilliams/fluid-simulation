Shader "Custom/Particle" {
    Properties {
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            StructuredBuffer<float3> positions;
            StructuredBuffer<float3> colours;

            int billboard;
            float size;

            struct Attributes {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 col : TEXCOORD1;
                float3 normal : TEXCOORD2;
            };

            Varyings vert(Attributes IN) {
                float3 centreWorld = positions[IN.instanceID];

                Varyings OUT;

                if (billboard == 1) {
                    float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                    float3 cameraUp = UNITY_MATRIX_V[1].xyz;

                    float3 vertWorld = centreWorld +
                        cameraRight * IN.vertex.x * size * 0.5 +
                        cameraUp * IN.vertex.y * size * 0.5;

                    OUT.pos = mul(UNITY_MATRIX_VP, float4(vertWorld, 1));
                    OUT.normal = -normalize(_WorldSpaceCameraPos - centreWorld);
                } else {
                    float3 centreObj = mul(unity_WorldToObject, float4(centreWorld, 1)).xyz;
                    float3 vertObj = centreObj + IN.vertex.xyz * (size * 0.5);

                    OUT.pos = TransformObjectToHClip(vertObj);
                    OUT.normal = normalize(IN.vertex.xyz);
                }

                OUT.col = colours[IN.instanceID];
                OUT.uv = IN.texcoord;
                return OUT;
            }

			float4 frag (Varyings IN) : SV_Target
			{
                Light light = GetMainLight();
				if (billboard == 1) {
					float2 centeredUV = IN.uv * 2.0 - 1.0; // Map to [-1, 1]
					float dist = length(centeredUV);

					if (dist > 1.0) discard;

					float edge = 1.0 - smoothstep(0.95, 1.0, dist);

					float3 normal = normalize(float3(centeredUV, sqrt(1.0 - dist * dist)));
					float shading = saturate(dot(light.direction, normal));
					shading = shading * 0.7 + 0.3;

					return float4(IN.col * shading, edge);
				} else {
					float shading = saturate(dot(light.direction, IN.normal));
					shading = shading * 0.7 + 0.3;

					return float4(IN.col * shading, 1);
				}
			}
            ENDHLSL
        }
    }
}