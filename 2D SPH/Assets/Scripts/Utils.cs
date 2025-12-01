using UnityEngine;
using UnityEngine.Experimental.Rendering;

static class Utils
{
    public class Constants
    {
        public static int threadGroupSize = 256;
        public static int scanBlockSize = threadGroupSize * 2;
        public static int stableWCSPHStep = 1000;
        public static int stableIISPHStep = 150;
        public static int stablePCISPHStep = 150;
    }

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
}