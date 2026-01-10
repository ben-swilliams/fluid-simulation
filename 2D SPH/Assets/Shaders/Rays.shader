  Shader "Custom/Rays"
  {
      Properties
      {
      }

      SubShader
      {
          Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }
          Cull Back ZWrite Off
          Blend SrcAlpha OneMinusSrcAlpha

          Pass
          {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma multi_compile _ RAYS_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            Texture3D<float4> DensityTex;
            SamplerState samplerDensityTex;

            float4x4 worldToContainer;
            float4x4 containerToWorld;

            float3 scatterCoeffs;
            float3 floorSize;
            float3 skyTint;

            float densityMultiplier;
            float densityThreshold;

            float fluidIOR;

            int maxRefractions;

            float chequerFrequency;

            static const float fluidStepSize = 0.005;
            static const float lightStepSize = 0.1;
            static const int maxSteps = 1024;

            struct Attributes {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;  // Clip space position
                float2 uv : TEXCOORD0;             // Screen UV (0-1 range)
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);

                OUT.uv = GetFullScreenTriangleTexCoord(IN.vertexID);

                return OUT;
            }

            float SampleDensity(float3 uvw) {
                if (any(uvw < 0) || any(uvw > 1)) return 0;
                float sample = DensityTex.SampleLevel(samplerDensityTex, uvw, 0).r;

                return max(0, sample - densityThreshold);
            }

            bool IsInFluid(float3 uvw) {
                return SampleDensity(uvw) > 0;
            }

            float3 CalculateNormal(float3 uvw) {
                const float offsetSize = 0.01;
                float3 offsetX = float3(1, 0, 0) * offsetSize;
                float3 offsetY = float3(0, 1, 0) * offsetSize;
                float3 offsetZ = float3(0, 0, 1) * offsetSize;

                float dx = SampleDensity(uvw - offsetX) - SampleDensity(uvw + offsetX);
                float dy = SampleDensity(uvw - offsetY) - SampleDensity(uvw + offsetY);
                float dz = SampleDensity(uvw - offsetZ) - SampleDensity(uvw + offsetZ);

                float3 surfaceNormal = normalize(float3(dx, dy, dz));

                // Blend normal at edges of container
                float3 centred = uvw - 0.5;
                float3 distances = 0.5 - abs(centred);
                float closestDistance = min(distances.x, min(distances.y, distances.z));

                // Normal of the face we are closest to
                float3 faceNormal = (distances.x < distances.y && distances.x < distances.z) ? float3(sign(centred.x), 0, 0) :
                    (distances.y < distances.z) ? float3(0, sign(centred.y), 0) :
                    float3(0, 0, sign(centred.z));

                const float smoothDistance = 0.05;
                const float smoothPow = 5;

                float faceWeight = closestDistance;
                faceWeight = (1 - smoothstep(0, smoothDistance, faceWeight)) *
                            (1 - pow(saturate(surfaceNormal.y), smoothPow));

                return normalize(surfaceNormal * (1 - faceWeight) + faceNormal * faceWeight);
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
                float3 lastLoc = rayOrigin;
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
                        lastLoc = rayLoc;
                        totalDensity += SampleDensity(rayLoc) * rayStepSize * densityMultiplier;
                    } else if (!findEntry) {
                        sp.uvw = lastLoc;
                        sp.norm = CalculateNormal(lastLoc);
                        sp.densityEnRoute = totalDensity;
                        sp.isSurface = true;
                        break;
                    }

                    lastLoc = rayLoc;
                    rayLoc += rayDir * rayStepSize;
                }

                return sp;
            }

            float3 SampleFloor(float3 pos) {
                float2 cell = floor(pos.xz / chequerFrequency);
                int2 cellInt = int2(cell);
                float checker = (cellInt.x & 1) ^ (cellInt.y & 1);

                float3 colorA = float3(0.8, 0.8, 0.8);
                float3 colorB = float3(0.2, 0.2, 0.2);

                return lerp(colorA, colorB, checker);
            }
            
            float3 SampleEnvironment(float3 rayLoc, float3 rayDir) {
                // Skybox sample
                float3 light = GlossyEnvironmentReflection(rayDir, 0.0, 1.0) * skyTint;

                if (rayDir.y < 0) {
                    float t = rayLoc.y / -rayDir.y;
                    if (t >= 0) {
                        // Hit the floor
                        float3 floorLocUVW = rayLoc + t * rayDir;  // Already in 0-1 space
                        float3 floorLocWorld = mul(containerToWorld, float4(floorLocUVW, 1.0)).xyz;

                        if (abs(floorLocWorld.x) < floorSize.x && abs(floorLocWorld.z) < floorSize.z)
                            light = SampleFloor(floorLocWorld);
                    }
                }

                return light;
            }

            float3 RefractRay(float3 fluidUVW, float3 rayDir) {
                float3 normal = CalculateNormal(fluidUVW);
                bool enteringMaterial = dot(rayDir, normal) < 0;
                
                if (!enteringMaterial)
                    normal = -normal;

                float eta = enteringMaterial ? (1.0 / fluidIOR) : fluidIOR;
                float3 refractedDir = refract(rayDir, normal, eta);

                if (dot(refractedDir, refractedDir) == 0)
                    return reflect(rayDir, normal);
                
                return refractedDir;
            }

            float CalculateReflectionCoefficient(float3 rayDir, float3 normal, float n1, float n2) {
                float innerF0 = (n2 - n1) / (n2 + n1);
                float f0 = innerF0 * innerF0;

                float cosTheta = abs(dot(normal, rayDir));
                float innerFinal = 1 - cosTheta;
                float innerFinalSq = innerFinal * innerFinal;
                float innerFinal4 = innerFinalSq * innerFinalSq;

                return f0 + (1 - f0) * innerFinal4 * innerFinal;
            }

            struct RaysInfo {
                float3 refractDir;
                float3 reflectDir;
                float refractCoeff;
                float reflectCoeff;
            };

            RaysInfo CalculateOutputRays(float3 rayUVW, float3 rayDir) {
                RaysInfo ri;

                float3 normal = CalculateNormal(rayUVW);
                bool enteringMaterial = dot(rayDir, normal) < 0;
                
                if (!enteringMaterial)
                    normal = -normal;

                float eta = enteringMaterial ? (1.0 / fluidIOR) : fluidIOR;
                
                ri.refractDir = refract(rayDir, normal, eta);
                ri.reflectDir = reflect(rayDir, normal);

                // Total internal reflection
                if (!any(ri.refractDir)) {
                    ri.reflectCoeff = 1;
                    ri.refractCoeff = 0;
                    return ri;
                }

                float n1 = enteringMaterial ? 1.0 : fluidIOR;
                float n2 = enteringMaterial ? fluidIOR : 1.0;
                float reflectCoeff = CalculateReflectionCoefficient(rayDir, normal, n1, n2);
                ri.reflectCoeff = reflectCoeff;
                ri.refractCoeff = 1 - reflectCoeff;

                return ri;
            }

            bool RayBoxIntersection(float3 rayOrigin, float3 rayDir, out float tMin, out float tMax)
            {
                float3 invDir = 1.0 / rayDir;
                float3 t0 = (0.0 - rayOrigin) * invDir;
                float3 t1 = (1.0 - rayOrigin) * invDir;

                float3 tmin3 = min(t0, t1);
                float3 tmax3 = max(t0, t1);

                tMin = max(max(tmin3.x, tmin3.y), tmin3.z);
                tMax = min(min(tmax3.x, tmax3.y), tmax3.z);

                return tMax >= tMin && tMax > 0;
            }

            bool IntersectWithContainer(float2 uv, out float3 origin, out float3 rayDir) {
                float3 viewVector = ComputeWorldSpacePosition(uv, 1.0, UNITY_MATRIX_I_VP) - _WorldSpaceCameraPos;

                float3 worldRayOrigin = _WorldSpaceCameraPos;
                float3 worldRayDir = normalize(viewVector);

                float3 localRayOrigin = mul(worldToContainer, float4(worldRayOrigin, 1)).xyz;
                rayDir = normalize(mul((float3x3)worldToContainer, worldRayDir));

                float tMin;
                float tMax;
                bool boxIntersect = RayBoxIntersection(localRayOrigin, rayDir, tMin, tMax);

                origin = localRayOrigin + max(0.0, tMin) * rayDir;

                return boxIntersect;
            }

            float4 frag (Varyings IN) : SV_Target {
                #ifndef RAYS_ENABLED
                    return float4(0, 0, 0, 0);
                #endif

                float3 rayLoc;
                float3 rayDir;

                bool boxIntersect = IntersectWithContainer(IN.uv, rayLoc, rayDir);
                if (!boxIntersect) return float4(0, 0, 0, 0);

                bool inFluid = IsInFluid(rayLoc);

                float3 totalLight = float3(0, 0, 0);
                float3 transmittance = 1;

                // Handle initial entry if outside fluid
                if (!inFluid) {
                    SurfacePoint sp = FindSurfaceAlongRay(rayLoc, rayDir, fluidStepSize, true);
                    if (!sp.isSurface) return float4(1, 1, 1, 0);

                    rayLoc = sp.uvw;
                    RaysInfo raysInfo = CalculateOutputRays(rayLoc, rayDir);

                    bool traceReflection = raysInfo.reflectCoeff > raysInfo.refractCoeff;
                    
                    if (traceReflection) {
                        SurfacePoint refractSp = FindSurfaceAlongRay(rayLoc, raysInfo.refractDir, fluidStepSize, false);
                        if (refractSp.isSurface) {
                            float3 refractTransmittance = exp(-refractSp.densityEnRoute * scatterCoeffs);
                            totalLight += SampleEnvironment(refractSp.uvw, raysInfo.refractDir) * raysInfo.refractCoeff * refractTransmittance * transmittance;
                        }
                        rayDir = raysInfo.reflectDir;
                        transmittance *= raysInfo.reflectCoeff;
                    } else {
                        totalLight += SampleEnvironment(rayLoc, raysInfo.reflectDir) * raysInfo.reflectCoeff * transmittance;
                        rayDir = raysInfo.refractDir;
                        inFluid = true;
                        transmittance *= raysInfo.refractCoeff;
                    }
                }

                for (int ref = 0; ref < maxRefractions; ref++) {
                    float stepSize = inFluid ? lightStepSize * (ref + 1) : lightStepSize * (ref + 1);
                    
                    SurfacePoint sp = FindSurfaceAlongRay(rayLoc, rayDir, inFluid ? fluidStepSize : stepSize, !inFluid);
                    
                    if (inFluid) {
                        transmittance *= exp(-sp.densityEnRoute * scatterCoeffs);
                    }
                    
                    if (!sp.isSurface) break;

                    rayLoc = sp.uvw;
                    RaysInfo raysInfo = CalculateOutputRays(rayLoc, rayDir);

                    float refractDensity = DensityAlongRay(rayLoc, raysInfo.refractDir, stepSize);
                    float reflectDensity = DensityAlongRay(rayLoc, raysInfo.reflectDir, stepSize);

                    bool traceReflection = reflectDensity * raysInfo.reflectCoeff > refractDensity * raysInfo.refractCoeff;
                    
                    float3 boringDir = traceReflection ? raysInfo.refractDir : raysInfo.reflectDir;
                    float boringCoeff = traceReflection ? raysInfo.refractCoeff : raysInfo.reflectCoeff;
                    float boringDensity = traceReflection ? refractDensity : reflectDensity;
                    
                    float3 boringTransmittance = exp(-boringDensity * scatterCoeffs);
                    totalLight += SampleEnvironment(rayLoc, boringDir) * boringCoeff * boringTransmittance * transmittance;
                    
                    rayDir = traceReflection ? raysInfo.reflectDir : raysInfo.refractDir;
                    inFluid = traceReflection ? inFluid : !inFluid;
                    transmittance *= traceReflection ? raysInfo.reflectCoeff : raysInfo.refractCoeff;
                }

                Light light = GetMainLight();
                float3 envLight = SampleEnvironment(rayLoc, rayDir);
                float sunDensity = DensityAlongRay(rayLoc, -light.direction, lightStepSize);
                envLight *= exp(-sunDensity * scatterCoeffs) * light.color;
                totalLight += envLight * transmittance;

                return float4(totalLight, 1);
            }
            ENDHLSL
          }
      }
  }