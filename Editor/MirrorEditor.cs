using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace kTools.Mirrors.Editor
{
    using Editor = UnityEditor.Editor;

    [CustomEditor(typeof(Mirror)), CanEditMultipleObjects]
    sealed class MirrorEditor : Editor
    {
#region Structs
        struct Styles
        {
            // Properties
            public static readonly GUIContent TextureScale = new GUIContent("Texture Scale",
                "DecalData defining options and inputs for this Decal.");

            public static readonly GUIContent Offset = new GUIContent("Offset",
                "DecalData defining options and inputs for this Decal.");

            public static readonly GUIContent LayerMask = new GUIContent("Layer Mask",
                "DecalData defining options and inputs for this Decal.");
        }

        struct PropertyNames
        {
            public static readonly string TextureScale = "m_TextureScale";
            public static readonly string Offset = "m_Offset";
            public static readonly string LayerMask = "m_LayerMask";
        }
#endregion

#region Fields
        Mirror m_Target;

        // Properties
        SerializedProperty m_TextureScaleProp;
        SerializedProperty m_OffsetProp;
        SerializedProperty m_LayerMaskProp;
#endregion

#region State
        void OnEnable()
        {
            // Set data
            m_Target = target as Mirror;

            // Get Properties
            m_TextureScaleProp = serializedObject.FindProperty(PropertyNames.TextureScale);
            m_OffsetProp = serializedObject.FindProperty(PropertyNames.Offset);
            m_LayerMaskProp = serializedObject.FindProperty(PropertyNames.LayerMask);
        }
#endregion

#region GUI
        public override void OnInspectorGUI()
        {
            // Setup
            serializedObject.Update();

            // Texture Scale
            EditorGUI.BeginChangeCheck();
            var textureScale = EditorGUILayout.Slider(Styles.TextureScale, m_TextureScaleProp.floatValue, 0, 1);
            if(EditorGUI.EndChangeCheck())
            {
                m_TextureScaleProp.floatValue = textureScale;
            }

            // Clip Plane Offset
            EditorGUILayout.PropertyField(m_OffsetProp, Styles.Offset);

            // Layer Mask
            EditorGUI.BeginChangeCheck();
            LayerMask tempMask = EditorGUILayout.MaskField(Styles.LayerMask, (LayerMask)m_LayerMaskProp.intValue, InternalEditorUtility.layers);
            if(EditorGUI.EndChangeCheck())
            {
                m_LayerMaskProp.intValue = (int)tempMask;
            }

            // Finalize
            serializedObject.ApplyModifiedProperties();
        }
#endregion
    }
}