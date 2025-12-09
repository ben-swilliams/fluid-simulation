  Shader "Custom/Rays"
  {
      Properties
      {
      }

      SubShader
      {
          Tags { "RenderType"="Transparent" "Queue"="Transparent" }
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
              float3 floorSize;

              float densityMultiplier;
              float sunIntensity;
              float densityThreshold;

              float fluidIOR;

              int maxRefractions;

              float checkerFrequency;

              static const float fluidStepSize = 0.005;
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
                return SampleDensity(uvw) > 0;
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

            float DensityAlongRay(float3 rayOrigin, float3 rayDir, float rayStepSize) {
                float3 rayLoc = rayOrigin;
                float totalDensity = 0;

                for (int _ = 0; _ < maxSteps; _++) {
                    if (any(rayLoc < 0) || any(rayLoc > 1)) break;

                    totalDensity += SampleDensity(rayLoc) * rayStepSize * densityMultiplier;

                    rayLoc += rayDir * rayStepSize;
                }

                return totalDensity;
            }

            SurfacePoint FindSurfaceAlongRay(float3 rayOrigin, float3 rayDir, float rayStepSize, bool findEntry) {
                SurfacePoint sp;
                sp.isSurface = false;

                // Assume we know that ray origin is part of our current phase
                float3 rayLoc = rayOrigin + rayDir * rayStepSize;

                float totalDensity = 0;

                for (int _ = 0; _ < maxSteps; _++) {
                    if (any(rayLoc < 0) || any(rayLoc > 1)) {
                        // Still set density if we have done fluid -> box
                        sp.densityEnRoute = totalDensity;
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

            float3 SampleFloor(float3 pos) {
                float2 cell = floor(pos.xz / checkerFrequency);
                int2 cellInt = int2(cell);
                float checker = (cellInt.x & 1) ^ (cellInt.y & 1);

                float3 colorA = float3(0.8, 0.8, 0.8);
                float3 colorB = float3(0.2, 0.2, 0.2);

                return lerp(colorA, colorB, checker);
            }

            float3 RefractRay(float3 fluidUVW, float3 rayDir, bool isEntry) {
                float3 normal = CalculateNormal(fluidUVW);
                if (dot(rayDir, normal) > 0)
                    normal = -normal;

                float ior = isEntry ? 1 / fluidIOR : fluidIOR;

                float3 refractedDir = refract(rayDir, normal, ior);

                if (dot(refractedDir, refractedDir) == 0)
                    return reflect(rayDir, normal);
                
                return refractedDir;
            }

            float4 frag (v2f i) : SV_Target
            {
              float3 rayLoc = i.uvwEntry;
              float3 rayDir = normalize(i.uvwRayDir);

              bool inFluid = IsInFluid(rayLoc);

              SurfacePoint sp;

              if (!inFluid) {
                sp = FindSurfaceAlongRay(rayLoc, rayDir, fluidStepSize, true);
                if (!sp.isSurface) return float4(1, 1, 1, 0);

                rayLoc = sp.uvw;
                inFluid = true;
              }

              // At this point, we have either not found any fluid and returned see-through colour
              // Or we have a surface on the fluid

              float3 totalLight = float3(0, 0, 0);
              float totalDensity = 0;
              float3 transmittance = 1;

                for (int _ = 0; _ < maxRefractions; _++) {
                    // Exiting
                    if (inFluid) {
                        sp = FindSurfaceAlongRay(rayLoc, rayDir, fluidStepSize, false);

                        totalDensity += sp.densityEnRoute;
                        transmittance = exp(-totalDensity * scatterCoeffs);

                        if (!sp.isSurface) break;

                        rayLoc = sp.uvw;
                        inFluid = false;
                    // Entering
                    } else {
                        sp = FindSurfaceAlongRay(rayLoc, rayDir, fluidStepSize, true);
                        if (!sp.isSurface) break;

                        rayLoc = sp.uvw;
                        inFluid = true;
                    }
                }

                float3 light;

                if (rayDir.y >= 0) {
                    // Ray going up, hits sky
                    light = float3(1, 1, 1);
                } else {
                    float t = rayLoc.y / -rayDir.y;
                    if (t >= 0) {
                        // Hit the floor
                        float3 floorLocUVW = rayLoc + t * rayDir;
                        float3 floorLocWorld = mul(unity_ObjectToWorld, float4(floorLocUVW - 0.5, 1)).xyz;

                        if (abs(floorLocWorld.x) < floorSize.x && abs(floorLocWorld.z) < floorSize.z)
                            light = SampleFloor(floorLocWorld);
                        else
                            light = float3(1, 1, 1);
                    } else {
                        light = float3(1, 1, 1);
                    }
                }

                float sunDensity = DensityAlongRay(i.uvwEntry, sunDir, lightStepSize);
                float3 sunContribution = exp(-sunDensity * scatterCoeffs) * sunIntensity;
                light *= sunContribution;

                totalLight = light * transmittance;

                float opacity = 1 - (transmittance.r + transmittance.g + transmittance.b) / 3.0;
                return float4(totalLight, opacity);
            }
            ENDCG
          }
      }
  }