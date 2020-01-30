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
            // Foldouts
            public static readonly GUIContent ProjectionOptions = new GUIContent("Projection Options");
            public static readonly GUIContent OutputOptions = new GUIContent("Output Options");

            // Properties
            public static readonly GUIContent Offset = new GUIContent("Offset",
                "Offset value for oplique near clip plane.");

            public static readonly GUIContent LayerMask = new GUIContent("Layer Mask",
                "Which layers should the Mirror render.");

            public static readonly GUIContent Scope = new GUIContent("Scope",
                "Global output renders to the global texture. Only one Mirror can be global. Local output renders to one texture per Mirror, this is set on all elements of the Renderers list.");

            public static readonly GUIContent Renderers = new GUIContent("Renderers",
                "Renderers to set the reflection texture on.");
            
            public static readonly GUIContent TextureScale = new GUIContent("Texture Scale",
                "Scale value applied to the size of the source camera texture.");

            public static readonly GUIContent HDR = new GUIContent("HDR",
                "Should reflections be rendered in HDR.");

            public static readonly GUIContent MSAA = new GUIContent("MSAA",
                "Should reflections be resolved with MSAA.");
        }

        struct PropertyNames
        {
            public static readonly string Offset = "m_Offset";
            public static readonly string LayerMask = "m_LayerMask";
            public static readonly string Scope = "m_Scope";
            public static readonly string Renderers = "m_Renderers";
            public static readonly string TextureScale = "m_TextureScale";
            public static readonly string AllowHDR = "m_AllowHDR";
            public static readonly string AllowMSAA = "m_AllowMSAA";
        }
#endregion

#region Fields
        const string kEditorPrefKey = "kMirrors:MirrorData:";
        Mirror m_Target;

        // Foldouts
        bool m_ProjectionOptionsFoldout;
        bool m_OutputOptionsFoldout;

        // Properties
        SerializedProperty m_OffsetProp;
        SerializedProperty m_LayerMaskProp;
        SerializedProperty m_ScopeProp;
        SerializedProperty m_RenderersProp;
        SerializedProperty m_TextureScaleProp;
        SerializedProperty m_AllowHDR;
        SerializedProperty m_AllowMSAA;
#endregion

#region State
        void OnEnable()
        {
            // Set data
            m_Target = target as Mirror;

            // Get Properties
            m_OffsetProp = serializedObject.FindProperty(PropertyNames.Offset);
            m_LayerMaskProp = serializedObject.FindProperty(PropertyNames.LayerMask);
            m_ScopeProp = serializedObject.FindProperty(PropertyNames.Scope);
            m_RenderersProp = serializedObject.FindProperty(PropertyNames.Renderers);
            m_TextureScaleProp = serializedObject.FindProperty(PropertyNames.TextureScale);
            m_AllowHDR = serializedObject.FindProperty(PropertyNames.AllowHDR);
            m_AllowMSAA = serializedObject.FindProperty(PropertyNames.AllowMSAA);
        }
#endregion

#region GUI
        public override void OnInspectorGUI()
        {
            // Get foldouts from EditorPrefs
            m_ProjectionOptionsFoldout = GetFoldoutState("ProjectionOptions");
            m_OutputOptionsFoldout = GetFoldoutState("OutputOptions");

            // Setup
            serializedObject.Update();

            // Projection Options
            var projectionOptions = EditorGUILayout.BeginFoldoutHeaderGroup(m_ProjectionOptionsFoldout, Styles.ProjectionOptions);
            if(projectionOptions)
            {
                DrawProjectionOptions();
                EditorGUILayout.Space();
            }
            SetFoldoutState("ProjectionOptions", m_ProjectionOptionsFoldout, projectionOptions);
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Output Options
            var outputOptions = EditorGUILayout.BeginFoldoutHeaderGroup(m_OutputOptionsFoldout, Styles.OutputOptions);
            if(outputOptions)
            {
                DrawOutputOptions();
                EditorGUILayout.Space();
            }
            SetFoldoutState("OutputOptions", m_OutputOptionsFoldout, outputOptions);
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Finalize
            serializedObject.ApplyModifiedProperties();
        }

        void DrawProjectionOptions()
        {
            // Clip Plane Offset
            EditorGUILayout.PropertyField(m_OffsetProp, Styles.Offset);

            // Layer Mask
            EditorGUI.BeginChangeCheck();
            LayerMask tempMask = EditorGUILayout.MaskField(Styles.LayerMask, (LayerMask)m_LayerMaskProp.intValue, InternalEditorUtility.layers);
            if(EditorGUI.EndChangeCheck())
            {
                m_LayerMaskProp.intValue = (int)tempMask;
            }
        }

        void DrawOutputOptions()
        {
            // Scope
            EditorGUILayout.PropertyField(m_ScopeProp, Styles.Scope);

            // Renderers
            if(m_ScopeProp.enumValueIndex == (int)Mirror.OutputScope.Local)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_RenderersProp, Styles.Renderers);
                EditorGUI.indentLevel--;
            }

            // Texture Scale
            EditorGUI.BeginChangeCheck();
            var textureScale = EditorGUILayout.Slider(Styles.TextureScale, m_TextureScaleProp.floatValue, 0, 1);
            if(EditorGUI.EndChangeCheck())
            {
                m_TextureScaleProp.floatValue = textureScale;
            }

            // HDR
            EditorGUILayout.PropertyField(m_AllowHDR, Styles.HDR);

            // MSAA
            EditorGUILayout.PropertyField(m_AllowMSAA, Styles.MSAA);
        }
#endregion

#region EditorPrefs
        bool GetFoldoutState(string name)
        {
            // Get value from EditorPrefs
            return EditorPrefs.GetBool($"{kEditorPrefKey}.{name}");
        }

        void SetFoldoutState(string name, bool field, bool value)
        {
            if(field == value)
                return;

            // Set value to EditorPrefs and field
            EditorPrefs.SetBool($"{kEditorPrefKey}.{name}", value);
            field = value;
        }
#endregion
    }
}
