using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CorgiGodRays
{
    [System.Serializable]
    public class GodRaysVolume : VolumeComponent, IPostProcessComponent
    {
        public FloatParameter MainLightIntensity = new FloatParameter(0.0f);
        public FloatParameter AdditionalLightsIntensity = new FloatParameter(0.0f);
        public ClampedFloatParameter MainLightScattering = new ClampedFloatParameter(0.5f, -1f, 1f); 
        public ClampedFloatParameter AdditionalLightsScattering = new ClampedFloatParameter(0.5f, -1f, 1f); 
        public ColorParameter Tint = new ColorParameter(Color.white, true, true, true);

        public bool IsActive()
        {
            return true; 
        }

        public bool IsTileCompatible()
        {
            return false; 
        }
    }
}
