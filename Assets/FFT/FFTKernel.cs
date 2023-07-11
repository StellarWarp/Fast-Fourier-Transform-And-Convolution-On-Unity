using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rendering.FFT
{
    [Serializable]
    public class FFTKernel
    {
        private ComputeShader fftShader;
        private int fftKernel;
        private ComputeShader convolveShader;
        private int convolveKernel;
        private int kernelGenerateKernel;
        GroupSize convolveGroupSize;
        DispatchSize convolveDispatchSize;
        GroupSize kernelGenerateGroupSize;
        DispatchSize kernelGenerateDispatchSize;

        private struct GroupSize
        {
            public uint x;
            public uint y;
            public uint z;

            public GroupSize(ComputeShader shader, int kernel)
            {
                shader.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            }
        }

        private struct DispatchSize
        {
            public int x;
            public int y;
            public int z;

            public void Update(GroupSize groupSize, Vector3Int threadSize)
            {
                x = Mathf.CeilToInt(threadSize.x / (float)groupSize.x);
                y = Mathf.CeilToInt(threadSize.y / (float)groupSize.y);
                z = Mathf.CeilToInt(threadSize.z / (float)groupSize.z);
            }
        }


        private LocalKeyword keywordSizeX;
        private LocalKeyword keywordSizeY;
        private LocalKeyword keywordVertical;
        private LocalKeyword keywordInverse;
        private LocalKeyword keywordHalfPrecision;
        private LocalKeyword keywordInoutTarget;

        private int sizeX = 0;
        private int sizeY = 0;

        public bool HalfPrecision
        {
            get => fftShader.IsKeywordEnabled(keywordHalfPrecision);
            set
            {
                if (value)
                {
                    fftShader.EnableKeyword(keywordHalfPrecision);
                }
                else
                {
                    fftShader.DisableKeyword(keywordHalfPrecision);
                }
            }
        }

        public void Init()
        {
            fftShader = Resources.Load<ComputeShader>("FFT/FFT_RAIDXN");
            convolveShader = Resources.Load<ComputeShader>("FFT/Convolve");
            fftKernel = fftShader.FindKernel("FFT");
            convolveKernel = convolveShader.FindKernel("Convolve");
            kernelGenerateKernel = convolveShader.FindKernel("GenerateGaussian");

            convolveGroupSize = new GroupSize(convolveShader, convolveKernel);
            kernelGenerateGroupSize = new GroupSize(convolveShader, kernelGenerateKernel);

            keywordInverse = fftShader.keywordSpace.FindKeyword("INVERSE");
            keywordVertical = fftShader.keywordSpace.FindKeyword("VERTICAL");
            keywordHalfPrecision = fftShader.keywordSpace.FindKeyword("HAFT_PRECISION");
            keywordInoutTarget = fftShader.keywordSpace.FindKeyword("INOUT_TARGET");
            UpdateSize(256, 256);
        }

        void UpdateSize(int width, int height)
        {
            if (width != sizeX)
            {
                sizeX = width;
                keywordSizeX = fftShader.keywordSpace.FindKeyword($"SIZE_{sizeX}");
                convolveDispatchSize.Update(convolveGroupSize, new(sizeX, sizeY, 1));
                kernelGenerateDispatchSize.Update(kernelGenerateGroupSize, new(sizeX, sizeY, 1));
            }

            if (height != sizeY)
            {
                sizeY = height;
                keywordSizeY = fftShader.keywordSpace.FindKeyword($"SIZE_{sizeY}");
                convolveDispatchSize.Update(convolveGroupSize, new(sizeX, sizeY, 1));
                kernelGenerateDispatchSize.Update(kernelGenerateGroupSize, new(sizeX, sizeY, 1));
            }
        }

        void HorizontalFFT()
        {
            fftShader.EnableKeyword(keywordSizeX);
            fftShader.Dispatch(fftKernel, 1, sizeY, 1);
            fftShader.DisableKeyword(keywordSizeX);
        }

        void VerticalFFT()
        {
            fftShader.EnableKeyword(keywordSizeY);
            fftShader.EnableKeyword(keywordVertical);
            fftShader.Dispatch(fftKernel, 1, sizeX, 1);
            fftShader.DisableKeyword(keywordVertical);
            fftShader.DisableKeyword(keywordSizeY);
        }

        void InternalFFT(RenderTexture target)
        {
            fftShader.EnableKeyword(keywordInoutTarget);
            fftShader.SetTexture(fftKernel, "Target", target);
            UpdateSize(target.width, target.height);
            HorizontalFFT();
            VerticalFFT();
        }

        void InternalFFT(RenderTexture source, RenderTexture target)
        {
            fftShader.DisableKeyword(keywordInoutTarget);
            fftShader.SetTexture(fftKernel, "Source", source);
            fftShader.SetTexture(fftKernel, "Target", target);
            UpdateSize(target.width, target.height);

            HorizontalFFT();
            Graphics.Blit(target, source);
            VerticalFFT();
        }

        void InternalFFT(RenderTexture texture, CommandBuffer cmd)
        {
            fftShader.EnableKeyword(keywordInoutTarget);
            cmd.SetComputeTextureParam(fftShader, fftKernel, "Target", texture);
            UpdateSize(texture.width, texture.height);

            cmd.EnableKeyword(fftShader, keywordSizeX);
            cmd.DispatchCompute(fftShader, fftKernel, 1, sizeY, 1);
            cmd.DisableKeyword(fftShader, keywordSizeX);

            cmd.EnableKeyword(fftShader, keywordSizeY);
            cmd.EnableKeyword(fftShader, keywordVertical);
            cmd.DispatchCompute(fftShader, fftKernel, 1, sizeX, 1);
            cmd.DisableKeyword(fftShader, keywordVertical);
            cmd.DisableKeyword(fftShader, keywordSizeY);
        }

        public void FFT(RenderTexture source, RenderTexture target)
        {
            fftShader.DisableKeyword(keywordInverse);
            InternalFFT(source, target);
        }

        public void IFFT(RenderTexture source, RenderTexture target)
        {
            fftShader.EnableKeyword(keywordInverse);
            InternalFFT(source, target);
        }

        public void FFT(RenderTexture target)
        {
            fftShader.DisableKeyword(keywordInverse);
            InternalFFT(target);
        }

        public void IFFT(RenderTexture target)
        {
            fftShader.EnableKeyword(keywordInverse);
            InternalFFT(target);
        }

        public void FFT(RenderTexture texture, CommandBuffer cmd)
        {
            cmd.DisableKeyword(fftShader, keywordInverse);
            InternalFFT(texture, cmd);
        }

        public void IFFT(RenderTexture texture, CommandBuffer cmd)
        {
            cmd.EnableKeyword(fftShader, keywordInverse);
            InternalFFT(texture, cmd);
        }

        public void Convolve(CommandBuffer cmd, RenderTexture texture, RenderTexture filter)
        {
            FFT(texture, cmd);
            if (texture.width != filter.width || texture.height != filter.height)
            {
                throw new Exception("Texture size not match");
            }

            cmd.SetComputeTextureParam(convolveShader, convolveKernel, "Target", texture);
            cmd.SetComputeTextureParam(convolveShader, convolveKernel, "Filter", filter);
            cmd.SetComputeIntParams(convolveShader, "TargetSize", texture.width, texture.height);
            cmd.DispatchCompute(convolveShader, convolveKernel,
                convolveDispatchSize.x, convolveDispatchSize.y / 2 + 1, 1);
            IFFT(texture, cmd);
        }

        public void ComputeFilter(CommandBuffer cmd, RenderTexture texture, Vector2 sigma, float scale,
            Vector2Int imgSize)
        {
            cmd.SetComputeIntParams(convolveShader, "TargetSize", texture.width, texture.height);
            cmd.SetComputeIntParams(convolveShader, "ImageSize", imgSize.x, imgSize.y);
            cmd.SetComputeVectorParam(convolveShader, "Sigma", sigma);
            cmd.SetComputeFloatParam(convolveShader, "Factor", scale);
            cmd.SetComputeTextureParam(convolveShader, kernelGenerateKernel, "Target", texture);

            cmd.DispatchCompute(convolveShader, kernelGenerateKernel,
                kernelGenerateDispatchSize.x, kernelGenerateDispatchSize.y, 1);
            FFT(texture, cmd);
        }
    }
}