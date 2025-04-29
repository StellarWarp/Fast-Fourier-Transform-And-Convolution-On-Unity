using Rendering.FFT;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace FFT
{
    [ExecuteAlways]
    public class RadixNFFTBenchmark : MonoBehaviour
    {
        public FFTKernel kernel = new FFTKernel();
        private RenderTexture rwTarget;
        private RenderTexture readSource;

        [FormerlySerializedAs("size")] public Vector2Int currentSize;

        // Start is called before the first frame update
        public FFTKernel.FFTSize sizeX = FFTKernel.FFTSize.Size729;
        public FFTKernel.FFTSize sizeY = FFTKernel.FFTSize.Size729;
        public Vector2 fftPadding = new Vector2(0.1f, 0.1f);
        public bool sameSize = false;
        public bool profileConvolution = false;
        public bool grayScale = false;
        public bool paddingOptimization = false;
        public bool dispatchMegre = false;
        public bool inplace = false;
        public bool padding = false;
        public bool threadRemap = false;
        public bool shaderHalfPrecision = true;
        public bool textureHalfPrecision = true;

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
            currentSize = new(0, 0);
            UpdateSize();
        }

        private void OnDestroy()
        {
            rwTarget?.Release();
            readSource?.Release();
        }

        void UpdateSize()
        {
            int verticalPadding = Mathf.FloorToInt((int)sizeY* fftPadding.y);
            int targetTexHeight = paddingOptimization ? (int)sizeY - 2 * verticalPadding : (int)sizeY;
            if (currentSize.x != (int)sizeX || currentSize.y != (int)sizeY || rwTarget.height != targetTexHeight || 
                (rwTarget.format == RenderTextureFormat.ARGBHalf && !textureHalfPrecision) ||
                (rwTarget.format == RenderTextureFormat.ARGBFloat && textureHalfPrecision))
            {
                if (sameSize)
                {
                    if (currentSize.x != (int)sizeX) sizeY = sizeX;
                    if (currentSize.y != (int)sizeY) sizeX = sizeY;
                }
                currentSize.x = (int)sizeX;
                currentSize.y = (int)sizeY;
                var format = textureHalfPrecision ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;
                if (rwTarget) rwTarget.Release();
                rwTarget = new RenderTexture(currentSize.x, targetTexHeight, 0,
                    format, RenderTextureReadWrite.Linear);
                rwTarget.enableRandomWrite = true;
                rwTarget.Create();
                if (readSource) readSource.Release();
                readSource = new RenderTexture(currentSize.x, currentSize.y, 0,
                    format, RenderTextureReadWrite.Linear);
                // readSource.enableRandomWrite = true;
                readSource.Create();
            }
        }


        ProfilingSampler profilerFft = new ProfilingSampler("FFT Profile");

        private ProfilingSampler profilerConv = new ProfilingSampler("FFT Convolution");

        // Update is called once per frame
        public int testScale = 10;

        void Update()
        {
            UpdateSize();
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "FFT Command Buffer";
            // kernel.HalfPrecision = shaderHalfPrecision;
            kernel.Inplace = inplace;
            kernel.Padding = padding;
            kernel.ThreadRemap = threadRemap;
            if (profileConvolution)
            {
                using (new ProfilingScope(cmd, profilerConv))
                {
                    if (dispatchMegre)
                    {
                        int paddingY = (currentSize.y - rwTarget.height) / 2;
                        var verticalRange = paddingOptimization? new Vector2Int(0, rwTarget.height) : Vector2Int.zero;
                        var offset = paddingOptimization? new Vector2Int(0, -paddingY): Vector2Int.zero;
                    
                        for (int i = 0; i < testScale; i++)
                            kernel.ConvolveOpt(cmd, rwTarget, readSource,
                                currentSize,
                                Vector2Int.zero, 
                                verticalRange, 
                                offset, 
                                grayScale);
                    }
                    else
                    {
                        if (grayScale)
                            for (int i = 0; i < testScale; i++)
                                kernel.GrayScaleConvolve(cmd, rwTarget, readSource);
                        else
                            for (int i = 0; i < testScale; i++)
                                kernel.Convolve(cmd, rwTarget, readSource);
                    }
                }
            }
            else
            {
                using (new ProfilingScope(cmd, profilerFft))
                {
                    for (int i = 0; i < testScale; i++)
                        kernel.FFT(rwTarget, cmd);
                    for (int i = 0; i < testScale; i++)
                        kernel.IFFT(rwTarget, cmd);
                }
            }

            Graphics.ExecuteCommandBuffer(cmd);
        }
    }
}