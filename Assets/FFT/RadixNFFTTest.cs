using Rendering.FFT;
using UnityEngine;
using UnityEngine.Rendering;

namespace FFT
{
    [ExecuteAlways]
    public class RadixNFFTTest : MonoBehaviour
    {
        public FFTKernel kernel = new FFTKernel();
        public RenderTexture input;
        public RenderTexture spectral;
        public RenderTexture output;
        private RenderTexture tex;
        public Texture source;
        private bool inverse;
        public bool copy;
        private Vector2Int size;
    
        public FFTKernel.FFTSize newSize = FFTKernel.FFTSize.Size729;
        public bool inplace = false;
        public bool padding = false;


        void OnEnable()
        {
            Init();
        }

        private void Start()
        {
            Init();
        }

        void Init()
        {
            kernel.Init();
            // size = new(1024, 1024);
            size = new(0, 0);
            UpdateSize();
        }

        private void OnDestroy()
        {
            if(tex) tex.Release();
        }
    
        void UpdateSize()
        {
            if (size.x != (int)newSize)
            {
                size.x = (int)newSize;
                size.y = (int)newSize;
                if(tex) tex.Release();
                tex = new RenderTexture(size.x, size.y, 0,
                    RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                tex.wrapMode = input.wrapMode;
                tex.filterMode = input.filterMode;
                tex.enableRandomWrite = true;
                tex.Create();
            }
        }

        public bool inout_target = true;

        ProfilingSampler profiler = new ProfilingSampler("FFT Operation");

        void FFT(CommandBuffer cmd)
        {
            kernel.Inplace = inplace;
            kernel.Padding = padding;
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


        // Update is called once per frame
        void Update()
        {
            UpdateSize();
            kernel.SqrtNormalize = true;
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "FFT Command Buffer";
        
            if (copy)
                cmd.Blit(source, input);
            if (inout_target)
                cmd.Blit(input, tex);

            inverse = false;
            using (new ProfilingScope(cmd, profiler))
            {
                FFT(cmd);
            }
        
            cmd.Blit(tex, spectral);

            inverse = true;
            using (new ProfilingScope(cmd, profiler))
            {
                FFT(cmd);
            }
        
            cmd.Blit(tex, output);

            Graphics.ExecuteCommandBuffer(cmd);
        }
    }
}