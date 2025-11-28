using UnityEngine;

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

    public static void SetValues(ComputeShader shader, object[] pairs)
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