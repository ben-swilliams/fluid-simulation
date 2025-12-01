  Shader "Custom/Rays"
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

              struct v2f
              {
              };

              v2f vert (uint vertex_id: SV_VertexID)
              {
                  v2f o;
                  return o;
              }

              float4 frag (v2f i) : SV_Target
              {
                return float4(1, 1, 1, 1);
              }
              ENDCG
          }
      }
  }