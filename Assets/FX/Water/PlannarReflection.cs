using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plannar reflection effect
/// </summary>
[ExecuteInEditMode]
public class PlannarReflection : MonoBehaviour {

    [Header("Reflection")]
    public Camera m_Camera;
    [Range(1, 1024)] public int m_TextureSize = 512;
    public float m_ClipPlaneOffset = 0.07f;
    [Range(0f, 1f)] public float m_ReflectionStrength = 0f;
    public Color m_ReflectionTint = Color.white;
    public LayerMask m_ReflectLayers = -1;
    [Header("Blur")]
    [Range(0, 8)] public int m_Iterations = 1;
    [Range(0f, 1f)] public float m_Interpolation = 1f;
    public Material m_BlurMat;
    [Header("Bump")]
    public Texture2D m_BumpTex;
    [Range(0.1f, 1f)] public float m_BumpStrength = 0.5f;
    [Range(1f, 16f)] public float m_BumpTexScale = 1f;
    [Header("Mask")]
    public Texture2D m_MaskTex;
    [Header("Height Based Sharp")]
    public bool m_EnableHeightSharp = false;
    [Header("Internal")]
    public RenderTexture m_RTReflectionColor = null;
    public RenderTexture m_RTRefractionColor = null;
    public RenderTexture m_RTScatterColor = null;
    private Camera m_ReflectionCamera = null;
    private Camera m_RefractionCamera = null;
    private int m_OldReflectionTextureSize = 0;
    private static bool s_InsideRendering = false;
    [Header("Sun")]
    public GameObject m_Sun;
    [Header("Speed")]
    public float m_Speed = 1.0f;
    private float m_Phase = 0.0f;
    private float m_CausicsPhase = 0.0f;
    [Header("Terrain")]
    public Terrain m_Terrain = null;
    public Texture2D m_HeightMap;
    [Range(0.0f, 10.0f)]
    public float m_Factor;
    public Shader m_ScatteringShader;
    public Texture m_CausticsTexture;

    /// <summary>
    /// Convert float array to byte array
    /// </summary>
    /// <param name="nmbs">Input 2D float array</param>
    /// <returns>Byte array</returns>
    public byte[] ToByteArray(float[,] nmbs)
    {
        byte[] nmbsBytes = new byte[nmbs.GetLength(0) * nmbs.GetLength(1) * 4];
        int k = 0;
        for (int i = 0; i < nmbs.GetLength(0); i++)
        {
            for (int j = 0; j < nmbs.GetLength(1); j++)
            {
                byte[] array = BitConverter.GetBytes(nmbs[i, j]);
                for (int m = 0; m < array.Length; m++)
                {
                    nmbsBytes[k++] = array[m];
                }
            }
        }
        return nmbsBytes;
    }

    /// <summary>
    /// Refresh terrain texture into height map (used for height blending)
    /// </summary>
    void UpdateTerrain()
    {
        if (m_Terrain != null)
        {
            // Basically read height map into byte array and initialize texture
            int resolution = m_Terrain.terrainData.heightmapResolution;
            float[,] data = new float[resolution, resolution];
            data = m_Terrain.terrainData.GetHeights(0, 0, resolution, resolution);

            if (m_HeightMap != null)
                DestroyImmediate(m_HeightMap);
            m_HeightMap = new Texture2D(resolution, resolution, TextureFormat.RFloat, false);
            m_HeightMap.LoadRawTextureData(ToByteArray(data));
            m_HeightMap.name = "RTR_HeightMap";
            m_HeightMap.Apply();
        }
    }

    /// <summary>
    /// On initialization, update height map for height blending
    /// </summary>
    void Start()
    {
        UpdateTerrain();
    }

    /// <summary>
    /// Update timers for caustics and water movement
    /// </summary>
    void Update()
    {
        m_Phase += m_Speed * Time.deltaTime;
        if (m_Phase > 1.0f)
        {
            m_Phase -= 1.0f;
        }

        m_CausicsPhase += Time.deltaTime;
        if (m_CausicsPhase > 1.0f)
        {
            m_CausicsPhase -= 1.0f;
        }
    }
    
