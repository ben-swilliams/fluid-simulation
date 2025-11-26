  Shader "Custom/MarchingCubes"
  {
      Properties
      {
      }

      SubShader
      {
          Tags { "RenderType"="Opaque" }

          Pass
          {
              CGPROGRAM
              #pragma vertex vert
              #pragma fragment frag
              #pragma target 5.0

              #include "UnityCG.cginc"

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

              struct v2f
              {
                  float4 pos : SV_POSITION;
                  float3 normal : NORMAL;
              };

              v2f vert (uint vertex_id: SV_VertexID)
              {
                  // Each triangle has 3 vertices
                  int triIndex = vertex_id / 3;
                  int vertIndex = vertex_id % 3;

                  Triangle tri = VertexBuffer[triIndex];

                  Vertex v;
                  if (vertIndex == 0) v = tri.a;
                  else if (vertIndex == 1) v = tri.b;
                  else v = tri.c;

                  v2f o;
                  o.pos = UnityObjectToClipPos(float4(v.position, 1));
                  o.normal = UnityObjectToWorldNormal(v.normal);
                  return o;
              }

              float4 frag (v2f i) : SV_Target
              {
                    float shading = saturate(dot(_WorldSpaceLightPos0.xyz, i.normal));
					shading = shading * 0.7 + 0.3;

					return float4(float3(1,1,1) * shading, 1);
              }
              ENDCG
          }
      }
  }