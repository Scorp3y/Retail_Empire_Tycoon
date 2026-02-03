using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CorgiGodRays
{
    [CreateAssetMenu(fileName = "GodRaysRenderData", menuName = "GodRaysRenderData")]
    public class GodRaysRenderData : ScriptableObject
    {
        public Material Grabpass;
        public Material DepthGrabpass;
        public Material ScreenSpaceGodRays;
        public Material ApplyGodRays;
        public Material BilateralBlur;
        public ComputeShader TemporalReprojectionShader;

#if UNITY_URP_12_OR_HIGHER 
        public bool StripUnusedShadersFromBuilds = false; // intentionally false by default! 
#endif

#if UNITY_EDITOR
        public static GodRaysRenderData FindData()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:GodRaysRenderData");
            foreach (var guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) continue;
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<GodRaysRenderData>(assetPath);
                if (data != null) return data;
            }

            var newData = GodRaysRenderData.CreateInstance<GodRaysRenderData>();
            UnityEditor.AssetDatabase.CreateAsset(newData, "GodRaysRenderData.asset");

            Debug.Log("GodRaysRenderData not found, so one was created.", newData);

            return newData;
        }
#endif
    }
}