    /// <summary>
    /// Render function
    /// - Has to render reflection
    /// - Has to render refraction/scattering
    /// </summary>
    void OnWillRenderObject()
    {
        Renderer rd = GetComponent<Renderer>();
        if (!enabled || !rd || !rd.sharedMaterial || !rd.enabled)
            return;

        Camera cam = m_Camera;

        if (!Application.isPlaying)
        {
            cam = Camera.current;
        }

        if (cam == null)
            return;

        // Don't allow recursive reflections!
        if (s_InsideRendering)
            return;

        s_InsideRendering = true;

        // Update cameras and render targets
        CreateMirrorObjects();

        UpdateCameraModes(cam, m_ReflectionCamera);
        UpdateCameraModes(cam, m_RefractionCamera);

        // Plannar reflection - create view & projection matrices
        Vector3 pos = transform.position;
        Vector3 normal = transform.up;
        float d = -Vector3.Dot(normal, pos) - m_ClipPlaneOffset;
        Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

        Matrix4x4 mr = Matrix4x4.zero;
        CalculateReflectionMatrix(ref mr, reflectionPlane);
        Vector3 oldpos = cam.transform.position;
        Vector3 newpos = mr.MultiplyPoint(oldpos);
        m_ReflectionCamera.worldToCameraMatrix = cam.worldToCameraMatrix * mr;

        // Setup oblique projection matrix to clip everything below/above it
        Vector4 clipPlane = CameraSpacePlane(m_ReflectionCamera, pos, normal, 1f);
        Matrix4x4 projection = cam.projectionMatrix;
        CalculateObliqueMatrix(ref projection, clipPlane);
        m_ReflectionCamera.projectionMatrix = projection;

        // Setup camera and render reflections
        m_ReflectionCamera.cullingMask = ~(1 << 4) & m_ReflectLayers.value;
        m_ReflectionCamera.targetTexture = m_RTReflectionColor;
        GL.invertCulling = true;
        m_ReflectionCamera.transform.position = newpos;
        Vector3 euler = cam.transform.eulerAngles;
        m_ReflectionCamera.transform.eulerAngles = new Vector3(0, euler.y, euler.z);
        m_ReflectionCamera.allowMSAA = false;

        m_ReflectionCamera.Render();
        
        m_ReflectionCamera.transform.position = oldpos;
        GL.invertCulling = false;

        // Setup refraction camera and render refractions
        m_RefractionCamera.worldToCameraMatrix = cam.worldToCameraMatrix;
        m_RefractionCamera.projectionMatrix = cam.projectionMatrix;
        m_RefractionCamera.cullingMask = ~(1 << 4) & m_ReflectLayers.value;
        m_RefractionCamera.depthTextureMode = DepthTextureMode.Depth;
        m_RefractionCamera.renderingPath = RenderingPath.DeferredShading;
        m_RefractionCamera.targetTexture = m_RTRefractionColor;
        m_RefractionCamera.allowMSAA = false;

        m_RefractionCamera.Render();

        // Bind another texture and render scattering color with caustics
        m_RefractionCamera.renderingPath = RenderingPath.Forward;
        m_RefractionCamera.targetTexture = m_RTScatterColor;

        Shader.SetGlobalVector("_ScatterPlane", reflectionPlane);
        Shader.SetGlobalFloat("_Phase", m_CausicsPhase);
        Shader.SetGlobalFloat("_CausticsSpeed", 1.0f / m_Speed);
        Shader.SetGlobalTexture("_ScatterCausticsTex", m_CausticsTexture);

        m_RefractionCamera.RenderWithShader(m_ScatteringShader, null);

        // In case of missing height map, recompute it
        if (m_HeightMap == null)
        {
            UpdateTerrain();
        }

        // Setup all shader parameters
        Material[] materials;
        if (Application.isEditor)
            materials = rd.sharedMaterials;
        else
            materials = rd.materials;
        foreach (Material mat in materials)
        {
            if (m_BumpTex)   // enable bump if necessary
            {
                mat.EnableKeyword("RPR_BUMP_REFLECTION");
                mat.SetFloat("_BumpStrength", m_BumpStrength);
                mat.SetTexture("_BumpTex", m_BumpTex);
                mat.SetTextureScale("_BumpTex", new Vector2(m_BumpTexScale, m_BumpTexScale));
            }
            else
            {
                mat.DisableKeyword("RPR_BUMP_REFLECTION");
            }
            if (m_EnableHeightSharp)
                mat.EnableKeyword("RPR_HEIGHT_ATTEN");
            else
                mat.DisableKeyword("RPR_HEIGHT_ATTEN");
            mat.SetTexture("_MaskTex", m_MaskTex);
            mat.SetTexture("_ReflectionTex", m_RTReflectionColor);
            mat.SetTexture("_RefractionTex", m_RTRefractionColor);
            mat.SetTexture("_ScatterTex", m_RTScatterColor);
            mat.SetTexture("_HeightMap", m_HeightMap);
            mat.SetFloat("_ReflectionStrength", 1f - m_ReflectionStrength);
            mat.SetColor("_ReflectionTint", m_ReflectionTint);
            mat.SetFloat("_Phase", m_Phase);
            mat.SetVector("_SunDirection", new Vector4(m_Sun.transform.forward.x, m_Sun.transform.forward.y, m_Sun.transform.forward.z, 0.0f));
            mat.SetMatrix("_SunViewMatrix", m_Sun.transform.worldToLocalMatrix);
            mat.SetVector("_TerrainSize", m_Terrain.terrainData.size);
            mat.SetVector("_TerrainPosition", m_Terrain.GetPosition());
            mat.SetFloat("_TerrainFactor", m_Factor);
        }
        s_InsideRendering = false;
    }

