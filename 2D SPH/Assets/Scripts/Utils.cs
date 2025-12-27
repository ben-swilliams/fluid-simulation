using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Common
{
    public enum Solver { WCSPH, IISPH, PCISPH };
    public class Constants
    {
        public static int threadGroupSize = 256;
        public static int scanBlockSize = threadGroupSize * 2;
        public static int stableWCSPHStep = 1000;
        public static int stableIISPHStep = 150;
        public static int stablePCISPHStep = 150;
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
                    if (pairs[i + 1] is float floatVal)
                        shader.SetFloat(name, floatVal);
                    if (pairs[i + 1] is Vector3 vecVal)
                        shader.SetVector(name, vecVal);
                }
            }
        }

        public static Vector3 CubicSplineGrad(Vector3 offset, float r, float gradConstant, float smoothingRadius)
        {
            if (r < 1e-12) return Vector3.zero;

            float q = r / smoothingRadius;
            float gradFactor = 0f;

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

        public static float ComputeDelta(float particleSpacing, float beta, float gradConstant, float smoothingRadius)
        {
            Vector3 gradSum = Vector3.zero;
            float dotGradSum = 0f;

            Vector3 prototypePos = Vector3.zero;

            int range = Mathf.CeilToInt(2 * smoothingRadius / particleSpacing);

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

                        if (r >= 2 * smoothingRadius) continue;

                        Vector3 grad = Utils.CubicSplineGrad(offset, r, gradConstant, smoothingRadius);

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
            float cellSize = 2f * smoothingRadius;

            // Calculate grid dimensions (number of cells in each axis)
            int gridX = Mathf.CeilToInt(containerSize.x / cellSize);
            int gridY = Mathf.CeilToInt(containerSize.y / cellSize);
            int gridZ = Mathf.CeilToInt(containerSize.z / cellSize);

            int cellCount = gridX * gridY * gridZ;

            return cellCount;
        }

        public static Vector3[] GenerateVelocityData(int instanceCount, float speed)
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