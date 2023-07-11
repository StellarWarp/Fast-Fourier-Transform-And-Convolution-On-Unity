using System;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenu("Addition-Post-Processing/Convolution Bloom")]
    public sealed class ConvolutionBloom : VolumeComponent, IPostProcessComponent
    {
        
        public BoolParameter isActive = new BoolParameter(false);
        [Tooltip("Filters out pixels under this level of brightness. Value is in gamma-space.")]
        public FloatParameter threshold = new FloatParameter(0.8f);
        [Tooltip("Strength of the bloom filter.")]
        public FloatParameter intensity = new FloatParameter(0.8f);
        public Vector2Parameter sigma = new Vector2Parameter(new Vector2(5,10));
        public  Vector2Parameter fftExtend = new Vector2Parameter(new Vector2(0.1f, 0.1f));
        [Tooltip("Update the parameters of the bloom filter.")]
        public BoolParameter updateKernel = new BoolParameter(false);
        public TextureParameter kernel = new TextureParameter(null);
        public FloatParameter kernelScaler = new FloatParameter(1.0f);
        public FloatParameter kernelPow = new FloatParameter(1f);
        public FloatParameter kernelMinClamp = new FloatParameter(0.0f);
        public FloatParameter kernelMaxClamp = new FloatParameter(1.0f);
        
        
        

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
            return updateKernel.value;
        }
    }
}
