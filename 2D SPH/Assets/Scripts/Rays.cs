using UnityEngine;

public class Rays
{
    Shader shader;
    GameObject cube;

    public Rays(Shader raysShader, GameObject cube)
    {
        shader = raysShader;
        this.cube = cube;
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