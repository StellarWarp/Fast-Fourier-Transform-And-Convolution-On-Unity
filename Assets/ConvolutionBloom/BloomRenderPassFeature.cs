using Rendering.FFT;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;

namespace ConvolutionBloom
{
    public class BloomRenderPassFeature : ScriptableRendererFeature
    {
        class BloomRenderPass : ScriptableRenderPass
        {
            private FFTKernel fftKernel = new FFTKernel();

            private FFTKernel.FFTSize convolutionSizeX = FFTKernel.FFTSize.None;
            private FFTKernel.FFTSize convolutionSizeY = FFTKernel.FFTSize.None;
            private RenderTexture fftTarget;
            private RenderTexture PSF;
            private RenderTexture OTF;

            private Material brightMaskMaterial = new Material(Shader.Find("ConvolutionBloom/BrightMask"));
            private Material bloomBlendMaterial = new Material(Shader.Find("ConvolutionBloom/Blend"));
            private Material PSFRemapMaterial = new Material(Shader.Find("ConvolutionBloom/PsfRemap"));
            private Material PSFGeneratorMaterial = new Material(Shader.Find("ConvolutionBloom/PsfGenerator"));

            static class ShaderProperties
            {
                public static readonly int FFTExtend = Shader.PropertyToID("_FFT_EXTEND");
                public static readonly int Threshold = Shader.PropertyToID("_Threshlod");
                public static readonly int ThresholdKnee = Shader.PropertyToID("_ThresholdKnee");
                public static readonly int TexelSize = Shader.PropertyToID("_TexelSize");
                public static readonly int MaxClamp = Shader.PropertyToID("_MaxClamp");
                public static readonly int MinClamp = Shader.PropertyToID("_MinClamp");
                public static readonly int KernelPow = Shader.PropertyToID("_Power");
                public static readonly int KernelScaler = Shader.PropertyToID("_Scaler");
                public static readonly int GrayScale = Shader.PropertyToID("_GrayScale");
                public static readonly int ScreenX = Shader.PropertyToID("_ScreenX");
                public static readonly int ScreenY = Shader.PropertyToID("_ScreenY");
                public static readonly int EnableRemap = Shader.PropertyToID("_EnableRemap");
                public static readonly int Intensity = Shader.PropertyToID("_Intensity");
            }


            public BloomRenderPass()
            {
                fftKernel.Init();
            }

            void UpdateRenderTextureSize(UnityEngine.Rendering.Universal.ConvolutionBloom bloomParam)
            {
                var sizeX = bloomParam.convolutionSizeX.value;
                var sizeY = bloomParam.convolutionSizeY.value;
                int width = (int)sizeX;
                int height = (int)sizeY;
                RenderTextureFormat format = bloomParam.halfPrecisionTexture.value ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;

                int verticalPadding = Mathf.FloorToInt(height * bloomParam.fftExtend.value.y);
                int targetTexHeight = bloomParam.disableReadWriteOptimization.value ? height : height - 2 * verticalPadding;
                if (!OTF || !fftTarget || !PSF || convolutionSizeX != sizeX
                    || convolutionSizeY != sizeY || fftTarget.height != targetTexHeight
                    || fftTarget.format != format)
                {
                    convolutionSizeX = sizeX;
                    convolutionSizeY = sizeY;

                    if (!OTF || OTF.width != width || OTF.height != height || OTF.format != format)
                    {
                        if (OTF) OTF.Release();
                        OTF = new RenderTexture(width, height, 0,
                            format, RenderTextureReadWrite.Linear);
                        OTF.depthStencilFormat = GraphicsFormat.None;
                        OTF.enableRandomWrite = true;
                        OTF.Create();
                    }

                    if (!fftTarget || fftTarget.width != width || fftTarget.height != targetTexHeight || fftTarget.format != format)
                    {
                        if (fftTarget) fftTarget.Release();
                        fftTarget = new RenderTexture(width, targetTexHeight, 0,
                            format, RenderTextureReadWrite.Linear);
                        fftTarget.depthStencilFormat = GraphicsFormat.None;
                        fftTarget.wrapMode = TextureWrapMode.Clamp;
                        fftTarget.enableRandomWrite = true;
                        fftTarget.Create();
                    }

                    if (!PSF || PSF.width != width || PSF.height != height || PSF.format != format)
                    {
                        if (PSF) PSF.Release();
                        PSF = new RenderTexture(width, height, 0,
                            format, RenderTextureReadWrite.Linear);
                        PSF.depthStencilFormat = GraphicsFormat.None;
                        PSF.enableRandomWrite = true;
                        PSF.Create();
                    }
                }
            }

