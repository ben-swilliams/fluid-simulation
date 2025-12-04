  Shader "Custom/Rays"
  {
      Properties
      {
      }

      SubShader
      {
          Tags { "RenderType"="Transparent" }
          Cull Back ZWrite Off
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

              float3 scatterCoeffs;

              float densityMultiplier;
              float sunIntensity;
              float densityThreshold;

              static const float stepSize = 0.01;
              static const int maxSteps = 256;

              struct v2f
              {
                float4 vertex : SV_POSITION;
                float3 uvwEntry : TEXCOORD0;
                float3 uvwRayDir : TEXCOORD1;
                float3 uvwSunRayDir : TEXCOORD2;
              };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                o.uvwEntry = v.vertex.xyz + 0.5;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldRayDir = normalize(worldPos - _WorldSpaceCameraPos);
                o.uvwRayDir = mul(unity_WorldToObject, float4(worldRayDir, 0)).xyz;

                // point to light
                float3 lightRayDir = normalize(_WorldSpaceLightPos0.xyz);
                o.uvwSunRayDir = mul(unity_WorldToObject, float4(lightRayDir, 0)).xyz;

                return o;
            }

            float SampleDensity(float3 uvw) {
                float sample = DensityTex.Sample(samplerDensityTex, uvw).r;

                return max(0, sample - densityThreshold);
            }

            float DensityOnRay(float3 rayOrigin, float3 rayDir, float rayStepSize) {
                float totalDensity = 0;

                float3 rayLoc = rayOrigin;

                for (int _ = 0; _ < maxSteps; _++) {
                    if (any(rayLoc < 0) || any(rayLoc > 1)) break;

                    float density = SampleDensity(rayLoc) * rayStepSize * densityMultiplier;
                    totalDensity += density; 

                    rayLoc += rayDir * rayStepSize;
                }

                return totalDensity;
            }

            float4 frag (v2f i) : SV_Target
            {
              float totalDensity = 0;

              float3 rayLoc = i.uvwEntry;
              float3 rayDir = normalize(i.uvwRayDir);
              float3 sunDir = normalize(i.uvwSunRayDir);

              float3 totalLight = 0;

              float3 finalT = float3(1, 1, 1);

              [loop]
              for (int _ = 0; _ < maxSteps; _++) {
                  if (any(rayLoc < 0) || any(rayLoc > 1)) break;

                  float density = SampleDensity(rayLoc) * stepSize * densityMultiplier;
                  totalDensity += density; 

                  float sunRayDensity = DensityOnRay(rayLoc, sunDir, 0.2);
                  float3 sunlight = exp(-sunRayDensity * scatterCoeffs) * sunIntensity;
                  float3 scatteredLight = density * scatterCoeffs * sunlight;
                  float3 transmittance = exp(-totalDensity * scatterCoeffs);

                  totalLight += scatteredLight * transmittance;
                  finalT *= transmittance;

                  rayLoc += rayDir * stepSize;
              }
              
              float opacity = 1 - (finalT.r + finalT.g + finalT.b) / 3.0;
              return float4(totalLight.xyz, opacity);
            }
            ENDCG
          }
      }
  }