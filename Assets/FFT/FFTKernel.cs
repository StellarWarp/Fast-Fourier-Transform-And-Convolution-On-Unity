using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;

namespace Rendering.FFT
{
    [Serializable]
    public class FFTKernel
    {
        public enum FFTSize
        {
            None = 0,
            Size16 = 16,
            Size32 = 32,
            Size64 = 64,
            Size256 = 256,
            Size512 = 512,
            Size729 = 729,
            Size972 = 972,
            Size1024 = 1024,
            Size1296 = 1296,
            Size1620 = 1620,
            Size2048 = 2048,
        }

        private ComputeShader fftShader;
        private int fftKernel;
        private int Convolution1DKernel;
        private int Convolution2DKernel;

        private ComputeShader convolveShader;
        private int convolveKernel;
        private int grayScaleConvolveKernel;
        GroupSize convolveGroupSize;
        DispatchSize convolveDispatchSize;

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
        private LocalKeyword keywordForward;
        private LocalKeyword keywordInverse;
        private LocalKeyword keywordConvolution1D;
        private LocalKeyword keywordConvolution2D;
        private LocalKeyword keywordHalfPrecision;
        private LocalKeyword keywordInoutTarget;
        private LocalKeyword keywordInplace;
        private LocalKeyword keywordPadding;
        private LocalKeyword keywordThreadRemap;
        private LocalKeyword keywordSqrtNormalize;
        private LocalKeyword keywordReadBlock;
        private LocalKeyword keywordWriteBlock;
        private LocalKeyword keywordRWShift;

        private int sizeX = 0;
        private int sizeY = 0;

        public bool HalfPrecision
        {
            get => false;
            // get => fftShader.IsKeywordEnabled(keywordHalfPrecision);
            // set
            // {
            //     if (value)
            //     {
            //         fftShader.EnableKeyword(keywordHalfPrecision);
            //     }
            //     else
            //     {
            //         fftShader.DisableKeyword(keywordHalfPrecision);
            //     }
            // }
        }

        public bool Inplace
        {
            get => fftShader.IsKeywordEnabled(keywordInplace);
            set
            {
                if (value)
                {
                    fftShader.EnableKeyword(keywordInplace);
                }
                else
                {
                    fftShader.DisableKeyword(keywordInplace);
                }
            }
        }

        public bool Padding
        {
            get => fftShader.IsKeywordEnabled(keywordPadding);
            set
            {
                if (value)
                {
                    fftShader.EnableKeyword(keywordPadding);
                }
                else
                {
                    fftShader.DisableKeyword(keywordPadding);
                }
            }
        }

        public bool ThreadRemap
        {
            get => fftShader.IsKeywordEnabled(keywordThreadRemap);
            set
            {
                if (value)
                {
                    fftShader.EnableKeyword(keywordThreadRemap);
                }
                else
                {
                    fftShader.DisableKeyword(keywordThreadRemap);
                }
            }
        }

        public bool SqrtNormalize
        {
            get => fftShader.IsKeywordEnabled(keywordSqrtNormalize);
            set
            {
                if (value)
                {
                    fftShader.EnableKeyword(keywordSqrtNormalize);
                }
                else
                {
                    fftShader.DisableKeyword(keywordSqrtNormalize);
                }
            }
        }

