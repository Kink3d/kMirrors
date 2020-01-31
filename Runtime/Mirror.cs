using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace kTools.Mirrors
{
    /// <summary>
    /// Mirror Object component.
    /// </summary>
    [AddComponentMenu("kTools/Mirror"), ExecuteInEditMode]
    [RequireComponent(typeof(Camera), typeof(UniversalAdditionalCameraData))]
    public class Mirror : MonoBehaviour
    {
#region Enumerations
        /// <summary>
        /// Camera override enumeration for Mirror properties
        /// <summary>
        public enum MirrorCameraOverride
        {
            UseSourceCameraSettings,
            Off,
        }

        /// <summary>
        /// Scope enumeration for Mirror output destination
        /// <summary>
        public enum OutputScope
        {
            Global,
            Local,
        }
#endregion

#region Serialized Fields
        [SerializeField]
        float m_Offset;

        [SerializeField]
        int m_LayerMask;

        [SerializeField]
        OutputScope m_Scope;

        [SerializeField]
        List<Renderer> m_Renderers;

        [SerializeField]
        float m_TextureScale;

        [SerializeField]
        MirrorCameraOverride m_AllowHDR;

        [SerializeField]
        MirrorCameraOverride m_AllowMSAA;
#endregion

#region Fields
        const string kGizmoPath = "Packages/com.kink3d.mirrors/Gizmos/Mirror.png";
        Camera m_ReflectionCamera;
        UniversalAdditionalCameraData m_CameraData;
        RenderTexture m_RenderTexture;
        RenderTextureDescriptor m_PreviousDescriptor;
#endregion

#region Constructors
        public Mirror()
        {
            // Set data
            m_Offset = 0.01f;
            m_LayerMask = -1;
            m_Scope = OutputScope.Global;
            m_Renderers = new List<Renderer>();
            m_TextureScale = 1.0f;
            m_AllowHDR = MirrorCameraOverride.UseSourceCameraSettings;
            m_AllowMSAA = MirrorCameraOverride.UseSourceCameraSettings;
        }
#endregion

#region Properties
        /// <summary>Offset value for oplique near clip plane.</summary>
        public float offest
        {
            get => m_Offset;
            set => m_Offset = value;
        }

        /// <summary>Which layers should the Mirror render.</summary>
        public LayerMask layerMask
        {
            get => m_LayerMask;
            set => m_LayerMask = value;
        }

        /// <summary>
        /// Global output renders to the global texture. Only one Mirror can be global.
        /// Local output renders to one texture per Mirror, this is set on all elements of the Renderers list.
        /// </summary>
        public OutputScope scope
        {
            get => m_Scope;
            set => m_Scope = value;
        }

        /// <summary>Renderers to set the reflection texture on.</summary>
        public List<Renderer> renderers
        {
            get => m_Renderers;
            set => m_Renderers = value;
        }

        /// <summary>Scale value applied to the size of the source camera texture.</summary>
        public float textureScale
        {
            get => m_TextureScale;
            set => m_TextureScale = value;
        }

        /// <summary>Should reflections be rendered in HDR.</summary>
        public MirrorCameraOverride allowHDR
        {
            get => m_AllowHDR;
            set => m_AllowHDR = value;
        }

        /// <summary>Should reflections be resolved with MSAA.</summary>
        public MirrorCameraOverride allowMSAA
        {
            get => m_AllowMSAA;
            set => m_AllowMSAA = value;
        }

        Camera reflectionCamera
        {
            get
            {
                if(m_ReflectionCamera == null)
                    m_ReflectionCamera = GetComponent<Camera>();
                return m_ReflectionCamera;
            }
        }

        UniversalAdditionalCameraData cameraData
        {
            get
            {
                if(m_CameraData == null)
                    m_CameraData = GetComponent<UniversalAdditionalCameraData>();
                return m_CameraData;
            }
        }
#endregion

#region State
        void OnEnable()
        {
            // Callbacks
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            
            // Initialize Components
            InitializeCamera();
        }

        void OnDisable()
        {
            // Callbacks
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;

            // Dispose RenderTexture
            SafeDestroyObject(m_RenderTexture);
        }
#endregion

#region Initialization
        void InitializeCamera()
        {
            // Setup Camera
            reflectionCamera.cameraType = CameraType.Reflection;
            reflectionCamera.targetTexture = m_RenderTexture;

            // Setup AdditionalCameraData
            cameraData.renderShadows = false;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
        }
#endregion

#region RenderTexture
        RenderTextureDescriptor GetDescriptor(Camera camera)
        {
            // Get scaled Texture size
            var width = (int)Mathf.Max(camera.pixelWidth * textureScale, 4);
            var height = (int)Mathf.Max(camera.pixelHeight * textureScale, 4);

            // Get Texture format
            var hdr = allowHDR == MirrorCameraOverride.UseSourceCameraSettings ? camera.allowHDR : false;
            var renderTextureFormat = hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            return new RenderTextureDescriptor(width, height, renderTextureFormat, 16) { autoGenerateMips = true, useMipMap = true };
        }
#endregion

#region Rendering
        void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // Never render Mirrors for Preview or Reflection cameras
            if(camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
                return;

            // Profiling command
            CommandBuffer cmd = CommandBufferPool.Get($"Mirror {gameObject.GetInstanceID()}");
            using (new ProfilingSample(cmd, $"Mirror {gameObject.GetInstanceID()}"))
            {
                ExecuteCommand(context, cmd);

                // Test for Descriptor changes
                var descriptor = GetDescriptor(camera);
                if(!descriptor.Equals(m_PreviousDescriptor))
                {
                    // Dispose RenderTexture
                    if(m_RenderTexture != null)
                    {
                        SafeDestroyObject(m_RenderTexture);
                    }
                    
                    // Create new RenderTexture
                    m_RenderTexture = new RenderTexture(descriptor);
                    m_PreviousDescriptor = descriptor;
                    reflectionCamera.targetTexture = m_RenderTexture;
                }
                
                // Execute
                RenderMirror(context, camera);
                SetShaderUniforms(context, m_RenderTexture, cmd);
            }
            ExecuteCommand(context, cmd);
        }

        void RenderMirror(ScriptableRenderContext context, Camera camera)
        {
            // Mirror the view matrix
            var mirrorMatrix = GetMirrorMatrix();
            reflectionCamera.worldToCameraMatrix = camera.worldToCameraMatrix * mirrorMatrix;

            // Make oplique projection matrix where near plane is mirror plane
            var mirrorPlane = GetMirrorPlane(reflectionCamera);
            var projectionMatrix = camera.CalculateObliqueMatrix(mirrorPlane);
            reflectionCamera.projectionMatrix = projectionMatrix;
            
            // Miscellanious camera settings
            reflectionCamera.cullingMask = layerMask;
            reflectionCamera.allowHDR = allowHDR == MirrorCameraOverride.UseSourceCameraSettings ? camera.allowHDR : false;
            reflectionCamera.allowMSAA = allowMSAA == MirrorCameraOverride.UseSourceCameraSettings ? camera.allowMSAA : false;
            reflectionCamera.enabled = false;

            // Render reflection camera with inverse culling
            GL.invertCulling = true;
            UniversalRenderPipeline.RenderSingleCamera(context, reflectionCamera);
            GL.invertCulling = false;
        }
#endregion

#region Projection
        Matrix4x4 GetMirrorMatrix()
        {
            // Setup
            var position = transform.position;
            var normal = transform.forward;
            var depth = -Vector3.Dot(normal, position) - offest;

            // Create matrix
            var mirrorMatrix = new Matrix4x4()
            {
                m00 = (1f - 2f * normal.x  * normal.x),
                m01 = (-2f     * normal.x  * normal.y),
                m02 = (-2f     * normal.x  * normal.z),
                m03 = (-2f     * depth     * normal.x),
                m10 = (-2f     * normal.y  * normal.x),
                m11 = (1f - 2f * normal.y  * normal.y),
                m12 = (-2f     * normal.y  * normal.z),
                m13 = (-2f     * depth     * normal.y),
                m20 = (-2f     * normal.z  * normal.x),
                m21 = (-2f     * normal.z  * normal.y),
                m22 = (1f - 2f * normal.z  * normal.z),
                m23 = (-2f     * depth     * normal.z),
                m30 = 0f,
                m31 = 0f,
                m32 = 0f,
                m33 = 1f,
            };
            return mirrorMatrix;
        }
    
        Vector4 GetMirrorPlane(Camera camera)
        {
            // Calculate mirror plane in camera space.
            var pos = transform.position - Vector3.forward * 0.1f;
            var normal = transform.forward;
            var offsetPos = pos + normal * offest;
            var cpos = camera.worldToCameraMatrix.MultiplyPoint(offsetPos);
            var cnormal = camera.worldToCameraMatrix.MultiplyVector(normal).normalized;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }
#endregion

#region Output
        void SetShaderUniforms(ScriptableRenderContext context, RenderTexture renderTexture, CommandBuffer cmd)
        {
            var block = new MaterialPropertyBlock();
            switch(scope)
            {
                case OutputScope.Global:
                    // Globals
                    cmd.SetGlobalTexture("_ReflectionMap", renderTexture);
                    ExecuteCommand(context, cmd);

                    // Property Blocm
                    block.SetFloat("_LocalMirror", 0.0f);
                    foreach(var renderer in renderers)
                    {
                        renderer.SetPropertyBlock(block);
                    }
                    break;
                case OutputScope.Local:
                    // Keywords
                    Shader.EnableKeyword("_BLEND_MIRRORS");

                    // Property Block
                    block.SetTexture("_LocalReflectionMap", renderTexture);
                    block.SetFloat("_LocalMirror", 1.0f);
                    foreach(var renderer in renderers)
                    {
                        renderer.SetPropertyBlock(block);
                    }
                    break;
            }
        }
#endregion

#region CommandBufer
        void ExecuteCommand(ScriptableRenderContext context, CommandBuffer cmd)
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
#endregion

#region Object
        void SafeDestroyObject(Object obj)
        {
            if(obj == null)
                return;
            
            #if UNITY_EDITOR
            DestroyImmediate(obj);
            #else
            Destroy(obj);
            #endif
        }
#endregion

#region AssetMenu
#if UNITY_EDITOR
        // Add a menu item to Mirrors
        [UnityEditor.MenuItem("GameObject/kTools/Mirror", false, 10)]
        static void CreateMirrorObject(UnityEditor.MenuCommand menuCommand)
        {
            // Create Mirror
            GameObject go = new GameObject("New Mirror", typeof(Mirror));
            
            // Transform
            UnityEditor.GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            
            // Undo and Selection
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            UnityEditor.Selection.activeObject = go;
        }
#endif
#endregion

#region Gizmos
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // Setup
            var bounds = new Vector3(1.0f, 1.0f, 0.0f);
            var color = new Color32(0, 120, 255, 255);
            var selectedColor = new Color32(255, 255, 255, 255);
            var isSelected = UnityEditor.Selection.activeObject == gameObject;

            // Draw Gizmos
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = isSelected ? selectedColor : color;
            Gizmos.DrawIcon(transform.position, kGizmoPath, true);
            Gizmos.DrawWireCube(Vector3.zero, bounds);
        }
#endif
#endregion        
    }
}
