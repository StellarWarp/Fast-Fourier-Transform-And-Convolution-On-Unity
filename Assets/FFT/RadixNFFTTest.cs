using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Rendering.FFT;

[ExecuteAlways]
public class RadixNFFTTest : MonoBehaviour
{
    public FFTKernel kernel;
    public RenderTexture input;
    public RenderTexture output;
    private RenderTexture tex;
    public Texture source;
    public bool inverse;
    public bool copy;
    private Vector2Int size;

    public float factor = 1;

    // Start is called before the first frame update


    void OnEnable()
    {
        kernel.Init();
        size = new(1024, 1024);
        tex = new RenderTexture(size.x, size.y, 0,
            RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        tex.wrapMode = input.wrapMode;
        tex.filterMode = input.filterMode;
        tex.enableRandomWrite = true;
        tex.Create();
    }

    private void Start()
    {
        kernel.Init();
    }

    private void OnDestroy()
    {
        tex.Release();
    }

    bool inout_target = true;

    ProfilingSampler profiler = new ProfilingSampler("FFT Operation");

    void FFT(CommandBuffer cmd)
    {
        kernel.HalfPrecision = true;
        if (inverse)
        {
            if (inout_target)
                kernel.IFFT(tex, cmd);
            else
                kernel.IFFT(input, tex);
        }
        else
        {
            if (inout_target)
                kernel.FFT(tex, cmd);
            else
                kernel.FFT(input, tex);
        }
    }

    public bool test = false;

    // Update is called once per frame
    void Update()
    {
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "FFT Command Buffer";
        if (copy)
            cmd.Blit(source, input);
        if (inout_target)
            cmd.Blit(input, tex);

        using (new ProfilingScope(cmd, profiler))
        {
            if (test)
                for (int i = 0; i < 20; i++)
                    FFT(cmd);
            else
                FFT(cmd);
        }


        cmd.Blit(tex, output);

        Graphics.ExecuteCommandBuffer(cmd);
    }
}