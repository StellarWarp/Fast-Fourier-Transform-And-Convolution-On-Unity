using System;
using Rendering.FFT;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public sealed class FFTSizeParameter : VolumeParameter<FFTKernel.FFTSize>
    {
        public FFTSizeParameter(FFTKernel.FFTSize value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Serializable, VolumeComponentMenu("Addition-Post-Processing/Convolution Bloom")]
    public sealed class ConvolutionBloom : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter isActive = new(false);

        public FFTSizeParameter convolutionSizeX = new(FFTKernel.FFTSize.Size512);
        public FFTSizeParameter convolutionSizeY = new(FFTKernel.FFTSize.Size512);

        public FloatParameter threshold = new(0.8f);
        public FloatParameter thresholdKnee = new(0.5f);
        public FloatParameter clampMax = new(65472f);

        public MinFloatParameter intensity = new(1.0f, 0);
        public Vector2Parameter fftExtend = new(new Vector2(0.1f, 0.1f));
        public BoolParameter grayScaleConvolve = new(false);
        public BoolParameter disableDispatchMergeOptimization = new(false);
        public BoolParameter disableReadWriteOptimization = new(false);

        public BoolParameter halfPrecisionTexture = new(true);
        public BoolParameter updateOTF = new(false);
        public BoolParameter generatePSF = new(false);
        public TextureParameter imagePSF = new(null);
        public FloatParameter imagePSFScaler = new(1.0f);
        public FloatParameter imagePSFMinClamp = new(0.0f);
        public FloatParameter imagePSFMaxClamp = new(65472f);
        public FloatParameter imagePSFPow = new(1f);


        //debug
        public BoolParameter showDownSampleResult = new(false);
        public RenderTextureParameter outPSF = new(null);
        public RenderTextureParameter outImageInput = new(null);
        public RenderTextureParameter outFFTInput = new(null);
        public RenderTextureParameter outBloomImage = new(null);
        public RenderTextureParameter outImageOutput = new(null);

        public bool IsActive()
        {
            return isActive.value;
        }

        public bool IsTileCompatible()
        {
            return false;
        }

        public bool IsParamUpdated()
        {
            return updateOTF.value;
        }
    }
}