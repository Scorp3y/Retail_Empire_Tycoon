#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CorgiGodRays
{
    [CustomEditor(typeof(GodRaysRenderFeature))]
    public class GodRaysRenderFeatureEditor : Editor
    {
        private SerializedProperty renderData;
        private SerializedProperty Jitter;
        private SerializedProperty textureQuality;
        private SerializedProperty stepQuality;
        private SerializedProperty blur;
        private SerializedProperty BlurCount;
        private SerializedProperty blurSamples;
        private SerializedProperty depthAwareUpsampling;
        private SerializedProperty allowMainLight;
        private SerializedProperty allowAdditionalLights;
        private SerializedProperty allowAdditionalLightShadows;
        private SerializedProperty useUnityDepthDirectly;
        private SerializedProperty supportUnityScreenSpaceShadows;
        private SerializedProperty useVariableIntensity;
        private SerializedProperty variableIntensityCurve;
        private SerializedProperty maxDistance;
        private SerializedProperty encodeLightColor;
        private SerializedProperty enableHighQualityTextures;
        private SerializedProperty AdditionalLightLayers;
        private SerializedProperty maxAdditionalLightCount;
        private SerializedProperty temporallyRender;
        private SerializedProperty temporalDuration;
        private SerializedProperty temporalUseDiscard;
        private SerializedProperty temporalReprojection;
        private SerializedProperty renderOrder;
        private SerializedProperty customRenderPassEvent;
        private SerializedProperty customRenderPassOffset;

        private void OnEnable()
        {
            var settings = serializedObject.FindProperty("settings");
            renderData = settings.FindPropertyRelative("renderData");
            Jitter = settings.FindPropertyRelative("Jitter");
            textureQuality = settings.FindPropertyRelative("textureQuality");
            stepQuality = settings.FindPropertyRelative("stepQuality");
            blur = settings.FindPropertyRelative("blur");
            BlurCount = settings.FindPropertyRelative("BlurCount");
            blurSamples = settings.FindPropertyRelative("blurSamples");
            depthAwareUpsampling = settings.FindPropertyRelative("depthAwareUpsampling");
            allowMainLight = settings.FindPropertyRelative("allowMainLight");
            allowAdditionalLights = settings.FindPropertyRelative("allowAdditionalLights");
            allowAdditionalLightShadows = settings.FindPropertyRelative("allowAdditionalLightShadows");
            useUnityDepthDirectly = settings.FindPropertyRelative("useUnityDepthDirectly");
            supportUnityScreenSpaceShadows = settings.FindPropertyRelative("supportUnityScreenSpaceShadows");
            useVariableIntensity = settings.FindPropertyRelative("useVariableIntensity");
            variableIntensityCurve = settings.FindPropertyRelative("variableIntensityCurve");
            maxDistance = settings.FindPropertyRelative("maxDistance");
            encodeLightColor = settings.FindPropertyRelative("encodeLightColor");
            enableHighQualityTextures = settings.FindPropertyRelative("enableHighQualityTextures");
            AdditionalLightLayers = settings.FindPropertyRelative("AdditionalLightLayers");
            maxAdditionalLightCount = settings.FindPropertyRelative("maxAdditionalLightCount");
            temporallyRender = settings.FindPropertyRelative("temporallyRender");
            temporalDuration = settings.FindPropertyRelative("temporalDuration");
            temporalUseDiscard = settings.FindPropertyRelative("temporalUseDiscard");
            temporalReprojection = settings.FindPropertyRelative("temporalReprojection");
            renderOrder = settings.FindPropertyRelative("renderOrder");
            customRenderPassEvent = settings.FindPropertyRelative("customRenderPassEvent");
            customRenderPassOffset = settings.FindPropertyRelative("customRenderPassOffset");

            
        }

        public override void OnInspectorGUI()
        {
            var instance = (GodRaysRenderFeature) target;

            EditorGUILayout.BeginVertical("GroupBox");
            {
                EditorGUILayout.LabelField("Quality Settings", EditorStyles.boldLabel); 
                EditorGUILayout.PropertyField(maxDistance);
                EditorGUILayout.PropertyField(encodeLightColor);
                EditorGUILayout.PropertyField(enableHighQualityTextures);
                EditorGUILayout.PropertyField(textureQuality);
                if (instance.settings.textureQuality != GodRaysRenderFeature.VolumeTextureQuality.High)
                { 
                    EditorGUILayout.PropertyField(depthAwareUpsampling);
                }
                EditorGUILayout.PropertyField(stepQuality);
                EditorGUILayout.PropertyField(blur);
                if(instance.settings.blur)
                {
                    EditorGUILayout.PropertyField(BlurCount);
                    EditorGUILayout.PropertyField(blurSamples);
                }
                EditorGUILayout.PropertyField(Jitter);

                EditorGUILayout.PropertyField(temporallyRender);
                if(instance.settings.temporallyRender)
                {
                    EditorGUILayout.PropertyField(temporalReprojection);
                    EditorGUILayout.PropertyField(temporalUseDiscard);
                    EditorGUILayout.PropertyField(temporalDuration);
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Feature Settings", EditorStyles.boldLabel); 
                EditorGUILayout.PropertyField(allowMainLight);
                EditorGUILayout.PropertyField(allowAdditionalLights);
                
                if(instance.settings.allowAdditionalLights)
                {
                    EditorGUILayout.PropertyField(maxAdditionalLightCount);
                    EditorGUILayout.PropertyField(allowAdditionalLightShadows);
                    EditorGUILayout.PropertyField(AdditionalLightLayers);
                }

                EditorGUILayout.PropertyField(useUnityDepthDirectly);
                EditorGUILayout.PropertyField(supportUnityScreenSpaceShadows);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Misc Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(renderOrder);
                if(instance.settings.renderOrder == GodRaysRenderFeature.GodraysRenderOrder.Custom)
                {
                    EditorGUILayout.BeginVertical("GroupBox");
                    EditorGUILayout.PropertyField(customRenderPassEvent);
                    EditorGUILayout.PropertyField(customRenderPassOffset);
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.PropertyField(useVariableIntensity);
                if (instance.settings.useVariableIntensity)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        var wasChanged = GUI.changed;
                        EditorGUILayout.PropertyField(variableIntensityCurve);

                        // "Ensures the intensity curve texture's internal texture representation is up-to-date. Press this after making changes. To make changes during runtime, call TriggerRefreshCurveTexture() on your GodRaysRenderFeature."
                        if (GUILayout.Button("refresh", GUILayout.Width(98f)) || (!wasChanged && GUI.changed))
                        {
                            instance.TriggerRefreshCurveTexture();
                        }
                    }
                    EditorGUILayout.EndHorizontal(); 
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Internal Data", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(renderData);

                if(!instance.settings.allowMainLight && !instance.settings.allowAdditionalLights)
                {
                    EditorGUILayout.HelpBox("Both allowMainLight and allowAdditionalLights are disabled, please enable one!", MessageType.Warning); 
                }
            }
            EditorGUILayout.EndVertical();

            if(GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(instance); 
            }
        }
    }
}
#endif