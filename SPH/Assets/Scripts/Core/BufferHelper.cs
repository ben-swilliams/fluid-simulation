#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

public struct BufferInfo
{
    public int Length;
    public int ElementSize;
    public Array? InitData;
}

public class BufferHelper
{
    ComputeShader shader;

    Dictionary<string, HashSet<int>> bufferToKernelDependencies;
    Dictionary<string, ComputeBuffer> buffers;

    public BufferHelper(ComputeShader shader,
                        Dictionary<int, string[]> kernelToBufferDependencies,
                        Dictionary<string, BufferInfo> bufferInfo,
                        Dictionary<string, ComputeBuffer> externalBuffers)
    {
        this.shader = shader;

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
            }
        }

        foreach (string name in bufferInfo.Keys)
        {
            CreateBuffer(name, bufferInfo[name]);
        }

        foreach (KeyValuePair<string, ComputeBuffer> external in externalBuffers)
        {
            if (!buffers.ContainsKey(external.Key))
                buffers.Add(external.Key, external.Value);
        }
    }

    void BindBuffer(string name, ComputeBuffer buffer)
    {
        if (!bufferToKernelDependencies.ContainsKey(name)) return;

        HashSet<int> dependentKernels = bufferToKernelDependencies[name];

        foreach (int k in dependentKernels)
        {
            if (buffer == null) continue;
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

    void ReleaseBuffer(string name)
    {
        if (buffers.ContainsKey(name) && buffers[name] != null)
        {
            buffers[name].Release();
            buffers.Remove(name);
        }
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
        ReleaseBuffer(name);

        CreateBuffer(name, info);
        BindBuffer(name, buffers[name]);
    }

    public void UpdateBuffer(string name, ComputeBuffer buffer)
    {
        ReleaseBuffer(name);

        buffers[name] = buffer;
        BindBuffer(name, buffers[name]);
    }

    public void UpdateBuffers(Dictionary<string, BufferInfo> buffers)
    {
        foreach (KeyValuePair<string, BufferInfo> pair in buffers)
        {
            UpdateBuffer(pair.Key, pair.Value);
        }
    }

    public ComputeBuffer? RetrieveBuffer(string name)
    {
        if (buffers.ContainsKey(name)) return buffers[name];

        return null;
    }
}