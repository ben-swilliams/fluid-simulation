Shader "Custom/Floor" {
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
            #pragma target 5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float frequency;

            struct Attributes {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct Varyings {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.pos = TransformObjectToHClip(IN.vertex.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.vertex.xyz);
                OUT.normal = TransformObjectToWorldNormal(IN.normal);
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                Light light = GetMainLight();
                float shading = saturate(dot(IN.normal, light.direction)) * 0.7 + 0.3;

                float2 cell = floor(IN.worldPos.xz / frequency);
                int2 cellInt = int2(cell);
                float checker = (cellInt.x & 1) ^ (cellInt.y & 1);

                float3 colorA = float3(0.8, 0.8, 0.8);
                float3 colorB = float3(0.2, 0.2, 0.2);

                float3 finalColor = lerp(colorA, colorB, checker);

                return float4(finalColor * shading, 1.0);
            }
            ENDHLSL
        }
    }
}