            public void Dispose()
            {
                if (PSF) PSF.Release();
                if (OTF) OTF.Release();
                if (fftTarget) fftTarget.Release();
                if (!brightMaskMaterial.IsDestroyed()) Object.DestroyImmediate(brightMaskMaterial);
                if (!bloomBlendMaterial.IsDestroyed()) Object.DestroyImmediate(bloomBlendMaterial);
                if (!PSFRemapMaterial.IsDestroyed()) Object.DestroyImmediate(PSFRemapMaterial);
                if (!PSFGeneratorMaterial.IsDestroyed()) Object.DestroyImmediate(PSFGeneratorMaterial);
            }

            public void Setup() { }

            // This method is called before executing the render pass.
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in a performant manner.
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (brightMaskMaterial.IsDestroyed()) brightMaskMaterial = new Material(Shader.Find("ConvolutionBloom/BrightMask"));
                if (bloomBlendMaterial.IsDestroyed()) bloomBlendMaterial = new Material(Shader.Find("ConvolutionBloom/Blend"));
                if (PSFRemapMaterial.IsDestroyed()) PSFRemapMaterial = new Material(Shader.Find("ConvolutionBloom/PsfRemap"));
                if (PSFGeneratorMaterial.IsDestroyed()) PSFGeneratorMaterial = new Material(Shader.Find("ConvolutionBloom/PsfGenerator"));
            }

            ProfilingSampler bloomProfiler = new ProfilingSampler("Convolution Bloom");


            ProfilingSampler profiler = new ProfilingSampler("Convolution");

            // Here you can implement the rendering logic.
            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var stack = VolumeManager.instance.stack;
                var bloomParams = stack.GetComponent<UnityEngine.Rendering.Universal.ConvolutionBloom>();
                if (bloomParams == null) return;
                if (!bloomParams.IsActive()) return;
                float threshold = bloomParams.threshold.value;
                float thresholdKnee = bloomParams.thresholdKnee.value;
                float clampMax = bloomParams.clampMax.value;
                float intensity = bloomParams.intensity.value;
                var fftExtend = bloomParams.fftExtend.value;

                UpdateRenderTextureSize(bloomParams);

                var cmd = CommandBufferPool.Get("Bloom Cmd");
                var debug_cmd = CommandBufferPool.Get("Debug Bloom");


                var targetX = renderingData.cameraData.camera.pixelWidth;
                var targetY = renderingData.cameraData.camera.pixelHeight;
                if (bloomParams.IsParamUpdated())
                {
                    OpticalTransferFunctionUpdate(cmd, bloomParams, new Vector2Int(targetX, targetY));
                }

                RenderTargetIdentifier screenTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // for article
                if (bloomParams.outImageInput.value)
                    debug_cmd.Blit(screenTarget, bloomParams.outImageInput.value);

