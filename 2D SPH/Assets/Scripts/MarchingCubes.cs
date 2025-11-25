using UnityEngine;

class MarchingCubes
{
    Shader cubesShader;

    private Bounds bounds;
    private Mesh mesh;
    private Material mat;
    private ComputeBuffer argsBuffer;
    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    public MarchingCubes(Shader shader, Bounds bounds, Mesh mesh)
    {
        cubesShader = shader;
        this.bounds = bounds;
        this.mesh = mesh;
        mat = new Material(cubesShader);
    }

    void InitialiseArgsBuffer(int instanceCount)
    {
        if (argsBuffer != null) argsBuffer.Release();

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
    }

    public void BindPositions(ComputeBuffer positionBuffer)
    {
        mat.SetBuffer("positions", positionBuffer);
        InitialiseArgsBuffer(positionBuffer.count);
    }

    public void UpdateContainerSize(Vector3 containerSize)
    {
        mat.SetVector("containerSize", containerSize);
    }

    public void DrawFrame(RenderTexture densityTex)
    {
        mat.SetTexture("DensityTex", densityTex);

        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            mat,
            bounds,
            argsBuffer
        );
    }

    public void CleanupBuffers()
    {
        if (argsBuffer != null)
            argsBuffer.Release();
    }
}