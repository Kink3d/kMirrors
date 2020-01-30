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
        public enum MirrorCameraOverride
        {
            UseSourceCameraSettings,
            Off,
        }
#endregion

#region Serialized Fields
        [SerializeField]
        float m_Offset;

        [SerializeField]
        int m_LayerMask;

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
#endregion

#region Constructors
        public Mirror()
        {
            // Set data
            m_TextureScale = 1.0f;
            m_Offset = 0.01f;
            m_LayerMask = -1;
        }
#endregion

#region Properties
        public float clipPlaneOffset => m_Offset;
        public LayerMask layerMask => m_LayerMask;
        public float textureScale => m_TextureScale;
        public MirrorCameraOverride allowHDR => m_AllowHDR;
        public MirrorCameraOverride allowMSAA => m_AllowMSAA;

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
        }
#endregion

#region Initialization
        void InitializeCamera()
        {
            // Setup AdditionalCameraData
            cameraData.renderShadows = false;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
        }
#endregion

#region Rendering
        void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // Never render Mirrors for Preview cameras
            if(camera.cameraType == CameraType.Preview)
                return;

            // Profiling command
            CommandBuffer cmd = CommandBufferPool.Get("Mirror");
            using (new ProfilingSample(cmd, "Mirror"))
            {
                ExecuteCommand(context, cmd);

                // Create target texture
                var width = (int)Mathf.Max(camera.pixelWidth * textureScale, 4);
                var height = (int)Mathf.Max(camera.pixelHeight * textureScale, 4);
                var hdr = allowHDR == MirrorCameraOverride.UseSourceCameraSettings ? camera.allowHDR : false;
                var renderTextureFormat = hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
                var rendertextureDesc = new RenderTextureDescriptor(width, height, renderTextureFormat, 16);
                var renderTexture = RenderTexture.GetTemporary(rendertextureDesc);
                reflectionCamera.targetTexture = renderTexture;
                
                // Render
                RenderMirror(context, camera);

                // Set texture to shaders
                cmd.SetGlobalTexture("_ReflectionMap", renderTexture);
                ExecuteCommand(context, cmd);

                // Cleanup
                reflectionCamera.targetTexture = null;
                RenderTexture.ReleaseTemporary(renderTexture);
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
            var depth = -Vector3.Dot(normal, position) - clipPlaneOffset;

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
            var offsetPos = pos + normal * clipPlaneOffset;
            var cpos = camera.worldToCameraMatrix.MultiplyPoint(offsetPos);
            var cnormal = camera.worldToCameraMatrix.MultiplyVector(normal).normalized;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }
#endregion

#region CommandBufer
        void ExecuteCommand(ScriptableRenderContext context, CommandBuffer cmd)
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
#endregion

#region AssetMenu
#if UNITY_EDITOR
        // Add a menu item to Decals
        [UnityEditor.MenuItem("GameObject/kTools/Mirror", false, 10)]
        static void CreateMirrorObject(UnityEditor.MenuCommand menuCommand)
        {
            // Create Decal
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
