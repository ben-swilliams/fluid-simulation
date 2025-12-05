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

              float3 sunDir;
              float3 scatterCoeffs;

              float densityMultiplier;
              float sunIntensity;
              float densityThreshold;

              static const float fluidStepSize = 0.01;
              static const float lightStepSize = 0.2;
              static const int maxSteps = 256;

              struct v2f
              {
                float4 vertex : SV_POSITION;
                float3 uvwEntry : TEXCOORD0;
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

                // point to light
                float3 lightRayDir = normalize(_WorldSpaceLightPos0.xyz);

                return o;
            }

            float SampleDensity(float3 uvw) {
                float sample = DensityTex.SampleLevel(samplerDensityTex, uvw, 0).r;

                return max(0, sample - densityThreshold);
            }

            bool IsInFluid(float3 uvw) {
                return SampleDensity(uvw) > densityThreshold;
            }

            float3 CalculateNormal(float3 uvw) {
                const float offsetSize = 0.05;
                float3 offsetX = float3(1, 0, 0) * offsetSize;
                float3 offsetY = float3(0, 1, 0) * offsetSize;
                float3 offsetZ = float3(0, 0, 1) * offsetSize;

                float dx = SampleDensity(uvw - offsetX) - SampleDensity(uvw + offsetX);
                float dy = SampleDensity(uvw - offsetY) - SampleDensity(uvw + offsetY);
                float dz = SampleDensity(uvw - offsetZ) - SampleDensity(uvw + offsetZ);

                return normalize(float3(dx, dy, dz));
            }

            struct SurfacePoint {
                float3 uvw;
                float3 norm;
                float densityEnRoute;
                // I.e. if a surface wasn't found
                bool isSurface;
            };

            SurfacePoint FindSurfaceAlongRay(float3 rayOrigin, float3 rayDir, float rayStepSize, bool findEntry) {
                SurfacePoint sp;
                sp.isSurface = false;

                // Assume we know that ray origin is part of our current phase
                float3 rayLoc = rayOrigin + rayDir * rayStepSize;

                float totalDensity = 0;

                for (int _ = 0; _ < maxSteps; _++) {
                    if (any(rayLoc < 0) || any(rayLoc > 1)) {
                        sp.isSurface = false;
                        break;
                    }

                    bool isInside = IsInFluid(rayLoc);

                    if (isInside && findEntry) {
                        sp.uvw = rayLoc;
                        sp.norm = CalculateNormal(rayLoc);
                        sp.isSurface = true;
                        break;
                    } else if (isInside) {
                        totalDensity += SampleDensity(rayLoc) * rayStepSize * densityMultiplier;
                    } else if (!findEntry) {
                        sp.uvw = rayLoc;
                        sp.norm = CalculateNormal(rayLoc);
                        sp.densityEnRoute = totalDensity;
                        sp.isSurface = true;
                        break;
                    }

                    rayLoc += rayDir * rayStepSize;
                }

                return sp;
            }

            float3 FindNextFluidPoint(float3 rayOrigin, float3 rayDir, float rayStepSize) {
                float3 rayLoc = rayOrigin;

                for (int i = 0; i < maxSteps; i++) {
                    if (any(rayLoc < 0) || any(rayLoc > 1)) break;

                    if (IsInFluid(rayLoc)) return rayLoc;

                    rayLoc += rayDir * rayStepSize;
                }

                // OOB position will auto terminate loop
                return float3(2, 2, 2);
            }

            float DensityOnRay(float3 rayOrigin, float3 rayDir, float rayStepSize) {
                float totalDensity = 0;

                float3 rayLoc = IsInFluid(rayOrigin) ? rayOrigin : FindNextFluidPoint(rayOrigin, rayDir, rayStepSize);

                for (int _ = 0; _ < maxSteps; _++) {
                    if (any(rayLoc < 0) || any(rayLoc > 1)) break;

                    float density = SampleDensity(rayLoc) * rayStepSize * densityMultiplier;
                    totalDensity += density; 

                    rayLoc += rayDir * rayStepSize;

                    if (!IsInFluid(rayLoc)) rayLoc = FindNextFluidPoint(rayLoc, rayDir, rayStepSize);
                }

                return totalDensity;
            }

            float4 frag (v2f i) : SV_Target
            {
              float totalDensity = 0;

              float3 rayLoc = i.uvwEntry;
              float3 rayDir = normalize(i.uvwRayDir);

              float3 totalLight = 0;

              float3 finalT = float3(1, 1, 1);

              [loop]
              for (int _ = 0; _ < maxSteps; _++) {
                  if (any(rayLoc < 0) || any(rayLoc > 1)) break;

                  float density = SampleDensity(rayLoc) * fluidStepSize * densityMultiplier;
                  totalDensity += density; 

                  float sunRayDensity = DensityOnRay(rayLoc, sunDir, lightStepSize);
                  float3 sunlight = exp(-sunRayDensity * scatterCoeffs) * sunIntensity;
                  float3 scatteredLight = density * scatterCoeffs * sunlight;
                  float3 transmittance = exp(-totalDensity * scatterCoeffs);

                  totalLight += scatteredLight * transmittance;
                  finalT *= transmittance;

                  rayLoc += rayDir * fluidStepSize;
              }
              
              float opacity = 1 - (finalT.r + finalT.g + finalT.b) / 3.0;
              return float4(totalLight.xyz, opacity);
            }
            ENDCG
          }
      }
  }