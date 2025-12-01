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

              Texture3D<float4> DensityTex;
              SamplerState linearClampSampler;

              struct v2f
              {
                float4 vertex : SV_POSITION;
                float3 uvw : TEX_COORD0;
              };

              v2f vert (appdata_full v, uint id : SV_InstanceID)
              {
                  v2f o;

                  float3 uvw = v.vertex + 0.5;

                  o.vertex = UnityObjectToClipPos(v.vertex);
                  o.uvw = uvw;
                  return o;
              }

              float4 frag (v2f i) : SV_Target
              {
                float4 col = DensityTex.Sample(linearClampSampler, i.uvw);
                return float4(col.r, 1, 1, 1);
              }
              ENDCG
          }
      }
  }