    /// <summary>
    /// Remove used objects
    /// </summary>
    void OnDisable()
    {
        if (m_RTReflectionColor)
        {
            DestroyImmediate(m_RTReflectionColor);
            m_RTReflectionColor = null;
            DestroyImmediate(m_ReflectionCamera.gameObject);
            m_ReflectionCamera = null;
        }

        if (m_RTRefractionColor)
        {
            DestroyImmediate(m_RTRefractionColor);
            m_RTRefractionColor = null;
            DestroyImmediate(m_RTScatterColor);
            m_RTScatterColor = null;
            DestroyImmediate(m_RefractionCamera.gameObject);
            m_RefractionCamera = null;
        }
    }

    /// <summary>
    /// Copy camera parameters from source into destination camera
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dest"></param>
    void UpdateCameraModes(Camera src, Camera dest)
    {
        if (dest == null)
            return;

        dest.clearFlags = src.clearFlags;
        dest.backgroundColor = src.backgroundColor;
        if (src.clearFlags == CameraClearFlags.Skybox)
        {
            Skybox sky = src.GetComponent<Skybox>();
            Skybox mysky = dest.GetComponent<Skybox>();
            if (!sky || !sky.material)
            {
                mysky.enabled = false;
            }
            else
            {
                mysky.enabled = true;
                mysky.material = sky.material;
            }
        }
        dest.farClipPlane = src.farClipPlane;
        dest.nearClipPlane = src.nearClipPlane;
        dest.orthographic = src.orthographic;
        dest.fieldOfView = src.fieldOfView;
        dest.aspect = src.aspect;
        dest.orthographicSize = src.orthographicSize;
    }