                using (new ProfilingScope(cmd, bloomProfiler))
                {
                    if (!bloomParams.disableReadWriteOptimization.value) fftExtend.y = 0;
                    brightMaskMaterial.SetVector(ShaderProperties.FFTExtend, fftExtend);
                    brightMaskMaterial.SetFloat(ShaderProperties.Threshold, threshold);
                    brightMaskMaterial.SetFloat(ShaderProperties.ThresholdKnee, thresholdKnee);
                    brightMaskMaterial.SetFloat(ShaderProperties.MaxClamp, clampMax);
                    brightMaskMaterial.SetVector(ShaderProperties.TexelSize, new Vector4(1f / targetX, 1f / targetY, 0, 0));
                    cmd.Blit(screenTarget, fftTarget, brightMaskMaterial);

                    if (bloomParams.showDownSampleResult.value)
                        debug_cmd.Blit(fftTarget, screenTarget);
                    // for article
                    if (bloomParams.outFFTInput.value)
                        debug_cmd.Blit(fftTarget, bloomParams.outFFTInput.value);

                    if (!bloomParams.showDownSampleResult.value)
                    {
                        Vector2Int size = new Vector2Int((int)convolutionSizeX, (int)convolutionSizeY);
                        Vector2Int horizontalRange = Vector2Int.zero;
                        Vector2Int verticalRange = Vector2Int.zero;
                        Vector2Int offset = Vector2Int.zero;

                        if (!bloomParams.disableReadWriteOptimization.value)
                        {
                            // int paddingX = Mathf.FloorToInt(size.x * fftExtend.x);
                            int paddingY = (size.y - fftTarget.height) / 2;
                            // horizontalRange = new Vector2Int(paddingX,fftTarget.width -  paddingX);
                            verticalRange = new Vector2Int(0, fftTarget.height);
                            offset = new Vector2Int(0, -paddingY);
                        }

                        if (bloomParams.disableDispatchMergeOptimization.value)
                        {
                            if (bloomParams.grayScaleConvolve.value)
                                fftKernel.GrayScaleConvolve(cmd, fftTarget, OTF);
                            else
                                fftKernel.Convolve(cmd, fftTarget, OTF);
                        }
                        else
                        {
                            fftKernel.ConvolveOpt(cmd, fftTarget, OTF,
                                size,
                                horizontalRange,
                                verticalRange,
                                offset,
                                bloomParams.grayScaleConvolve.value);
                        }


                        // for article
                        if (bloomParams.outBloomImage.value)
                            debug_cmd.Blit(fftTarget, bloomParams.outBloomImage.value);

                        bloomBlendMaterial.SetVector(ShaderProperties.FFTExtend, fftExtend);
                        bloomBlendMaterial.SetFloat(ShaderProperties.Intensity, intensity);
                        cmd.Blit(fftTarget, screenTarget, bloomBlendMaterial);
                    }

                    // for article
                    if (bloomParams.outImageOutput.value)
                        debug_cmd.Blit(screenTarget, bloomParams.outImageOutput.value);
                }

                context.ExecuteCommandBuffer(cmd);
                context.ExecuteCommandBuffer(debug_cmd);
                CommandBufferPool.Release(cmd);
                CommandBufferPool.Release(debug_cmd);
            }

            void OpticalTransferFunctionUpdate(CommandBuffer cmd, UnityEngine.Rendering.Universal.ConvolutionBloom param, Vector2Int size)
            {
                PSFRemapMaterial.SetFloat(ShaderProperties.MaxClamp, param.imagePSFMaxClamp.value);
                PSFRemapMaterial.SetFloat(ShaderProperties.MinClamp, param.imagePSFMinClamp.value);
                PSFRemapMaterial.SetVector(ShaderProperties.FFTExtend, param.fftExtend.value);
                PSFRemapMaterial.SetFloat(ShaderProperties.KernelPow, param.imagePSFPow.value);
                PSFRemapMaterial.SetFloat(ShaderProperties.KernelScaler, param.imagePSFScaler.value);
                PSFRemapMaterial.SetInt(ShaderProperties.GrayScale, param.grayScaleConvolve.value ? 1 : 0);
                PSFRemapMaterial.SetInt(ShaderProperties.ScreenX, size.x);
                PSFRemapMaterial.SetInt(ShaderProperties.ScreenY, size.y);
                if (param.generatePSF.value)
                {
                    PSFGeneratorMaterial.SetVector(ShaderProperties.FFTExtend, param.fftExtend.value);
                    PSFGeneratorMaterial.SetInt(ShaderProperties.ScreenX, size.x);
                    PSFGeneratorMaterial.SetInt(ShaderProperties.ScreenY, size.y);


                    if (param.outPSF.value) // for article
                    {
                        CommandBuffer debug_cmd = CommandBufferPool.Get("Debug Bloom");
                        PSFGeneratorMaterial.SetInt(ShaderProperties.EnableRemap, 0);
                        debug_cmd.Blit(param.outPSF.value, param.outPSF.value, PSFGeneratorMaterial);
                        Graphics.ExecuteCommandBuffer(debug_cmd);
                        CommandBufferPool.Release(debug_cmd);
                    }

                    PSFGeneratorMaterial.SetInt(ShaderProperties.GrayScale, param.grayScaleConvolve.value ? 1 : 0);
                    PSFGeneratorMaterial.SetInt(ShaderProperties.EnableRemap, 1);
                    cmd.Blit(OTF, OTF, PSFGeneratorMaterial);
                }
                else
                {
                    PSFRemapMaterial.SetInt(ShaderProperties.EnableRemap, 1);
                    cmd.Blit(param.imagePSF.value, OTF, PSFRemapMaterial);
                }

                fftKernel.FFT(OTF, cmd);
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
}