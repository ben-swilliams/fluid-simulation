#nullable enable

using System.Collections.Generic;
using UnityEngine;

public struct BufferInfo
{
    public int Length;
    public int ElementSize;
    public byte[]? InitData;
}

public class BufferHelper
{
    ComputeShader shader;
    
    HashSet<string> managedBuffers;
    HashSet<string> externalDependencies;
    Dictionary<string, HashSet<int>> bufferToKernelDependencies;
    Dictionary<string, ComputeBuffer> buffers;

    public BufferHelper(ComputeShader shader,
                        Dictionary<int, string[]> kernelToBufferDependencies,
                        Dictionary<string, BufferInfo> bufferInfo,
                        Dictionary<string, ComputeBuffer> externalBuffers)
    {
        this.shader = shader;

        managedBuffers = new HashSet<string>(bufferInfo.Keys);
        externalDependencies = new HashSet<string>(externalBuffers.Keys);

        bufferToKernelDependencies = new Dictionary<string, HashSet<int>>();

        buffers = new Dictionary<string, ComputeBuffer>();

        InitialiseBuffers(kernelToBufferDependencies, bufferInfo, externalBuffers);
        BindBuffers();
    }

    void InitialiseBuffers(Dictionary<int, string[]> kernelToBufferDependencies, Dictionary<string, BufferInfo> bufferInfo, Dictionary<string, ComputeBuffer> externalBuffers)
    {
        foreach (KeyValuePair<int, string[]> pair in kernelToBufferDependencies)
        {
            foreach (string name in pair.Value)
            {
                if (!bufferToKernelDependencies.ContainsKey(name))
                    bufferToKernelDependencies.Add(name, new HashSet<int>());

                bufferToKernelDependencies[name].Add(pair.Key);

                if (buffers.ContainsKey(name)) continue;

                if (externalDependencies.Contains(name))
                    buffers.Add(name, externalBuffers[name]);
                else
                    CreateBuffer(name, bufferInfo[name]);
            }
        }
    }

    void BindBuffer(string name, ComputeBuffer buffer)
    {
        HashSet<int> dependentKernels = bufferToKernelDependencies[name];

        foreach (int k in dependentKernels)
        {
            Debug.Log($"Binding buffer {k}: {name}");
            shader.SetBuffer(k, name, buffer);
        }
    }

    void BindBuffers()
    {
        foreach (KeyValuePair<string, ComputeBuffer> pair in buffers)
        {
            BindBuffer(pair.Key, pair.Value);
        }
    }

    void CreateBuffer(string name, BufferInfo info)
    {
                ComputeBuffer buffer = new ComputeBuffer(info.Length, info.ElementSize);

                if (info.InitData != null) buffer.SetData(info.InitData);

                buffers.Add(name, buffer);
    }

    public void Destroy()
    {
        foreach (KeyValuePair<string, ComputeBuffer> pair in buffers)
        {
            pair.Value?.Release();
        }
    }

    public void UpdateBuffer(string name, BufferInfo info)
    {
        if (buffers.ContainsKey(name)) {
            buffers[name].Release();
            buffers.Remove(name);
        }

       CreateBuffer(name, info);
       BindBuffer(name, buffers[name]);
    }

    public void UpdateBuffers(Dictionary<string, BufferInfo> buffers)
    {
        foreach (KeyValuePair<string, BufferInfo> pair in buffers)
        {
            UpdateBuffer(pair.Key, pair.Value);
        }
    }
}