    /// <summary>
    /// Create objects required for rendering reflections
    /// </summary>
    void CreateMirrorObjects()
    {
        // Camera for reflection
        if (m_ReflectionCamera == null)
        {
            GameObject go = new GameObject("RPR_Camera_" + GetInstanceID(), typeof(Camera), typeof(Skybox));
            m_ReflectionCamera = go.GetComponent<Camera>();
            m_ReflectionCamera.enabled = false;
            m_ReflectionCamera.transform.position = transform.position;
            m_ReflectionCamera.transform.rotation = transform.rotation;
            m_ReflectionCamera.renderingPath = RenderingPath.DeferredShading;
            go.hideFlags = HideFlags.DontSave;
        }

        // Camera for refraction
        if (m_RefractionCamera == null)
        {
            GameObject go = new GameObject("RFR_Camera_" + GetInstanceID(), typeof(Camera), typeof(Skybox));
            m_RefractionCamera = go.GetComponent<Camera>();
            m_RefractionCamera.enabled = false;
            m_RefractionCamera.transform.position = transform.position;
            m_RefractionCamera.transform.rotation = transform.rotation;
            go.hideFlags = HideFlags.DontSave;
        }

        // Reflection, Refraction and Scattering render targets
        if (null == m_RTReflectionColor || m_OldReflectionTextureSize != m_TextureSize)
        {
            if (m_RTReflectionColor)
                DestroyImmediate(m_RTReflectionColor);
            m_RTReflectionColor = new RenderTexture(m_TextureSize, m_TextureSize, 24);
            m_RTReflectionColor.name = "RPR_RTColor_" + GetInstanceID();
            m_RTReflectionColor.isPowerOfTwo = true;
            m_RTReflectionColor.hideFlags = HideFlags.DontSave;
            m_RTReflectionColor.wrapMode = TextureWrapMode.Repeat;

            if (m_RTRefractionColor)
                DestroyImmediate(m_RTRefractionColor);
            m_RTRefractionColor = new RenderTexture(m_TextureSize, m_TextureSize, 24);
            m_RTRefractionColor.name = "RFR_RTColor_" + GetInstanceID();
            m_RTRefractionColor.isPowerOfTwo = true;
            m_RTRefractionColor.hideFlags = HideFlags.DontSave;
            m_RTRefractionColor.wrapMode = TextureWrapMode.Repeat;

            if (m_RTScatterColor)
                DestroyImmediate(m_RTScatterColor);
            m_RTScatterColor = new RenderTexture(m_TextureSize, m_TextureSize, 24, RenderTextureFormat.ARGBHalf);
            m_RTScatterColor.name = "RSR_RTScatter_" + GetInstanceID();
            m_RTScatterColor.isPowerOfTwo = true;
            m_RTScatterColor.hideFlags = HideFlags.DontSave;
            m_RTScatterColor.wrapMode = TextureWrapMode.Repeat;

            m_OldReflectionTextureSize = m_TextureSize;
        }
    }

    /// <summary>
    /// Calcualte plane projected into camera space
    /// </summary>
    /// <param name="cam">Camera</param>
    /// <param name="pos">Position on plane</param>
    /// <param name="normal">Normal of plane</param>
    /// <param name="sideSign">Positive or negative side</param>
    /// <returns></returns>
    Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        Vector3 offsetPos = pos + normal * m_ClipPlaneOffset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    /// <summary>
    /// Signum function
    /// </summary>
    /// <param name="a">Input value</param>
    /// <returns>Returns 1 in case of positive, -1 in case of negative</returns>
    static float sgn(float a)
    {
        if (a > 0f) return 1f;
        if (a < 0f) return -1f;
        return 0f;
    }

    /// <summary>
    /// Compute oblique matrix used for clipping
    /// </summary>
    /// <param name="projection">Camera projection matrix (used as output!)</param>
    /// <param name="clipPlane">Clipping plane equation (in camera space)</param>
    static void CalculateObliqueMatrix(ref Matrix4x4 projection, Vector4 clipPlane)
    {
        Vector4 q = projection.inverse * new Vector4(sgn(clipPlane.x), sgn(clipPlane.y), 1f, 1f);
        Vector4 c = clipPlane * (2f / (Vector4.Dot(clipPlane, q)));
        projection[2] = c.x - projection[3];
        projection[6] = c.y - projection[7];
        projection[10] = c.z - projection[11];
        projection[14] = c.w - projection[15];
    }

    /// <summary>
    /// Calculate reflection matrix
    /// </summary>
    /// <param name="reflectionMat">Output reflection matrix</param>
    /// <param name="plane">Plane along which we reflect (in world space)</param>
    static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
    {
        reflectionMat.m00 = (1f - 2f * plane[0] * plane[0]);
        reflectionMat.m01 = (-2f * plane[0] * plane[1]);
        reflectionMat.m02 = (-2f * plane[0] * plane[2]);
        reflectionMat.m03 = (-2f * plane[3] * plane[0]);

        reflectionMat.m10 = (-2f * plane[1] * plane[0]);
        reflectionMat.m11 = (1f - 2f * plane[1] * plane[1]);
        reflectionMat.m12 = (-2f * plane[1] * plane[2]);
        reflectionMat.m13 = (-2f * plane[3] * plane[1]);

        reflectionMat.m20 = (-2f * plane[2] * plane[0]);
        reflectionMat.m21 = (-2f * plane[2] * plane[1]);
        reflectionMat.m22 = (1f - 2f * plane[2] * plane[2]);
        reflectionMat.m23 = (-2f * plane[3] * plane[2]);

        reflectionMat.m30 = 0f;
        reflectionMat.m31 = 0f;
        reflectionMat.m32 = 0f;
        reflectionMat.m33 = 1f;
    }
}
