using UnityEngine;

public class Rays
{
    Shader shader;

    GameObject cube;

    Material mat;

    public Rays(Shader raysShader, GameObject cube)
    {
        shader = raysShader;
        mat = new Material(shader);
        this.cube = cube;
        this.cube.GetComponent<Renderer>().material = mat;
    }

    public void BindTexture(RenderTexture densityTex)
    {
        mat.SetTexture("DensityTex", densityTex);
    }

    public void RenderToCube()
    {
        cube.SetActive(true);
    }

    public void DisableRays()
    {
       cube.SetActive(false);
    }
}