        public void Init()
        {
            fftShader = Resources.Load<ComputeShader>("Shaders/Compute/FFT/FFT_RAIDXN");
            convolveShader = Resources.Load<ComputeShader>("Shaders/Compute/Convolve");

            fftKernel = fftShader.FindKernel("FFT");
            Convolution1DKernel = fftShader.FindKernel("Convolution1D");
            Convolution2DKernel = fftShader.FindKernel("Convolution2D");

            convolveKernel = convolveShader.FindKernel("Convolve");
            grayScaleConvolveKernel = convolveShader.FindKernel("GrayScaleConvolve");

            convolveGroupSize = new GroupSize(convolveShader, convolveKernel);

            keywordForward = fftShader.keywordSpace.FindKeyword("FORWARD");
            keywordInverse = fftShader.keywordSpace.FindKeyword("INVERSE");
            keywordConvolution1D = fftShader.keywordSpace.FindKeyword("CONVOLUTION_1D");
            keywordConvolution2D = fftShader.keywordSpace.FindKeyword("CONVOLUTION_2D");
            keywordVertical = fftShader.keywordSpace.FindKeyword("VERTICAL");
            keywordHalfPrecision = fftShader.keywordSpace.FindKeyword("HAFT_PRECISION");
            keywordInoutTarget = fftShader.keywordSpace.FindKeyword("INOUT_TARGET");
            keywordInplace = fftShader.keywordSpace.FindKeyword("INPLACE");
            keywordPadding = fftShader.keywordSpace.FindKeyword("PADDING");
            keywordThreadRemap = fftShader.keywordSpace.FindKeyword("THREAD_REMAP");
            keywordSqrtNormalize = fftShader.keywordSpace.FindKeyword("SQRT_NORMALIZE");
            keywordReadBlock = fftShader.keywordSpace.FindKeyword("READ_BLOCK");
            keywordWriteBlock = fftShader.keywordSpace.FindKeyword("WRITE_BLOCK");
            keywordRWShift = fftShader.keywordSpace.FindKeyword("RW_SHIFT");
            UpdateSize(256, 256);
        }

