using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CorgiGodRays
{
    public class DemoUI : MonoBehaviour
    {
        [Header("Renderer References")]
        public GodRaysRenderFeature feature; 

        [Header("Settings References")]
        public Text frametime;
        public Dropdown VolumeTextureQuality;
        public Dropdown VolumeStepQuality;
        public Dropdown BilateralBlurSamples;
        public Toggle blurToggle;
        public Slider blurSlider;

        private void Start()
        {
            if (feature.settings.textureQuality == GodRaysRenderFeature.VolumeTextureQuality.Low) VolumeTextureQuality.SetValueWithoutNotify(0);
            if (feature.settings.textureQuality == GodRaysRenderFeature.VolumeTextureQuality.Med) VolumeTextureQuality.SetValueWithoutNotify(1);
            if (feature.settings.textureQuality == GodRaysRenderFeature.VolumeTextureQuality.High) VolumeTextureQuality.SetValueWithoutNotify(2);

            if (feature.settings.stepQuality == GodRaysRenderFeature.VolumeStepQuality.Low) VolumeStepQuality.SetValueWithoutNotify(0);
            if (feature.settings.stepQuality == GodRaysRenderFeature.VolumeStepQuality.Med) VolumeStepQuality.SetValueWithoutNotify(1);
            if (feature.settings.stepQuality == GodRaysRenderFeature.VolumeStepQuality.High) VolumeStepQuality.SetValueWithoutNotify(2);

            if (feature.settings.blurSamples == GodRaysRenderFeature.BilateralBlurSamples.Low) BilateralBlurSamples.SetValueWithoutNotify(0);
            if (feature.settings.blurSamples == GodRaysRenderFeature.BilateralBlurSamples.Med) BilateralBlurSamples.SetValueWithoutNotify(1);
            if (feature.settings.blurSamples == GodRaysRenderFeature.BilateralBlurSamples.High) BilateralBlurSamples.SetValueWithoutNotify(2);

            blurToggle.SetIsOnWithoutNotify(feature.settings.blur);
            blurSlider.SetValueWithoutNotify(feature.settings.BlurCount);

            Application.targetFrameRate = -1;

            frametime.text = "...";
        }

        private void Update()
        {
            var timings = new FrameTiming[1];
            FrameTimingManager.CaptureFrameTimings();
            var captured = FrameTimingManager.GetLatestTimings((uint) timings.Length, timings);

            if(captured == 0)
            {
                return;
            }

            var timing = timings[0];

            if (frametime != null)
            {
                frametime.text = $"cpu: {(timing.cpuFrameTime):N2} ms | gpu: {(timing.gpuFrameTime):N2}";
            }
        }

        public void OnDropdownVolumeTextureQuality(int index)
        {
            if (index == 0) feature.settings.textureQuality = GodRaysRenderFeature.VolumeTextureQuality.Low;
            if (index == 1) feature.settings.textureQuality = GodRaysRenderFeature.VolumeTextureQuality.Med;
            if (index == 2) feature.settings.textureQuality = GodRaysRenderFeature.VolumeTextureQuality.High;
            feature.EditorSetDirty();
        }

        public void OnDropdownVolumeStepQuality(int index)
        {
            if (index == 0) feature.settings.stepQuality = GodRaysRenderFeature.VolumeStepQuality.Low;
            if (index == 1) feature.settings.stepQuality = GodRaysRenderFeature.VolumeStepQuality.Med;
            if (index == 2) feature.settings.stepQuality = GodRaysRenderFeature.VolumeStepQuality.High;
            feature.EditorSetDirty();
        }

        public void OnDropdownBilateralBlurSamples(int index)
        {
            if (index == 0) feature.settings.blurSamples = GodRaysRenderFeature.BilateralBlurSamples.Low;
            if (index == 1) feature.settings.blurSamples = GodRaysRenderFeature.BilateralBlurSamples.Med; 
            if (index == 2) feature.settings.blurSamples = GodRaysRenderFeature.BilateralBlurSamples.High;
            feature.EditorSetDirty();
        }

        public void OnToggleblurToggle(bool enabled)
        {
            feature.settings.blur = enabled;
            feature.EditorSetDirty();
        }

        public void OnBlurSlider(float value)
        {
            feature.settings.BlurCount = Mathf.FloorToInt(value);
            feature.EditorSetDirty();
        }
    }
}
