using Unity.Mathematics;
using UnityEngine;

public class Rays
{
    Shader shader;

    GameObject cube;

    Material mat;

    public Material Mat => mat;

    public Rays(Shader raysShader, GameObject cube)
    {
        shader = raysShader;
        mat = new Material(shader);
        this.cube = cube;
        this.cube.GetComponent<Renderer>().material = mat;
    }

    public void RenderToCube(RenderTexture densityTex, float densityThreshold, float4x4 worldToContainer)
    {
        Shader.SetGlobalMatrix("worldtoContainer", worldToContainer);
        Shader.SetGlobalTexture("DensityTex", densityTex);
        Shader.SetGlobalFloat("densityThreshold", densityThreshold);
    }

    public void DisableRays()
    {
       cube.SetActive(false);
    }
}