  Shader "Custom/Rays"
  {
      Properties
      {
      }

      SubShader
      {
          Tags { "RenderType"="Transparent" }
          Cull Off ZWrite Off
          Blend SrcAlpha OneMinusSrcAlpha

          Pass
          {
              CGPROGRAM
              #pragma vertex vert
              #pragma fragment frag
              #pragma target 5.0

              #include "UnityCG.cginc"

              Texture3D<float4> DensityTex;
              SamplerState samplerDensityTex;

              static const float stepSize = 0.01;
              static const int maxSteps = 128;

              struct v2f
              {
                float4 vertex : SV_POSITION;
                float3 uvwEntry : TEX_COORD0;
                float3 uvwRayDir : TEXCOORD1;
              };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                o.uvwEntry = v.vertex.xyz + 0.5;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldRayDir = normalize(worldPos - _WorldSpaceCameraPos);
                o.uvwRayDir = mul(unity_WorldToObject, float4(worldRayDir, 0)).xyz;

                return o;
            }

              float4 frag (v2f i) : SV_Target
              {
                float transmittance = 1.0;

                float3 rayLoc = i.uvwEntry;
                float3 rayDir = normalize(i.uvwRayDir);

                [loop]
                for (int _ = 0; _ < maxSteps; _++) {
                    if (any(rayLoc < 0) || any(rayLoc > 1)) break;
                    if (transmittance < 0.01) break;

                    float density = DensityTex.Sample(samplerDensityTex, rayLoc).r;
                    
                    transmittance *= exp(-density * stepSize);

                    rayLoc += rayDir * stepSize;
                }

                float opacity = 1 - transmittance;
                
                return float4(1, 1, 1, opacity);
              }
              ENDCG
          }
      }
  }