        void UpdateSize(int width, int height)
        {
            if (width != sizeX)
            {
                sizeX = width;
                keywordSizeX = fftShader.keywordSpace.FindKeyword($"SIZE_{sizeX}");
                convolveDispatchSize.Update(convolveGroupSize, new(sizeX, sizeY, 1));
            }

            if (height != sizeY)
            {
                sizeY = height;
                keywordSizeY = fftShader.keywordSpace.FindKeyword($"SIZE_{sizeY}");
                convolveDispatchSize.Update(convolveGroupSize, new(sizeX, sizeY, 1));
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

        void InternalFFT(RenderTexture texture, CommandBuffer cmd, bool sqrtNormalize = false)
        {
            fftShader.EnableKeyword(keywordInoutTarget);
            cmd.SetComputeTextureParam(fftShader, fftKernel, "Target", texture);
            UpdateSize(texture.width, texture.height);

            if (sqrtNormalize) cmd.EnableKeyword(fftShader, keywordSqrtNormalize);

            cmd.EnableKeyword(fftShader, keywordSizeX);
            cmd.DispatchCompute(fftShader, fftKernel, 1, sizeY, 1);
            cmd.DisableKeyword(fftShader, keywordSizeX);

            cmd.EnableKeyword(fftShader, keywordSizeY);
            cmd.EnableKeyword(fftShader, keywordVertical);
            cmd.DispatchCompute(fftShader, fftKernel, 1, sizeX, 1);
            cmd.DisableKeyword(fftShader, keywordVertical);
            cmd.DisableKeyword(fftShader, keywordSizeY);

            if (sqrtNormalize) cmd.DisableKeyword(fftShader, keywordSqrtNormalize);
        }

        public void FFT(RenderTexture source, RenderTexture target)
        {
            fftShader.EnableKeyword(keywordForward);
            InternalFFT(source, target);
            fftShader.DisableKeyword(keywordForward);
        }

        public void IFFT(RenderTexture source, RenderTexture target)
        {
            fftShader.EnableKeyword(keywordInverse);
            InternalFFT(source, target);
            fftShader.DisableKeyword(keywordInverse);
        }

        public void FFT(RenderTexture target)
        {
            fftShader.EnableKeyword(keywordForward);
            InternalFFT(target);
            fftShader.DisableKeyword(keywordForward);
        }

        public void IFFT(RenderTexture target)
        {
            fftShader.EnableKeyword(keywordInverse);
            InternalFFT(target);
            fftShader.DisableKeyword(keywordInverse);
        }

        public void FFT(RenderTexture texture, CommandBuffer cmd, bool sqrtNormalize = false)
        {
            using (new ProfilingScope(cmd, fftProfilingSampler))
            {
                cmd.EnableKeyword(fftShader, keywordForward);
                InternalFFT(texture, cmd, sqrtNormalize);
                cmd.DisableKeyword(fftShader, keywordForward);
            }
        }

        public void IFFT(RenderTexture texture, CommandBuffer cmd, bool sqrtNormalize = false)
        {
            using (new ProfilingScope(cmd, fftProfilingSampler))
            {
                cmd.EnableKeyword(fftShader, keywordInverse);
                InternalFFT(texture, cmd, sqrtNormalize);
                cmd.DisableKeyword(fftShader, keywordInverse);
            }
        }

        private ProfilingSampler fftProfilingSampler = new("FFT Operation");
        private ProfilingSampler SpectrumProfilingSampler = new("FFT Conv - Spectrum Multiplication");
        private ProfilingSampler fftHorizontalProfilingSampler = new("FFT Horizontal");
        private ProfilingSampler mixedProfilingSampler = new("FFT Vertical + Spectrum + IFFT Vertical");

        public void Convolve(CommandBuffer cmd, RenderTexture texture, RenderTexture filter)
        {
            if (texture.width != filter.width || texture.height != filter.height)
            {
                throw new Exception("Texture size not match");
            }
            FFT(texture, cmd);

            convolveGroupSize = new GroupSize(convolveShader, convolveKernel);
            convolveDispatchSize.Update(convolveGroupSize, new(sizeX, sizeY, 1));

            using (new ProfilingScope(cmd, SpectrumProfilingSampler))
            {
                cmd.SetComputeTextureParam(convolveShader, convolveKernel, "Target", texture);
                cmd.SetComputeTextureParam(convolveShader, convolveKernel, "Filter", filter);
                cmd.SetComputeIntParams(convolveShader, "TargetSize", texture.width, texture.height);
                cmd.DispatchCompute(convolveShader, convolveKernel,
                    convolveDispatchSize.x, convolveDispatchSize.y / 2 + 1, 1);
            }

            IFFT(texture, cmd);
        }

        public void GrayScaleConvolve(CommandBuffer cmd, RenderTexture texture, RenderTexture filter)
        {
            FFT(texture, cmd);
            if (texture.width != filter.width || texture.height != filter.height)
            {
                throw new Exception("Texture size not match");
            }

            convolveGroupSize = new GroupSize(convolveShader, convolveKernel);
            convolveDispatchSize.Update(convolveGroupSize, new(sizeX, sizeY, 1));

            using (new ProfilingScope(cmd, SpectrumProfilingSampler))
            {
                cmd.SetComputeTextureParam(convolveShader, grayScaleConvolveKernel, "Target", texture);
                cmd.SetComputeTextureParam(convolveShader, grayScaleConvolveKernel, "Filter", filter);
                cmd.SetComputeIntParams(convolveShader, "TargetSize", texture.width, texture.height);
                cmd.DispatchCompute(convolveShader, grayScaleConvolveKernel,
                    convolveDispatchSize.x, convolveDispatchSize.y, 1);
            }

            IFFT(texture, cmd);
        }

        //warn that logic is not impl for offset.x
        public void ConvolveOpt(CommandBuffer cmd,
            RenderTexture texture,
            RenderTexture filter,
            Vector2Int size,
            Vector2Int horizontalRange,
            Vector2Int verticalRange,
            Vector2Int offset,
            bool grayScale)
        {
            if (size.x != filter.width || size.y != filter.height)
            {
                throw new Exception("Texture size not match");
            }

            int rwRangeBeginX = horizontalRange[0];
            int rwRangeEndX = horizontalRange[1];
            int rwRangeBeginY = verticalRange[0];
            int rwRangeEndY = verticalRange[1];
            bool horizontalReadWriteBlock = horizontalRange != Vector2Int.zero;
            bool vertiacalReadWriteBlock = verticalRange != Vector2Int.zero;
            bool verticalOffset = offset.y != 0;

            cmd.EnableKeyword(fftShader, keywordInoutTarget);
            cmd.SetComputeTextureParam(fftShader, fftKernel, "Target", texture);
            UpdateSize(size.x, size.y);

            int horizontalY = texture.height;

            using (new ProfilingScope(cmd, fftHorizontalProfilingSampler))
            {
                if (horizontalReadWriteBlock)
                {
                    cmd.EnableKeyword(fftShader, keywordReadBlock);
                    cmd.SetComputeIntParams(fftShader, "ReadWriteRangeAndOffset", rwRangeBeginX, rwRangeEndX);
                }

                cmd.EnableKeyword(fftShader, keywordForward);
                cmd.EnableKeyword(fftShader, keywordSizeX);
                cmd.DispatchCompute(fftShader, fftKernel, 1, horizontalY, 1);
                cmd.DisableKeyword(fftShader, keywordSizeX);
                cmd.DisableKeyword(fftShader, keywordForward);
                if (horizontalReadWriteBlock)
                {
                    cmd.DisableKeyword(fftShader, keywordReadBlock);
                }
            }

            // vertiacalReadWriteBlock = false;
            
            using (new ProfilingScope(cmd, mixedProfilingSampler))
            {
                cmd.EnableKeyword(fftShader, keywordSizeY);
                cmd.EnableKeyword(fftShader, keywordVertical);
                if (vertiacalReadWriteBlock || verticalOffset)
                {
                    if (vertiacalReadWriteBlock)
                    {
                        cmd.EnableKeyword(fftShader, keywordReadBlock);
                        cmd.EnableKeyword(fftShader, keywordWriteBlock);
                    }

                    if (verticalOffset)
                    {
                        cmd.EnableKeyword(fftShader, keywordRWShift);
                    }

                    cmd.SetComputeIntParams(fftShader, "ReadWriteRangeAndOffset",
                        rwRangeBeginY,
                        rwRangeEndY,
                        0,
                        offset.y);
                }

                if (grayScale)
                {
                    cmd.EnableKeyword(fftShader, keywordConvolution1D);

                    cmd.SetComputeTextureParam(fftShader, Convolution1DKernel, "Target", texture);
                    cmd.SetComputeTextureParam(fftShader, Convolution1DKernel, "ConvKernelSpectrum", filter);
                    cmd.DispatchCompute(fftShader, Convolution1DKernel, 1, sizeX, 1);

                    cmd.DisableKeyword(fftShader, keywordConvolution1D);
                }
                else
                {
                    cmd.EnableKeyword(fftShader, keywordConvolution2D);
                    cmd.EnableKeyword(fftShader, keywordInplace);

                    cmd.SetComputeIntParams(fftShader, "TargetSize", size.x, size.y);
                    cmd.SetComputeTextureParam(fftShader, Convolution2DKernel, "Target", texture);
                    cmd.SetComputeTextureParam(fftShader, Convolution2DKernel, "ConvKernelSpectrum", filter);
                    cmd.DispatchCompute(fftShader, Convolution2DKernel, 1, (sizeX + 1) / 2, 1);

                    cmd.DisableKeyword(fftShader, keywordInplace);
                    cmd.DisableKeyword(fftShader, keywordConvolution2D);
                }

                cmd.DisableKeyword(fftShader, keywordVertical);
                cmd.DisableKeyword(fftShader, keywordSizeY);
                if (vertiacalReadWriteBlock)
                {
                    cmd.DisableKeyword(fftShader, keywordReadBlock);
                    cmd.DisableKeyword(fftShader, keywordWriteBlock);
                }

                if (verticalOffset)
                {
                    cmd.DisableKeyword(fftShader, keywordRWShift);
                }
            }

            using (new ProfilingScope(cmd, fftHorizontalProfilingSampler))
            {
                if (horizontalReadWriteBlock)
                {
                    cmd.EnableKeyword(fftShader, keywordWriteBlock);
                    cmd.SetComputeIntParams(fftShader, "ReadWriteRangeAndOffset", rwRangeBeginX, rwRangeEndX);
                }

                cmd.EnableKeyword(fftShader, keywordInverse);
                cmd.EnableKeyword(fftShader, keywordSizeX);
                cmd.DispatchCompute(fftShader, fftKernel, 1, horizontalY, 1);
                cmd.DisableKeyword(fftShader, keywordSizeX);
                cmd.DisableKeyword(fftShader, keywordInverse);
                if (horizontalReadWriteBlock)
                {
                    cmd.DisableKeyword(fftShader, keywordWriteBlock);
                }
            }
        }
    }
}