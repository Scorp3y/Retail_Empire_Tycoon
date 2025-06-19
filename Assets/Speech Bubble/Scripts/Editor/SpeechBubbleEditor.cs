using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
namespace SpeechBubble
{
    [CustomEditor(typeof(SpeechBubble_TMP))]
    public class SpeechBubbleEditorTMP : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            SpeechBubble_TMP script = (SpeechBubble_TMP)target;

            if (GUILayout.Button("Apply Changes"))
            {
                script.updateEditor();
            }

        }

    }

    [CustomEditor(typeof(SpeechBubble_Legacy))]
    public class SpeechBubbleEditorLegacy : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            SpeechBubble_Legacy script = (SpeechBubble_Legacy)target;

            if (GUILayout.Button("Apply Changes"))
            {
                script.updateEditor();
            }

        }

    }


}
#endif
