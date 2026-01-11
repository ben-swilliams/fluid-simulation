using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Common
{
    public enum Solver { WCSPH, PCISPH, IISPH };
    public enum Kernel {Cubic, Spiky, Poly6};
    public class Constants
    {
        public static int threadGroupSize = 256;
        public static int scanBlockSize = threadGroupSize * 2;
        public static int stableWCSPHStep = 1000;
        public static int stableIISPHStep = 150;
        public static int stablePCISPHStep = 150;

        public static float CubicKernelConstant(float smoothingRadius)
        {
           return 8f / (Mathf.PI * Mathf.Pow(0.5f * smoothingRadius, 3)); 
        }

        public static float CubicGradConstant(float smoothingRadius)
        {
            return 6 * CubicKernelConstant(smoothingRadius) / (0.5f * smoothingRadius);
        }

        public static float SpikyKernelConstant(float smoothingRadius)
        {
            return 15f / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
        }

        public static float SpikyGradConstant(float smoothingRadius)
        {
            return 3 * SpikyKernelConstant(smoothingRadius);
        }

        public static float Poly6KernelConstant(float smoothingRadius)
        {
            return 315f / (64 * Mathf.PI * Mathf.Pow(smoothingRadius, 9));
        }

        public static float Poly6GradConstant(float smoothingRadius)
        {
            return -6 * Poly6KernelConstant(smoothingRadius);
        }
    };

    public static class Utils {
        public static void SetValues(object[] pairs, params ComputeShader[] shaders)
        {
            foreach (ComputeShader shader in shaders)
            {
                for (int i = 0; i < pairs.Length; i += 2)
                {
                    if (pairs[i] is not string name) continue;

                    if (pairs[i + 1] is int intVal)
                        shader.SetInt(name, intVal);
                    if (pairs[i + 1] is uint uintVal)
                        shader.SetInt(name, (int)uintVal);
                    if (pairs[i + 1] is Kernel enumVal)
                        shader.SetInt(name, (int)(object)enumVal);
                    if (pairs[i + 1] is float floatVal)
                        shader.SetFloat(name, floatVal);
                    if (pairs[i + 1] is Vector3 vecVal)
                        shader.SetVector(name, vecVal);
                }
            }
        }

        public static Vector3 Grad(Kernel k, Vector3 offset, float r, float smoothingRadius)
        {
            switch (k)
            {
                case Kernel.Cubic:
                    return CubicSplineGrad(offset, r, smoothingRadius);
                case Kernel.Spiky:
                    return SpikyGrad(offset, r, smoothingRadius);
                case Kernel.Poly6:
                    return Poly6Grad(offset, r, smoothingRadius);
            }

            return Vector3.zero;
        }

        public static Vector3 CubicSplineGrad(Vector3 offset, float r, float smoothingRadius)
        {
            if (r < 1e-12) return Vector3.zero;

            float q = r / (0.5f * smoothingRadius);
            float gradFactor = 0f;
            float gradConstant = Constants.CubicGradConstant(smoothingRadius);

            if (q < 1f)
            {
                gradFactor = gradConstant * (-3f * q + 2.25f * q * q);
            }
            else if (q < 2f)
            {
                float term = 2f - q;
                gradFactor = gradConstant * (-0.75f * term * term);
            }

            return offset * gradFactor / r;
        }

        public static Vector3 SpikyGrad(Vector3 offset, float r, float smoothingRadius)
        {
            if (r < 1e-12 || r > smoothingRadius) return Vector3.zero;

            Vector3 dir = offset / r;

            float coeff = (smoothingRadius - r) * (smoothingRadius - r);

            return Constants.SpikyGradConstant(smoothingRadius) * coeff * dir;
        }

        public static Vector3 Poly6Grad(Vector3 offset, float r, float smoothingRadius)
        {
            if (r < 1e-12 || r > smoothingRadius) return Vector3.zero;

            Vector3 dir = offset / r;

            float diff = smoothingRadius * smoothingRadius - r * r;

            return Constants.Poly6GradConstant(smoothingRadius) * diff * diff * dir;
        }


        public static RenderTexture CreateDensityTexture(int width, int height, int depth)
            {
                RenderTexture texture = new RenderTexture(width, height, 0);
                texture.graphicsFormat = GraphicsFormat.R16_SFloat;
                texture.volumeDepth = depth;
                texture.enableRandomWrite = true;
                texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                texture.useMipMap = false;
                texture.autoGenerateMips = false;
                texture.Create();

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                texture.name = "DensityMap";

                return texture;
            }

        public static float ComputeDelta(Kernel pressureKernel, float particleSpacing, float beta, float smoothingRadius)
        {
            Vector3 gradSum = Vector3.zero;
            float dotGradSum = 0f;

            Vector3 prototypePos = Vector3.zero;

            int range = Mathf.CeilToInt(smoothingRadius / particleSpacing);

            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    for (int z = -range; z <= range; z++)
                    {
                        if (x == 0 && y == 0 && z == 0) continue;

                        Vector3 neighborPos = new Vector3(x, y, z) * particleSpacing;
                        Vector3 offset = prototypePos - neighborPos;
                        float r = offset.magnitude;

                        if (r >= smoothingRadius) continue;

                        Vector3 grad = Utils.Grad(pressureKernel, offset, r, smoothingRadius);

                        gradSum += grad;
                        dotGradSum += Vector3.Dot(grad, grad);
                    }
                }
            }

            float denominator = beta * (-Vector3.Dot(gradSum, gradSum) - dotGradSum);

            if (Mathf.Abs(denominator) < 1e-12)
            {
                return 0f;
            }

            return -1f / denominator;
        }

        public static int SolverSteps(Solver solver)
        {
            if (solver == Solver.WCSPH) return Constants.stableWCSPHStep;
            if (solver == Solver.IISPH) return Constants.stableIISPHStep;
            if (solver == Solver.PCISPH) return Constants.stablePCISPHStep;

            return Constants.stableWCSPHStep;
        }

        public static int CalculateCellNumber(Vector3 containerSize, float smoothingRadius)
        {
            float cellSize = smoothingRadius;

            // Calculate grid dimensions (number of cells in each axis)
            int gridX = Mathf.CeilToInt(containerSize.x / cellSize);
            int gridY = Mathf.CeilToInt(containerSize.y / cellSize);
            int gridZ = Mathf.CeilToInt(containerSize.z / cellSize);

            int cellCount = gridX * gridY * gridZ;

            return cellCount;
        }

        public static Vector3[] GenerateVelocityData(int instanceCount, float speed = 0)
        {
            Vector3[] velocities = new Vector3[instanceCount];

            for (int i = 0; i < instanceCount; i++)
            {
                float random = Random.Range(0f, 2 * Mathf.PI);
                Vector3 vel = new Vector3(Mathf.Cos(random), Mathf.Sin(random)) * speed;
                velocities[i] = vel;
            }

            return velocities;
        }

    }
}