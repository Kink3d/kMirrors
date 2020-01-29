using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace kTools.Mirrors
{
    /// <summary>
    /// Mirror Object component.
    /// </summary>
    [AddComponentMenu("kTools/Mirror"), ExecuteInEditMode]
    public class Mirror : MonoBehaviour
    {
#region Serialized Fields
        [SerializeField]
        float m_TextureScale;

        [SerializeField]
        float m_Offset;

        [SerializeField]
        int m_LayerMask;
#endregion

#region Fields
        const string kGizmoPath = "Packages/com.kink3d.mirrors/Gizmos/Mirror.png";
        Camera m_ReflectionCamera;
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
        public float textureScale => m_TextureScale;
        public float clipPlaneOffset => m_Offset;
        public LayerMask layerMask => m_LayerMask;
#endregion

#region State
        void OnEnable()
        {
            // Callbacks
            RenderPipelineManager.beginCameraRendering += Render;
            
            // Initialize Components
            InitializeCamera();
        }

        void OnDisable()
        {
            // Callbacks
            RenderPipelineManager.beginCameraRendering -= Render;
            
            // Destroy Camera
            if(m_ReflectionCamera)
            {
                m_ReflectionCamera.targetTexture = null;
                #if UNITY_EDITOR
                GameObject.DestroyImmediate(m_ReflectionCamera.gameObject);
                #else
                GameObject.Destroy(m_ReflectionCamera.gameObject);
                #endif
            }
        }
#endregion

#region Initialization
        void InitializeCamera()
        {
            // Create Camera
            var cameraObj = new GameObject("Reflection Camera", typeof(Camera));
            cameraObj.hideFlags = HideFlags.DontSave;

            // Setup Reflection Camera
            m_ReflectionCamera = cameraObj.GetComponent<Camera>();
            m_ReflectionCamera.transform.SetParent(transform);
            m_ReflectionCamera.enabled = false;

            // Setup AdditionalCameraData
            var cameraData = cameraObj.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderShadows = false;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
        }
#endregion

#region Rendering
        void Render(ScriptableRenderContext context, Camera camera)
        {
            // Never render Mirrors for Preview cameras
            if(camera.cameraType == CameraType.Preview)
                return;
            
            // Test for SceneView camera
            // Updating projection matrices for SceneView camera breaks gizmos
            // TODO: This breaks scene view reflections. Re-use same camera instead
            var isSceneViewCamera = false;
            #if UNITY_EDITOR
            var sceneView = UnityEditor.SceneView.currentDrawingSceneView;
            isSceneViewCamera = sceneView != null && camera == sceneView.camera;
            #endif

            // Profiling command
            CommandBuffer cmd = CommandBufferPool.Get("Mirror");
            using (new ProfilingSample(cmd, "Mirror"))
            {
                ExecuteCommand(context, cmd);

                // Create target texture
                var width = (int)Mathf.Max(camera.pixelWidth * textureScale, 4);
                var height = (int)Mathf.Max(camera.pixelHeight * textureScale, 4);
                var rendertextureDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.Default, 16);
                var renderTexture = RenderTexture.GetTemporary(rendertextureDesc);
                m_ReflectionCamera.targetTexture = renderTexture;
                
                if(!isSceneViewCamera)
                {
                    // Mirror the view matrix
                    var mirrorMatrix = GetReflectionMatrix();
                    m_ReflectionCamera.worldToCameraMatrix = camera.worldToCameraMatrix * mirrorMatrix;

                    // Make oplique projection matrix where near plane is mirror plane
                    var clipPlane = CameraSpacePlane();
                    var projectionMatrix = camera.CalculateObliqueMatrix(clipPlane);
                    m_ReflectionCamera.projectionMatrix = projectionMatrix;
                }
                
                // Miscellanious camera settings
                m_ReflectionCamera.cullingMask = layerMask;
                m_ReflectionCamera.allowHDR = camera.allowHDR;

                // Render reflection camera
                UniversalRenderPipeline.RenderSingleCamera(context, m_ReflectionCamera);

                // Set texture to shaders
                cmd.SetGlobalTexture("_ReflectionMap", renderTexture);
                ExecuteCommand(context, cmd);

                // Cleanup
                m_ReflectionCamera.targetTexture = null;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
            ExecuteCommand(context, cmd);
        }

        Matrix4x4 GetReflectionMatrix()
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

        // Calculate mirror plane in camera space.
        Vector4 CameraSpacePlane()
        {
            var pos = transform.position - Vector3.forward * 0.1f;
            var normal = transform.forward;
            var offsetPos = pos + normal * clipPlaneOffset;
            var cpos = m_ReflectionCamera.worldToCameraMatrix.MultiplyPoint(offsetPos);
            var cnormal = m_ReflectionCamera.worldToCameraMatrix.MultiplyVector(normal).normalized;
            var plane = new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));

            return plane;
        }
#endregion

#region CommandBufer
        void ExecuteCommand(ScriptableRenderContext context, CommandBuffer cmd)
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
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
