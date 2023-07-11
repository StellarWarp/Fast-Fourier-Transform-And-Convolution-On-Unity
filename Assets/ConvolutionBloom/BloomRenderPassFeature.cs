using Rendering.FFT;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BloomRenderPassFeature : ScriptableRendererFeature
{
    class BloomRenderPass : ScriptableRenderPass
    {
        private FFTKernel fftKernel = new FFTKernel();

        private RenderTexture fftTarget;
        private RenderTexture filter;

        private Material bloomFilterMaterial= new Material(Shader.Find("ConvolutionBloom/Filter"));
        private Material bloomBlendMaterial= new Material(Shader.Find("ConvolutionBloom/Blend"));
        private Material imageKernelMaterial= new Material(Shader.Find("ConvolutionBloom/ImageKernel"));
        
        static class ShaderProperties
        {
            public static readonly int FFTExtend = Shader.PropertyToID("_FFT_EXTEND");
            public static readonly int Threshold = Shader.PropertyToID("_THRESHOLD");
            public static readonly int MaxClamp = Shader.PropertyToID("_MaxClamp");
            public static readonly int MinClamp = Shader.PropertyToID("_MinClamp");
            public static readonly int KernelPow = Shader.PropertyToID("_Power");
            public static readonly int KernelScaler = Shader.PropertyToID("_Scaler");
        }


        public BloomRenderPass()
        {
            fftKernel.Init();
            // fftKernel.HalfPrecision = true;
            fftKernel.HalfPrecision = false;
        }

        void CreateTexture()
        {
            filter = new RenderTexture(512, 512, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            filter.depthStencilFormat = GraphicsFormat.None;
            filter.enableRandomWrite = true;
            filter.Create();
            
            fftTarget = new RenderTexture(512, 512, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            fftTarget.enableRandomWrite = true;
            filter.depthStencilFormat = GraphicsFormat.None;
            fftTarget.Create();
        }

        public void Dispose()
        {
            if (filter) filter.Release();
            if (fftTarget) fftTarget.Release();
            if (!bloomFilterMaterial.IsDestroyed()) Object.DestroyImmediate(bloomFilterMaterial);
            if (!bloomBlendMaterial.IsDestroyed()) Object.DestroyImmediate(bloomBlendMaterial);
            if (!imageKernelMaterial.IsDestroyed()) Object.DestroyImmediate(imageKernelMaterial);
        }

        public void Setup()
        {
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (bloomFilterMaterial.IsDestroyed()) bloomFilterMaterial = new Material(Shader.Find("ConvolutionBloom/Filter"));
            if (bloomBlendMaterial.IsDestroyed()) bloomBlendMaterial   = new Material(Shader.Find("ConvolutionBloom/Blend"));
            if (imageKernelMaterial.IsDestroyed()) imageKernelMaterial = new Material(Shader.Find("ConvolutionBloom/ImageKernel"));
            if (filter == null || fftTarget == null) CreateTexture();
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var stack = VolumeManager.instance.stack;
            var bloomParams = stack.GetComponent<ConvolutionBloom>();
            if (bloomParams == null) return;
            if (!bloomParams.IsActive()) return;
            float threshold = bloomParams.threshold.value;
            float intensity = bloomParams.intensity.value;
            var fftExtend = bloomParams.fftExtend.value;
            var sigma = bloomParams.sigma.value;

            var cmd = CommandBufferPool.Get("Bloom");


            if (bloomParams.IsParamUpdated())
            {
                var targetX = renderingData.cameraData.camera.pixelWidth;
                var targetY = renderingData.cameraData.camera.pixelHeight;
                // fftKernel.ComputeFilter(cmd, filter, sigma, intensity,
                //     new(targetX + (int)(2 * targetX * fftExtend.x), targetY + (int)(2 * targetY * fftExtend.y)));
                ConvolutionKernelUpdate(cmd, bloomParams);
            }

            RenderTargetIdentifier targetColor = renderingData.cameraData.renderer.cameraColorTargetHandle;

            bloomFilterMaterial.SetVector(ShaderProperties.FFTExtend, fftExtend);
            bloomBlendMaterial.SetVector(ShaderProperties.FFTExtend, fftExtend);
            bloomFilterMaterial.SetFloat(ShaderProperties.Threshold, threshold);
            cmd.Blit(targetColor, fftTarget, bloomFilterMaterial);
            fftKernel.Convolve(cmd, fftTarget, filter);
            cmd.Blit(fftTarget, targetColor, bloomBlendMaterial);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void ConvolutionKernelUpdate(CommandBuffer cmd, ConvolutionBloom param)
        {
            imageKernelMaterial.SetFloat(ShaderProperties.MaxClamp, param.kernelMaxClamp.value);
            imageKernelMaterial.SetFloat(ShaderProperties.MinClamp, param.kernelMinClamp.value);
            imageKernelMaterial.SetFloat(ShaderProperties.KernelPow, param.kernelPow.value);
            imageKernelMaterial.SetFloat(ShaderProperties.KernelScaler, param.kernelScaler.value);
            cmd.Blit(param.kernel.value, filter, imageKernelMaterial);
            fftKernel.FFT(filter, cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd) { }
    }

    BloomRenderPass scriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        scriptablePass = new BloomRenderPass();

        // Configures where the render pass should be injected.
        scriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        scriptablePass.Setup();
        renderer.EnqueuePass(scriptablePass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (scriptablePass != null)
        {
            scriptablePass.Dispose();
        }
    }
}