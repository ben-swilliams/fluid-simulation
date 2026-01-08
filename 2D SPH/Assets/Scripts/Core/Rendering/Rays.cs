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

    public void RenderToCube(RenderTexture densityTex, float densityThreshold)
    {
        mat.SetTexture("DensityTex", densityTex);
        mat.SetFloat("densityThreshold", densityThreshold);
        cube.SetActive(true);
    }

    public void DisableRays()
    {
       cube.SetActive(false);
    }
}