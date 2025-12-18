  Shader "Custom/MarchingCubes"
  {
      Properties
      {
      }

      SubShader
      {
          Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"="Opaque"
        }

          Pass
          {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
            };

            struct Vertex {
                float3 position;
                float3 normal;
            };

            struct Triangle {
                Vertex a;
                Vertex b;
                Vertex c;
            };

            StructuredBuffer<Triangle> VertexBuffer;
            float4 col;

            Varyings vert (uint vertex_id : SV_VertexID)
            {
                // Each triangle has 3 vertices
                int triIndex = vertex_id / 3;
                int vertIndex = vertex_id % 3;

                Triangle tri = VertexBuffer[triIndex];

                Vertex v;
                if (vertIndex == 0) v = tri.a;
                else if (vertIndex == 1) v = tri.b;
                else v = tri.c;

                Varyings OUT;
                OUT.pos = TransformObjectToHClip(v.position);
                OUT.normal = TransformObjectToWorldNormal(v.normal);
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float shading = saturate(dot(GetMainLight().direction, IN.normal));
                shading = shading * 0.7 + 0.3;

                return float4(col.rgb * shading, col.a);
            }
            ENDHLSL
          }
      }
  }