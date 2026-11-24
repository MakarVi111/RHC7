using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RHC7
{
    /// <summary>Marker placed on the bone so raycasts can find the cleaning target.</summary>
    [DisallowMultipleComponent]
    public class NaturalBoneTarget : MonoBehaviour
    {
    }

    public enum DirtLayer
    {
        Dust = 0,
        Clay = 1,
        Crust = 2
    }

    /// <summary>
    /// One real scanned bone on a simple table. A procedurally generated,
    /// three-layer dirt mask (R dust, G clay, B crust, A wear) is baked into
    /// the mesh UVs, then painted at runtime by brush / scraper / chisel.
    /// </summary>
    [DisallowMultipleComponent]
    public class NaturalBoneCleaning : MonoBehaviour
    {
        [Header("Scene")]
        public Transform boneRoot;
        public Renderer[] boneRenderers;
        public Material dirtMaterialTemplate;
        public Camera viewCamera;
        public Transform pad;

        [Header("Bone")]
        public string boneLabel = "ЧЕРЕП TRICERATOPS";
        public float displayWidth = 0.42f;
        public float displayYaw = -48f;
        public float displayPitch = -16f;
        public float supportSink = 0.045f;

        [Header("Dirt volume")]
        [Range(64, 256)] public int maskSize = 160;
        public int noiseSeed = 20261124;
        public float removalScale = 1f;
        public float wearScale = 1f;

        [Header("Camera")]
        public float cameraDistance = 1.28f;
        public float minCameraDistance = 0.70f;
        public float maxCameraDistance = 1.75f;
        public float startYaw = 16f;
        public float startPitch = 19f;

        private class ToolSpec
        {
            public string displayName;
            public string shortName;
            public string roleHint;
            public float radiusLocal;
            public float softness;
            public float[] effectiveness = new float[3];
            public float wearRate;
            public Color uiColor;
            public Color ringGood;
        }

        private struct SurfaceSample
        {
            public Vector3 localPos;
            public Vector3 localNormal;
            public float dust;
            public float clay;
            public float crust;
        }

        private static readonly float[] LayerMassWeight = { 0.30f, 1.00f, 1.25f };
        private static readonly float[] LayerRemovalRate = { 2.80f, 1.20f, 0.68f };
        private static readonly string[] LayerNames = { "ПЫЛЬ", "ГЛИНА", "КОРА" };
        private static readonly Color DustColor = new Color(0.64f, 0.55f, 0.39f, 1f);
        private static readonly Color ClayColor = new Color(0.43f, 0.25f, 0.14f, 1f);
        private static readonly Color CrustColor = new Color(0.50f, 0.47f, 0.40f, 1f);

        private ToolSpec[] tools;
        private int toolIndex;

        private Texture3D dirtVolume;
        private Color32[] volumeData;
        private Color32[] volumeInitial;
        private Vector3 volumeMin;
        private Vector3 volumeSize;
        private int volumeRes;
        private float boneScale = 1f;
        private readonly List<SurfaceSample> surfaceSamples = new List<SurfaceSample>();
        private Material boneMaterial;
        private bool volumeDirty;
        private float lastVolumeUploadTime;

        private float initialDust, initialClay, initialCrust, initialMass;
        private float remainingDust, remainingClay, remainingCrust, remainingMass;
        private float wearTotal;

        private Transform spinPivot;
        private Vector3 cameraTarget;
        private float cameraYaw;
        private float cameraPitch;
        private float cameraZoom;

        private bool hasHit;
        private Vector3 hitPoint;
        private Vector3 hitNormal;
        private Vector3 hitLocalPoint;
        private Vector3 hitLocalNormal;
        private int hitLayer;
        private float hitCoverage;
        private Vector3 lastPaintLocal;
        private bool hasLastPaintLocal;
        private Vector2 lastMousePos;
        private float smoothedSpeed;
        private bool completed;
        private bool resetting;

        private ParticleSystem dirtParticles;
        private LineRenderer cursorRing;
        private GameObject cursorRingGo;

        private Font font;
        private Text toolNameText;
        private Text toolHintText;
        private Text promptText;
        private Text progressText;
        private Text wearText;
        private Text completionTitle;
        private Text completionSub;
        private GameObject completionPanel;
        private RectTransform progressFill;
        private RectTransform wearFill;
        private RectTransform[] layerFills;
        private Text[] layerPercents;
        private Image[] toolButtonImages;
        private Text[] toolButtonTexts;
        private Button[] toolButtons;

        private static readonly Color Gold = new Color(0.92f, 0.76f, 0.42f, 1f);
        private static readonly Color Ivory = new Color(0.88f, 0.83f, 0.72f, 1f);
        private static readonly Color Dim = new Color(0.62f, 0.57f, 0.47f, 1f);
        private static readonly Color Danger = new Color(0.86f, 0.38f, 0.28f, 1f);
        private static readonly Color Good = new Color(0.62f, 0.84f, 0.52f, 1f);
        private static readonly Color PanelColor = new Color(0.030f, 0.027f, 0.022f, 0.90f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.10f, 0.072f, 0.96f);
        private static readonly Color ButtonSelected = new Color(0.52f, 0.38f, 0.16f, 0.98f);

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (boneRoot == null)
            {
                GameObject found = GameObject.Find("Triceratops_Skull");
                if (found != null) boneRoot = found.transform;
            }
            if (pad == null)
            {
                GameObject found = GameObject.Find("BonePad");
                if (found != null) pad = found.transform;
            }

            BuildTools();
            SetupBone();
            GenerateDirtVolume();
            SetupMaterial();
            SetupInputAndPresentation();
        }

        private void Start()
        {
            SelectTool(0);
            UpdateHud();
            UpdateCamera(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            lastPaintLocal = Vector3.zero;
            hasLastPaintLocal = false;
            lastMousePos = Input.mousePosition;
            Debug.Log("[DirtCleaning] ready · " + DebugState());
        }

        private void BuildTools()
        {
            tools = new ToolSpec[3];

            tools[0] = new ToolSpec
            {
                displayName = "Кисть",
                shortName = "КИСТЬ",
                roleHint = "Сметает пыль. Широкая и безопасная, но глину и кору почти не берёт.",
                radiusLocal = 0.030f,
                softness = 0.72f,
                effectiveness = new[] { 1.00f, 0.22f, 0.02f },
                wearRate = 0.006f,
                uiColor = new Color(0.78f, 0.66f, 0.45f, 1f),
                ringGood = new Color(0.85f, 0.78f, 0.55f, 0.9f)
            };

            tools[1] = new ToolSpec
            {
                displayName = "Скребок",
                shortName = "СКРЕБОК",
                roleHint = "Срезает глину. Пыль убирает грубо, кору лишь царапает.",
                radiusLocal = 0.020f,
                softness = 0.55f,
                effectiveness = new[] { 0.55f, 1.00f, 0.12f },
                wearRate = 0.016f,
                uiColor = new Color(0.68f, 0.75f, 0.82f, 1f),
                ringGood = new Color(0.62f, 0.80f, 0.92f, 0.9f)
            };

            tools[2] = new ToolSpec
            {
                displayName = "Зубило",
                shortName = "ЗУБИЛО",
                roleHint = "Скалывает кору. Точечный удар; на пыли только царапает кость.",
                radiusLocal = 0.013f,
                softness = 0.45f,
                effectiveness = new[] { 0.12f, 0.45f, 1.00f },
                wearRate = 0.024f,
                uiColor = new Color(0.86f, 0.62f, 0.42f, 1f),
                ringGood = new Color(0.95f, 0.66f, 0.42f, 0.9f)
            };
        }

        private void SetupBone()
        {
            if (boneRoot == null)
            {
                Debug.LogError("[DirtCleaning] Bone root is missing.");
                enabled = false;
                return;
            }

            if (boneRenderers == null || boneRenderers.Length == 0)
            {
                boneRenderers = boneRoot.GetComponentsInChildren<Renderer>(true);
            }

            // Uniform scale to a display-friendly fossil size.
            Bounds bounds = CalculateBounds();
            float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (maxDim > 0.0001f)
            {
                float scale = displayWidth / maxDim;
                boneRoot.localScale = boneRoot.localScale * scale;
            }
            boneScale = boneRoot.lossyScale.x;

            // Three-quarter presentation, nose tipped slightly down onto the pad.
            boneRoot.rotation = Quaternion.Euler(displayPitch, displayYaw, 0f) * boneRoot.rotation;

            bounds = CalculateBounds();
            float padTop = 0.865f;
            if (pad != null)
            {
                var padRenderer = pad.GetComponentInChildren<Renderer>();
                if (padRenderer != null) padTop = padRenderer.bounds.max.y;
            }

            // Spin pivot at the visual centre: Q/E turns the bone in place,
            // which is essential for reaching the far side of the skull.
            GameObject pivotGo = new GameObject("BoneSpinPivot");
            pivotGo.transform.SetParent(transform, false);
            pivotGo.transform.position = bounds.center;
            spinPivot = pivotGo.transform;
            boneRoot.SetParent(spinPivot, true);

            bounds = CalculateBounds();
            // Sink the base slightly into the soft support cushion so the
            // skull reads as resting in the sandbag instead of hovering.
            Vector3 delta = new Vector3(
                (pad != null ? pad.position.x : 0f) - bounds.center.x,
                padTop - bounds.min.y + 0.002f - supportSink,
                (pad != null ? pad.position.z : 0f) - bounds.center.z);
            spinPivot.position += delta;

            // Colliders + target marker.
            if (boneRoot.GetComponent<NaturalBoneTarget>() == null)
                boneRoot.gameObject.AddComponent<NaturalBoneTarget>();

            var filters = boneRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<MeshCollider>() != null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
            }

            cameraTarget = CalculateBounds().center;
            cameraYaw = startYaw;
            cameraPitch = startPitch;
            cameraZoom = cameraDistance;
        }

        private Bounds CalculateBounds()
        {
            bool first = true;
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < boneRenderers.Length; i++)
            {
                var r = boneRenderers[i];
                if (r == null || !r.enabled) continue;
                if (first) { result = r.bounds; first = false; }
                else result.Encapsulate(r.bounds);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Procedural dirt
        // ------------------------------------------------------------------

        private static readonly Vector3 DirtOffset1 = new Vector3(13.7f, 41.2f, 7.9f);
        private static readonly Vector3 DirtOffset2 = new Vector3(61.4f, 10.5f, 33.1f);
        private static readonly Vector3 DirtOffset3 = new Vector3(27.9f, 73.6f, 51.2f);
        private static readonly Vector3 DirtOffset4 = new Vector3(88.1f, 19.4f, 67.3f);
        private static readonly Vector3 DirtOffset5 = new Vector3(5.2f, 90.7f, 12.8f);
        private static readonly Vector3 DirtOffset6 = new Vector3(44.6f, 36.9f, 81.4f);
        private static readonly Vector3 DirtOffset7 = new Vector3(70.3f, 24.8f, 58.6f);

        private void GenerateDirtVolume()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            volumeRes = Mathf.Clamp(maskSize, 64, 256);
            int n = volumeRes * volumeRes * volumeRes;
            volumeData = new Color32[n];
            for (int i = 0; i < n; i++) volumeData[i] = new Color32(255, 255, 255, 0);

            // Vertices double as surface samples: they carry the procedural
            // sediment coverage used for progress and layer readouts.
            surfaceSamples.Clear();
            var filter = boneRenderers != null && boneRenderers.Length > 0
                ? boneRenderers[0].GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogError("[DirtCleaning] No readable skull mesh found.");
                return;
            }

            Mesh mesh = filter.sharedMesh;
            if (!mesh.isReadable)
            {
                Debug.LogError("[DirtCleaning] Mesh '" + mesh.name + "' is not readable. Enable Read/Write on the model importer.");
                return;
            }

            Bounds bounds = mesh.bounds;
            volumeMin = bounds.min - Vector3.one * 0.012f;
            volumeSize = bounds.size + Vector3.one * 0.024f;

            Vector3[] verts = mesh.vertices;
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = verts[i];
                Vector3 vn = normals != null && normals.Length == verts.Length ? normals[i] : Vector3.up;
                float dust, clay, crust;
                EvaluateDirt(p, vn, out dust, out clay, out crust);
                surfaceSamples.Add(new SurfaceSample
                {
                    localPos = p,
                    localNormal = vn,
                    dust = dust,
                    clay = clay,
                    crust = crust
                });
            }

            dirtVolume = new Texture3D(volumeRes, volumeRes, volumeRes, TextureFormat.RGBA32, false, true);
            dirtVolume.name = "SkullDirtVolume";
            dirtVolume.wrapMode = TextureWrapMode.Clamp;
            dirtVolume.filterMode = FilterMode.Trilinear;
            dirtVolume.SetPixelData(volumeData, 0);
            dirtVolume.Apply(false);

            volumeInitial = (Color32[])volumeData.Clone();
            RecalculateMasses();

            stopwatch.Stop();
            Debug.Log("[DirtCleaning] dirt volume " + volumeRes + "^3 · " + surfaceSamples.Count +
                      " surface samples in " + stopwatch.ElapsedMilliseconds + " ms");
        }

        private static void EvaluateDirt(Vector3 p, Vector3 normal, out float dust, out float clay, out float crust)
        {
            float up = Mathf.Clamp01(normal.y * 0.5f + 0.5f);

            // The fossil rests on its pad: the hidden underside is not exposed
            // sediment, so the player never needs to reach it.
            float underside = SmoothStep(-0.55f, -0.05f, normal.y);

            float broad = Fbm3(p * 5.2f + DirtOffset1);
            float patch = Fbm3(p * 8.7f + DirtOffset2);
            float mid = Fbm3(p * 17.0f + DirtOffset3);
            float fine = Fbm3(p * 41.0f + DirtOffset4);
            float micro = Fbm3(p * 105.0f + DirtOffset5);
            float clayNoise = Fbm3(p * 12.5f + DirtOffset6);
            float dustNoise = Fbm3(p * 6.8f + DirtOffset7);

            // Exposed bone: whole patches where the fossil has no sediment.
            float bare = SmoothStep(0.595f, 0.735f, broad);
            float keep = 1f - bare * 0.94f;

            // Hard mineral crust prefers upward-facing surfaces and ridges.
            float crustPatch = SmoothStep(0.520f, 0.680f, patch);
            crustPatch *= 0.55f + 0.55f * up;
            crust = Mathf.Clamp01(crustPatch * (0.72f + 0.38f * mid));
            crust = Mathf.Clamp01(crust * (0.82f + 0.25f * fine));

            // Clay is broader and softer, often around crust islands.
            float clayPatch = SmoothStep(0.485f, 0.660f, clayNoise);
            clay = Mathf.Clamp01(clayPatch * (0.66f + 0.34f * mid));
            clay = Mathf.Clamp01(clay + crust * 0.18f);

            // Loose dust settles in broad drifts, thinner on hard crust.
            dust = 0.45f + 0.55f * mid;
            dust *= 0.70f + 0.30f * fine;
            dust *= Mathf.Lerp(1f, 0.42f, crust);
            dust *= Mathf.Lerp(1f, 0.72f, clay);
            dust *= 0.82f + 0.24f * micro;
            float dustDrift = SmoothStep(0.40f, 0.60f, dustNoise);
            dust *= 0.30f + 0.70f * dustDrift;

            dust *= keep * underside;
            clay *= keep * underside;
            crust *= keep * underside;

            // Sparse fully-bare islands and speckles.
            if (bare > 0.86f)
            {
                dust *= 0.10f;
                clay *= 0.08f;
                crust *= 0.08f;
            }

            // Tiny exposed flecks inside thin dust.
            float fleck = SmoothStep(0.74f, 0.86f, fine) * 0.35f;
            dust *= 1f - fleck;
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(0.00001f, b - a));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float ValueNoise3(float x, float y, float z)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            int iz = Mathf.FloorToInt(z);
            float fx = x - ix;
            float fy = y - iy;
            float fz = z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);

            float n000 = Hash(ix, iy, iz, 1337);
            float n100 = Hash(ix + 1, iy, iz, 1337);
            float n010 = Hash(ix, iy + 1, iz, 1337);
            float n110 = Hash(ix + 1, iy + 1, iz, 1337);
            float n001 = Hash(ix, iy, iz + 1, 1337);
            float n101 = Hash(ix + 1, iy, iz + 1, 1337);
            float n011 = Hash(ix, iy + 1, iz + 1, 1337);
            float n111 = Hash(ix + 1, iy + 1, iz + 1, 1337);

            float nx00 = Mathf.Lerp(n000, n100, fx);
            float nx10 = Mathf.Lerp(n010, n110, fx);
            float nx01 = Mathf.Lerp(n001, n101, fx);
            float nx11 = Mathf.Lerp(n011, n111, fx);
            return Mathf.Lerp(Mathf.Lerp(nx00, nx10, fy), Mathf.Lerp(nx01, nx11, fy), fz);
        }

        private static float Fbm3(Vector3 p)
        {
            float sum = 0f;
            float amp = 0.5f;
            for (int i = 0; i < 3; i++)
            {
                sum += ValueNoise3(p.x, p.y, p.z) * amp;
                p *= 2.07f;
                amp *= 0.5f;
            }
            return sum;
        }

        private void RecalculateMasses()
        {
            initialDust = initialClay = initialCrust = 0f;
            for (int i = 0; i < surfaceSamples.Count; i++)
            {
                initialDust += surfaceSamples[i].dust;
                initialClay += surfaceSamples[i].clay;
                initialCrust += surfaceSamples[i].crust;
            }
            initialMass = initialDust * LayerMassWeight[0] + initialClay * LayerMassWeight[1] + initialCrust * LayerMassWeight[2];
            remainingDust = initialDust;
            remainingClay = initialClay;
            remainingCrust = initialCrust;
            remainingMass = initialMass;
            wearTotal = 0f;
        }

        // ------------------------------------------------------------------
        // Material
        // ------------------------------------------------------------------

        private void SetupMaterial()
        {
            if (dirtMaterialTemplate == null)
            {
                Shader shader = Shader.Find("RHC7/Natural Bone Dirt");
                if (shader == null)
                {
                    Debug.LogError("[DirtCleaning] Shader 'RHC7/Natural Bone Dirt' not found.");
                    return;
                }
                dirtMaterialTemplate = new Material(shader);
            }

            boneMaterial = new Material(dirtMaterialTemplate);
            boneMaterial.name = "SkullDirt (runtime)";
            boneMaterial.SetTexture("_DirtVolume", dirtVolume);
            boneMaterial.SetVector("_VolumeMin", new Vector4(volumeMin.x, volumeMin.y, volumeMin.z, 0f));
            boneMaterial.SetVector("_VolumeSize", new Vector4(volumeSize.x, volumeSize.y, volumeSize.z, 0f));
            for (int i = 0; i < boneRenderers.Length; i++)
            {
                if (boneRenderers[i] != null) boneRenderers[i].sharedMaterial = boneMaterial;
            }
        }

        // ------------------------------------------------------------------
        // Presentation: camera, cursor ring, particles, UI
        // ------------------------------------------------------------------

        private void SetupInputAndPresentation()
        {
            if (viewCamera == null)
            {
                GameObject camGo = new GameObject("CleaningCamera");
                camGo.transform.SetParent(transform, false);
                viewCamera = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            if (viewCamera.GetComponent<AudioListener>() == null)
                viewCamera.gameObject.AddComponent<AudioListener>();
            viewCamera.fieldOfView = 38f;
            viewCamera.nearClipPlane = 0.02f;
            viewCamera.farClipPlane = 20f;
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(0.018f, 0.016f, 0.014f, 1f);

            BuildParticles();
            BuildCursorRing();

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            BuildUi();
        }

        private void BuildParticles()
        {
            GameObject go = new GameObject("DirtParticles");
            go.transform.SetParent(transform, false);
            dirtParticles = go.AddComponent<ParticleSystem>();
            var main = dirtParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.008f, 0.055f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.0012f, 0.0042f);
            main.startColor = DustColor;
            main.gravityModifier = 0.45f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            var emission = dirtParticles.emission;
            emission.enabled = false;
            var shape = dirtParticles.shape;
            shape.enabled = false;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", Color.white);
            psr.material = mat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
        }

        private void BuildCursorRing()
        {
            cursorRingGo = new GameObject("DirtCursorRing");
            cursorRingGo.transform.SetParent(transform, false);
            cursorRing = cursorRingGo.AddComponent<LineRenderer>();
            cursorRing.useWorldSpace = false;
            cursorRing.loop = true;
            cursorRing.positionCount = 48;
            cursorRing.widthMultiplier = 0.055f;
            cursorRing.numCapVertices = 2;
            cursorRing.alignment = LineAlignment.View;
            cursorRing.textureMode = LineTextureMode.Stretch;
            cursorRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cursorRing.receiveShadows = false;

            Shader shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            cursorRing.material = mat;
            cursorRing.startColor = Color.white;
            cursorRing.endColor = Color.white;
            for (int i = 0; i < cursorRing.positionCount; i++)
            {
                float a = i / (float)cursorRing.positionCount * Mathf.PI * 2f;
                cursorRing.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            cursorRingGo.SetActive(false);
        }

        private void UpdateCamera(bool immediate)
        {
            if (viewCamera == null) return;

            Quaternion rot = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 offset = rot * new Vector3(0f, 0f, -cameraZoom);
            Vector3 desired = cameraTarget + offset;
            if (immediate)
            {
                viewCamera.transform.position = desired;
            }
            else
            {
                viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, desired, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }
            viewCamera.transform.rotation = Quaternion.LookRotation(cameraTarget - viewCamera.transform.position, Vector3.up);
        }

        // ------------------------------------------------------------------
        // Main loop
        // ------------------------------------------------------------------

        private void Update()
        {
            if (resetting) return;

            HandleInput();
            UpdateCursorState();
            UpdateCamera(false);

            Vector2 mouse = Input.mousePosition;
            Vector2 mouseDelta = mouse - lastMousePos;
            lastMousePos = mouse;
            float dt = Time.deltaTime;

            if (!completed && hasHit && Input.GetMouseButton(0) && !PointerOverUi() && dt > 0.0001f)
            {
                ToolSpec tool = tools[toolIndex];
                float rawSpeed = Mathf.Clamp(mouseDelta.magnitude / Mathf.Max(1f, Screen.height) / dt, 0f, 2.2f);
                smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, 1f - Mathf.Exp(-16f * dt));

                float bonus = 0.42f + Mathf.Clamp01(smoothedSpeed) * 0.85f;
                bool paintedVisible = false;
                float radius = tool.radiusLocal;
                if (hasLastPaintLocal && Vector3.Distance(lastPaintLocal, hitLocalPoint) < radius * 4f)
                {
                    float dist = Vector3.Distance(lastPaintLocal, hitLocalPoint);
                    int steps = Mathf.Clamp(Mathf.CeilToInt(dist / Mathf.Max(0.001f, radius * 0.45f)), 1, 16);
                    for (int i = 1; i <= steps; i++)
                    {
                        Vector3 p = Vector3.Lerp(lastPaintLocal, hitLocalPoint, i / (float)steps);
                        paintedVisible |= PaintVolumeAt(p, hitLocalNormal, tool, dt / steps * bonus);
                    }
                }
                else
                {
                    paintedVisible = PaintVolumeAt(hitLocalPoint, hitLocalNormal, tool, dt * bonus);
                }

                lastPaintLocal = hitLocalPoint;
                hasLastPaintLocal = true;

                if (paintedVisible && UnityEngine.Random.value < 0.65f)
                {
                    EmitDirtParticle(hitPoint, hitNormal, hitLayer);
                }
            }
            else
            {
                hasLastPaintLocal = false;
                smoothedSpeed = Mathf.Lerp(smoothedSpeed, 0f, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            }

            if (volumeDirty && (!Input.GetMouseButton(0) || Time.unscaledTime - lastVolumeUploadTime > 0.05f))
            {
                UploadVolume();
                RecalculateRemainingFromSamples();
                volumeDirty = false;
                lastVolumeUploadTime = Time.unscaledTime;
            }

            UpdateHud();

            if (!completed && remainingMass / Mathf.Max(1f, initialMass) <= 0.02f)
            {
                Complete();
            }
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(2);
            if (Input.GetKeyDown(KeyCode.R)) ResetRun();

            if (Input.GetKey(KeyCode.Q) && spinPivot != null)
                spinPivot.Rotate(0f, -70f * Time.unscaledDeltaTime, 0f, Space.World);
            if (Input.GetKey(KeyCode.E) && spinPivot != null)
                spinPivot.Rotate(0f, 70f * Time.unscaledDeltaTime, 0f, Space.World);

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !PointerOverUi())
            {
                cameraZoom = Mathf.Clamp(cameraZoom - scroll * 0.075f, minCameraDistance, maxCameraDistance);
            }

            if (Input.GetMouseButton(1))
            {
                float dx = Input.GetAxis("Mouse X") * 3.2f;
                float dy = Input.GetAxis("Mouse Y") * 3.2f;
                cameraYaw += dx;
                cameraPitch = Mathf.Clamp(cameraPitch - dy, 5f, 72f);
            }
        }

        private void UpdateCursorState()
        {
            hasHit = false;
            if (completed || PointerOverUi() || viewCamera == null)
            {
                if (cursorRingGo != null) cursorRingGo.SetActive(false);
                return;
            }

            Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (cursorRingGo != null) cursorRingGo.SetActive(false);
                return;
            }

            if (hit.collider.GetComponentInParent<NaturalBoneTarget>() == null)
            {
                if (cursorRingGo != null) cursorRingGo.SetActive(false);
                return;
            }

            hasHit = true;
            hitPoint = hit.point;
            hitNormal = hit.normal;
            hitLocalPoint = boneRoot.InverseTransformPoint(hit.point);
            hitLocalNormal = boneRoot.InverseTransformDirection(hit.normal).normalized;
            GetDominantLayer(hitLocalPoint, hitLocalNormal, out hitLayer, out hitCoverage);

            UpdateRing(hit);
        }

        private void UpdateRing(RaycastHit hit)
        {
            if (cursorRingGo == null) return;
            cursorRingGo.SetActive(true);

            ToolSpec tool = tools[toolIndex];
            float worldRadius = tool.radiusLocal * boneScale;
            cursorRingGo.transform.position = hit.point + hit.normal * 0.0025f;
            cursorRingGo.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            cursorRingGo.transform.localScale = Vector3.one * worldRadius;

            float eff = tool.effectiveness[hitLayer];
            Color color;
            if (hitCoverage < 0.035f)
            {
                color = new Color(0.85f, 0.82f, 0.74f, 0.45f);
            }
            else if (eff >= 0.60f)
            {
                color = tool.ringGood;
            }
            else if (eff >= 0.30f)
            {
                color = new Color(0.95f, 0.78f, 0.32f, 0.85f);
            }
            else
            {
                color = new Color(0.95f, 0.36f, 0.24f, 0.9f);
            }
            // LineRenderer widths are world units, not local units.
            cursorRing.widthMultiplier = Input.GetMouseButton(0) && hitCoverage >= 0.035f ? 0.0022f : 0.0016f;
            cursorRing.startColor = color;
            cursorRing.endColor = color;
        }

        private bool PaintVolumeAt(Vector3 localPoint, Vector3 localNormal, ToolSpec tool, float dt)
        {
            if (volumeData == null) return false;

            float eDust = tool.effectiveness[0];
            float eClay = tool.effectiveness[1];
            float eCrust = tool.effectiveness[2];

            float sd, sc, sk;
            EvaluateDirt(localPoint, localNormal, out sd, out sc, out sk);
            float md, mc, mk, mw;
            SampleVolume(localPoint, out md, out mc, out mk, out mw);
            float visD = sd * md;
            float visC = sc * mc;
            float visK = sk * mk;
            float present = visD + visC + visK;
            float dirtWas = Mathf.Max(visD, Mathf.Max(visC, visK));
            float eff = 0f;
            if (present > 0.01f)
                eff = (visD * eDust + visC * eClay + visK * eCrust) / present;

            float radius = tool.radiusLocal;
            float vx = volumeSize.x / volumeRes;
            float vy = volumeSize.y / volumeRes;
            float vz = volumeSize.z / volumeRes;

            int minX = Mathf.Max(0, Mathf.FloorToInt((localPoint.x - radius - volumeMin.x) / vx));
            int maxX = Mathf.Min(volumeRes - 1, Mathf.CeilToInt((localPoint.x + radius - volumeMin.x) / vx));
            int minY = Mathf.Max(0, Mathf.FloorToInt((localPoint.y - radius - volumeMin.y) / vy));
            int maxY = Mathf.Min(volumeRes - 1, Mathf.CeilToInt((localPoint.y + radius - volumeMin.y) / vy));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((localPoint.z - radius - volumeMin.z) / vz));
            int maxZ = Mathf.Min(volumeRes - 1, Mathf.CeilToInt((localPoint.z + radius - volumeMin.z) / vz));
            if (minX > maxX || minY > maxY || minZ > maxZ) return false;

            bool changed = false;
            bool removedVisible = false;
            for (int z = minZ; z <= maxZ; z++)
            {
                float pz = volumeMin.z + (z + 0.5f) * vz;
                for (int y = minY; y <= maxY; y++)
                {
                    float py = volumeMin.y + (y + 0.5f) * vy;
                    int row = (z * volumeRes + y) * volumeRes;
                    for (int x = minX; x <= maxX; x++)
                    {
                        float px = volumeMin.x + (x + 0.5f) * vx;
                        float dx = (px - localPoint.x) / radius;
                        float dy = (py - localPoint.y) / radius;
                        float dz = (pz - localPoint.z) / radius;
                        float d = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (d >= 1f) continue;

                        float falloff = 1f - SmoothStep(tool.softness, 1f, d);
                        if (falloff <= 0.001f) continue;

                        float pressure = dt * falloff * removalScale;
                        int idx = row + x;
                        Color32 c = volumeData[idx];
                        float r = c.r / 255f;
                        float g = c.g / 255f;
                        float b = c.b / 255f;
                        float a = c.a / 255f;

                        float oldR = r, oldG = g, oldB = b;
                        r = Mathf.Max(0f, r - pressure * LayerRemovalRate[0] * eDust);
                        g = Mathf.Max(0f, g - pressure * LayerRemovalRate[1] * eClay);
                        b = Mathf.Max(0f, b - pressure * LayerRemovalRate[2] * eCrust);

                        float wearGain = pressure * tool.wearRate * wearScale * (0.15f + (1f - eff) * 0.85f);
                        if (dirtWas < 0.05f) wearGain *= 1.35f;
                        a = Mathf.Clamp01(a + wearGain);
                        if (a > wearTotal) wearTotal = a;

                        if (r != oldR || g != oldG || b != oldB || a != c.a / 255f)
                        {
                            changed = true;
                            if (d < 0.55f && present > 0.03f && (oldR - r) + (oldG - g) + (oldB - b) > 0.0001f)
                                removedVisible = true;
                        }

                        volumeData[idx] = new Color32(
                            (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                    }
                }
            }

            if (changed) volumeDirty = true;
            return removedVisible;
        }

        private void SampleVolume(Vector3 local, out float dust, out float clay, out float crust, out float wear)
        {
            dust = clay = crust = wear = 0f;
            if (volumeData == null) return;

            float fx = (local.x - volumeMin.x) / volumeSize.x * volumeRes - 0.5f;
            float fy = (local.y - volumeMin.y) / volumeSize.y * volumeRes - 0.5f;
            float fz = (local.z - volumeMin.z) / volumeSize.z * volumeRes - 0.5f;

            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            int z0 = Mathf.FloorToInt(fz);
            float tx = fx - x0;
            float ty = fy - y0;
            float tz = fz - z0;

            x0 = Mathf.Clamp(x0, 0, volumeRes - 1);
            y0 = Mathf.Clamp(y0, 0, volumeRes - 1);
            z0 = Mathf.Clamp(z0, 0, volumeRes - 1);
            int x1 = Mathf.Min(x0 + 1, volumeRes - 1);
            int y1 = Mathf.Min(y0 + 1, volumeRes - 1);
            int z1 = Mathf.Min(z0 + 1, volumeRes - 1);

            float d000 = volumeData[(z0 * volumeRes + y0) * volumeRes + x0].r / 255f;
            float d100 = volumeData[(z0 * volumeRes + y0) * volumeRes + x1].r / 255f;
            float d010 = volumeData[(z0 * volumeRes + y1) * volumeRes + x0].r / 255f;
            float d110 = volumeData[(z0 * volumeRes + y1) * volumeRes + x1].r / 255f;
            float d001 = volumeData[(z1 * volumeRes + y0) * volumeRes + x0].r / 255f;
            float d101 = volumeData[(z1 * volumeRes + y0) * volumeRes + x1].r / 255f;
            float d011 = volumeData[(z1 * volumeRes + y1) * volumeRes + x0].r / 255f;
            float d111 = volumeData[(z1 * volumeRes + y1) * volumeRes + x1].r / 255f;
            float c00 = Mathf.Lerp(Mathf.Lerp(d000, d100, tx), Mathf.Lerp(d010, d110, tx), ty);
            float c01 = Mathf.Lerp(Mathf.Lerp(d001, d101, tx), Mathf.Lerp(d011, d111, tx), ty);
            dust = Mathf.Lerp(c00, c01, tz);

            float e000 = volumeData[(z0 * volumeRes + y0) * volumeRes + x0].g / 255f;
            float e100 = volumeData[(z0 * volumeRes + y0) * volumeRes + x1].g / 255f;
            float e010 = volumeData[(z0 * volumeRes + y1) * volumeRes + x0].g / 255f;
            float e110 = volumeData[(z0 * volumeRes + y1) * volumeRes + x1].g / 255f;
            float e001 = volumeData[(z1 * volumeRes + y0) * volumeRes + x0].g / 255f;
            float e101 = volumeData[(z1 * volumeRes + y0) * volumeRes + x1].g / 255f;
            float e011 = volumeData[(z1 * volumeRes + y1) * volumeRes + x0].g / 255f;
            float e111 = volumeData[(z1 * volumeRes + y1) * volumeRes + x1].g / 255f;
            float g00 = Mathf.Lerp(Mathf.Lerp(e000, e100, tx), Mathf.Lerp(e010, e110, tx), ty);
            float g01 = Mathf.Lerp(Mathf.Lerp(e001, e101, tx), Mathf.Lerp(e011, e111, tx), ty);
            clay = Mathf.Lerp(g00, g01, tz);

            float f000 = volumeData[(z0 * volumeRes + y0) * volumeRes + x0].b / 255f;
            float f100 = volumeData[(z0 * volumeRes + y0) * volumeRes + x1].b / 255f;
            float f010 = volumeData[(z0 * volumeRes + y1) * volumeRes + x0].b / 255f;
            float f110 = volumeData[(z0 * volumeRes + y1) * volumeRes + x1].b / 255f;
            float f001 = volumeData[(z1 * volumeRes + y0) * volumeRes + x0].b / 255f;
            float f101 = volumeData[(z1 * volumeRes + y0) * volumeRes + x1].b / 255f;
            float f011 = volumeData[(z1 * volumeRes + y1) * volumeRes + x0].b / 255f;
            float f111 = volumeData[(z1 * volumeRes + y1) * volumeRes + x1].b / 255f;
            float b00 = Mathf.Lerp(Mathf.Lerp(f000, f100, tx), Mathf.Lerp(f010, f110, tx), ty);
            float b01 = Mathf.Lerp(Mathf.Lerp(f001, f101, tx), Mathf.Lerp(f011, f111, tx), ty);
            crust = Mathf.Lerp(b00, b01, tz);

            float a000 = volumeData[(z0 * volumeRes + y0) * volumeRes + x0].a / 255f;
            float a100 = volumeData[(z0 * volumeRes + y0) * volumeRes + x1].a / 255f;
            float a010 = volumeData[(z0 * volumeRes + y1) * volumeRes + x0].a / 255f;
            float a110 = volumeData[(z0 * volumeRes + y1) * volumeRes + x1].a / 255f;
            float a001 = volumeData[(z1 * volumeRes + y0) * volumeRes + x0].a / 255f;
            float a101 = volumeData[(z1 * volumeRes + y0) * volumeRes + x1].a / 255f;
            float a011 = volumeData[(z1 * volumeRes + y1) * volumeRes + x0].a / 255f;
            float a111 = volumeData[(z1 * volumeRes + y1) * volumeRes + x1].a / 255f;
            float a00 = Mathf.Lerp(Mathf.Lerp(a000, a100, tx), Mathf.Lerp(a010, a110, tx), ty);
            float a01 = Mathf.Lerp(Mathf.Lerp(a001, a101, tx), Mathf.Lerp(a011, a111, tx), ty);
            wear = Mathf.Lerp(a00, a01, tz);
        }

        private void UploadVolume()
        {
            if (dirtVolume == null || volumeData == null) return;
            dirtVolume.SetPixelData(volumeData, 0);
            dirtVolume.Apply(false);
            if (boneMaterial != null) boneMaterial.SetTexture("_DirtVolume", dirtVolume);
        }

        private void GetDominantLayer(Vector3 localPoint, Vector3 localNormal, out int layer, out float coverage)
        {
            float sd, sc, sk;
            EvaluateDirt(localPoint, localNormal, out sd, out sc, out sk);
            float md, mc, mk, mw;
            SampleVolume(localPoint, out md, out mc, out mk, out mw);
            float r = sd * md;
            float g = sc * mc;
            float b = sk * mk;
            coverage = Mathf.Max(r, Mathf.Max(g, b));
            if (b >= r && b >= g) layer = 2;
            else if (g >= r && g >= b) layer = 1;
            else layer = 0;
            if (coverage < 0.035f) layer = 0;
        }

        private void EmitDirtParticle(Vector3 point, Vector3 normal, int layer)
        {
            if (dirtParticles == null) return;
            Color color = layer == 1 ? ClayColor : (layer == 2 ? CrustColor : DustColor);
            var emit = new ParticleSystem.EmitParams();
            emit.position = point + normal * 0.002f;
            emit.velocity = normal * UnityEngine.Random.Range(0.012f, 0.045f) +
                            UnityEngine.Random.insideUnitSphere * 0.04f;
            emit.startSize = UnityEngine.Random.Range(0.0012f, 0.0045f);
            emit.startLifetime = UnityEngine.Random.Range(0.35f, 0.85f);
            emit.startColor = color;
            dirtParticles.Emit(emit, 1);
        }

        private void SelectTool(int index)
        {
            if (tools == null || tools.Length == 0) return;
            toolIndex = Mathf.Clamp(index, 0, tools.Length - 1);
            if (toolButtonImages != null)
            {
                for (int i = 0; i < toolButtonImages.Length; i++)
                {
                    if (toolButtonImages[i] == null) continue;
                    toolButtonImages[i].color = i == toolIndex ? ButtonSelected : ButtonColor;
                }
            }
            if (toolButtonTexts != null)
            {
                for (int i = 0; i < toolButtonTexts.Length; i++)
                {
                    if (toolButtonTexts[i] == null) continue;
                    toolButtonTexts[i].color = i == toolIndex ? new Color(1f, 0.94f, 0.74f, 1f) : Ivory;
                }
            }
        }

        private void Complete()
        {
            completed = true;
            hasLastPaintLocal = false;
            if (cursorRingGo != null) cursorRingGo.SetActive(false);
            if (completionPanel != null) completionPanel.SetActive(true);
            if (completionTitle != null) completionTitle.text = "КОСТЬ ОЧИЩЕНА";
            if (completionSub != null)
                completionSub.text = "Износ поверхности: " + Mathf.RoundToInt(wearTotal * 100f) + "%  ·  R — заново";
            UpdateHud();
        }

        public void ResetRun()
        {
            if (resetting) return;
            resetting = true;
            if (volumeInitial != null)
            {
                Array.Copy(volumeInitial, volumeData, volumeInitial.Length);
                UploadVolume();
            }
            RecalculateRemainingFromSamples();
            wearTotal = 0f;
            completed = false;
            hasLastPaintLocal = false;
            volumeDirty = false;
            SelectTool(0);
            if (completionPanel != null) completionPanel.SetActive(false);
            UpdateHud();
            resetting = false;
        }

        private void UpdateHud()
        {
            if (progressText == null) return;

            float progress = 1f - remainingMass / Mathf.Max(1f, initialMass);
            progress = Mathf.Clamp01(progress);
            float shown = completed ? 1f : progress;
            if (progressFill != null) progressFill.sizeDelta = new Vector2(390f * shown, 0f);
            progressText.text = "ОЧИЩЕНО " + Mathf.RoundToInt(shown * 100f) + "%";

            if (wearFill != null) wearFill.sizeDelta = new Vector2(390f * Mathf.Clamp01(wearTotal), 0f);
            wearText.text = "ИЗНОС " + Mathf.RoundToInt(wearTotal * 100f) + "%";
            wearText.color = wearTotal > 0.6f ? Danger : Dim;

            if (layerPercents != null && layerFills != null)
            {
                float[] initial = { initialDust, initialClay, initialCrust };
                float[] remaining = { remainingDust, remainingClay, remainingCrust };
                for (int i = 0; i < 3; i++)
                {
                    float pct = initial[i] > 0.0001f ? Mathf.Clamp01(remaining[i] / initial[i]) : 0f;
                    if (layerPercents[i] != null)
                    {
                        layerPercents[i].text = Mathf.RoundToInt(pct * 100f) + "%";
                        layerPercents[i].color = pct < 0.02f ? Good : Ivory;
                    }
                    if (layerFills[i] != null)
                        layerFills[i].sizeDelta = new Vector2(168f * pct, 0f);
                }
            }

            ToolSpec tool = tools[toolIndex];
            if (toolNameText != null) toolNameText.text = "ИНСТРУМЕНТ: " + tool.displayName.ToUpperInvariant();
            if (toolHintText != null) toolHintText.text = tool.roleHint;

            if (completed)
            {
                if (promptText != null)
                {
                    promptText.text = "КОСТЬ ОЧИЩЕНА · R — НАЧАТЬ ЗАНОВО";
                    promptText.color = Good;
                }
                return;
            }

            if (promptText == null) return;
            if (!hasHit)
            {
                promptText.text = "Наведите курсор на кость и зажмите ЛКМ · Q/E — повернуть кость · ПКМ — осмотр";
                promptText.color = Dim;
                return;
            }
            if (hitCoverage < 0.035f)
            {
                promptText.text = "Участок чистый · не перетирайте поверхность";
                promptText.color = new Color(0.75f, 0.72f, 0.64f, 1f);
                return;
            }

            float eff = tool.effectiveness[hitLayer];
            string layerName = LayerNames[hitLayer];
            if (eff >= 0.60f)
            {
                promptText.text = "Слой: " + layerName + " · инструмент подходит";
                promptText.color = Good;
            }
            else if (eff >= 0.30f)
            {
                promptText.text = "Слой: " + layerName + " · работает медленно, лучше сменить инструмент";
                promptText.color = new Color(0.95f, 0.78f, 0.32f, 1f);
            }
            else
            {
                promptText.text = "Слой: " + layerName + " · этот инструмент почти не берёт, кость изнашивается";
                promptText.color = Danger;
            }
        }

        private bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------------
        // UI
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            if (font == null) return;
            if (EventSystem.current == null)
            {
                var es = new GameObject("CleaningEventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("DirtCleaningUI");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Top-left: bone + tool.
            var infoPanel = MakePanel(canvasGo.transform, "InfoPanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f),
                new Vector2(500f, 190f), PanelColor);
            MakeText(infoPanel, "Title", "RHC7 · КАБИНЕТ ПРЕПАРАТОРА",
                new Vector2(16f, -12f), new Vector2(468f, 28f), 20, Gold, TextAnchor.UpperLeft);
            MakeText(infoPanel, "Bone", boneLabel,
                new Vector2(16f, -44f), new Vector2(468f, 24f), 15, Ivory, TextAnchor.UpperLeft);
            toolNameText = MakeText(infoPanel, "ToolName", "ИНСТРУМЕНТ: КИСТЬ",
                new Vector2(16f, -74f), new Vector2(468f, 26f), 17, Gold, TextAnchor.UpperLeft);
            toolHintText = MakeText(infoPanel, "ToolHint", "",
                new Vector2(16f, -104f), new Vector2(468f, 72f), 13, Dim, TextAnchor.UpperLeft);

            // Top-right: layer breakdown.
            var layerPanel = MakePanel(canvasGo.transform, "LayerPanel",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f),
                new Vector2(390f, 200f), PanelColor);
            MakeText(layerPanel, "LayerTitle", "СЛОИ ПОРОДЫ",
                new Vector2(16f, -12f), new Vector2(358f, 26f), 17, Gold, TextAnchor.UpperLeft);
            layerFills = new RectTransform[3];
            layerPercents = new Text[3];
            Color[] layerColors = { DustColor, ClayColor, CrustColor };
            for (int i = 0; i < 3; i++)
            {
                float y = -52f - i * 44f;
                MakeText(layerPanel, "LayerLabel" + i, LayerNames[i],
                    new Vector2(16f, y), new Vector2(90f, 22f), 13, Ivory, TextAnchor.UpperLeft);
                MakeBar(layerPanel, "LayerBg" + i, "LayerFill" + i,
                    new Vector2(112f, y - 2f), new Vector2(168f, 12f),
                    new Color(0.10f, 0.09f, 0.07f, 1f), layerColors[i], out layerFills[i]);
                layerPercents[i] = MakeText(layerPanel, "LayerPct" + i, "100%",
                    new Vector2(292f, y), new Vector2(82f, 22f), 13, Ivory, TextAnchor.UpperRight);
            }

            // Top-centre prompt.
            var promptPanel = MakePanel(canvasGo.transform, "PromptPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f),
                new Vector2(760f, 46f), PanelColor);
            promptText = MakeText(promptPanel, "Prompt", "",
                new Vector2(0f, 0f), new Vector2(740f, 42f), 14, Dim, TextAnchor.MiddleCenter,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));

            // Bottom-left: overall progress + wear.
            var progressPanel = MakePanel(canvasGo.transform, "ProgressPanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f),
                new Vector2(438f, 128f), PanelColor);
            MakeText(progressPanel, "ProgLabel", "ОБЩИЙ ПРОГРЕСС",
                new Vector2(16f, -10f), new Vector2(300f, 22f), 14, Gold, TextAnchor.UpperLeft);
            MakeBar(progressPanel, "ProgBg", "ProgFill",
                new Vector2(16f, -40f), new Vector2(390f, 14f),
                new Color(0.10f, 0.09f, 0.07f, 1f), Gold, out progressFill);
            progressText = MakeText(progressPanel, "ProgText", "ОЧИЩЕНО 0%",
                new Vector2(16f, -60f), new Vector2(300f, 20f), 12, Dim, TextAnchor.UpperLeft);
            MakeBar(progressPanel, "WearBg", "WearFill",
                new Vector2(16f, -82f), new Vector2(390f, 9f),
                new Color(0.10f, 0.09f, 0.07f, 1f), Danger, out wearFill);
            wearText = MakeText(progressPanel, "WearText", "ИЗНОС 0%",
                new Vector2(300f, -60f), new Vector2(106f, 20f), 12, Dim, TextAnchor.UpperRight);
            MakeText(progressPanel, "WearLabel", "ИЗНОС ПОВЕРХНОСТИ",
                new Vector2(16f, -96f), new Vector2(300f, 20f), 12, Dim, TextAnchor.UpperLeft);

            // Bottom-centre: tool belt.
            var toolbar = MakePanel(canvasGo.transform, "Toolbar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f),
                new Vector2(900f, 112f), PanelColor);
            toolButtons = new Button[3];
            toolButtonImages = new Image[3];
            toolButtonTexts = new Text[3];
            string[] labels = {
                "1 · КИСТЬ\nпыль",
                "2 · СКРЕБОК\nглина",
                "3 · ЗУБИЛО\nкора"
            };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var button = MakeButton(toolbar, "ToolButton" + i, labels[i],
                    new Vector2(-292f + i * 292f, 0f), new Vector2(268f, 72f),
                    out toolButtonImages[i], out toolButtonTexts[i]);
                button.onClick.AddListener(delegate { SelectTool(index); });
                toolButtons[i] = button;
            }

            // Completion banner.
            var banner = MakePanel(canvasGo.transform, "CompletionPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f),
                new Vector2(620f, 126f), new Color(0.035f, 0.045f, 0.035f, 0.94f));
            completionPanel = banner.gameObject;
            completionTitle = MakeText(banner, "CompletionTitle", "КОСТЬ ОЧИЩЕНА",
                new Vector2(0f, -22f), new Vector2(592f, 40f), 28, Good, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            completionSub = MakeText(banner, "CompletionSub", "",
                new Vector2(0f, -72f), new Vector2(592f, 30f), 14, Ivory, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            completionPanel.SetActive(false);
        }

        private RectTransform MakePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        private Text MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size,
            int fontSize, Color color, TextAnchor anchor)
        {
            return MakeText(parent, name, text, pos, size, fontSize, color, anchor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        }

        private Text MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size,
            int fontSize, Color color, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private void MakeBar(Transform parent, string bgName, string fillName, Vector2 pos, Vector2 size,
            Color bg, Color fill, out RectTransform fillRect)
        {
            var bgGo = new GameObject(bgName);
            bgGo.transform.SetParent(parent, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 1f);
            bgRt.anchorMax = new Vector2(0f, 1f);
            bgRt.pivot = new Vector2(0f, 1f);
            bgRt.anchoredPosition = pos;
            bgRt.sizeDelta = size;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = bg;

            var fillGo = new GameObject(fillName);
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 0f);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fill;
            fillRect = fillRt;
        }

        private Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
            out Image image, out Text text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            image = go.AddComponent<Image>();
            image.color = ButtonColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            text = textGo.AddComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = 15;
            text.color = Ivory;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return button;
        }

        // ------------------------------------------------------------------
        // Debug hooks
        // ------------------------------------------------------------------

        public string DebugState()
        {
            float progress = 1f - remainingMass / Mathf.Max(1f, initialMass);
            return string.Format(
                "tool={0} progress={1:F3} dust={2:F3} clay={3:F3} crust={4:F3} wear={5:F3} complete={6} hit={7} layer={8}",
                tools[toolIndex].shortName, progress,
                initialDust > 0f ? remainingDust / initialDust : 0f,
                initialClay > 0f ? remainingClay / initialClay : 0f,
                initialCrust > 0f ? remainingCrust / initialCrust : 0f,
                wearTotal, completed, hasHit, hasHit ? LayerNames[hitLayer] : "-");
        }

        public void DebugSelectTool(int index)
        {
            SelectTool(index);
        }

        public void DebugSetLayer(int layer, float remaining)
        {
            if (volumeData == null) return;
            remaining = Mathf.Clamp01(remaining);
            for (int i = 0; i < volumeData.Length; i++)
            {
                Color32 c = volumeData[i];
                if (layer == 0) c.r = (byte)Mathf.RoundToInt(remaining * 255f);
                else if (layer == 1) c.g = (byte)Mathf.RoundToInt(remaining * 255f);
                else c.b = (byte)Mathf.RoundToInt(remaining * 255f);
                volumeData[i] = c;
            }
            UploadVolume();
            RecalculateRemainingFromSamples();
            UpdateHud();
        }

        public void DebugCleanAll()
        {
            if (volumeData == null) return;
            for (int i = 0; i < volumeData.Length; i++)
            {
                Color32 c = volumeData[i];
                c.r = 0; c.g = 0; c.b = 0;
                volumeData[i] = c;
            }
            UploadVolume();
            RecalculateRemainingFromSamples();
            Complete();
            UpdateHud();
        }

        public void DebugPaintAtScreen(float sx, float sy, int tool, float seconds)
        {
            if (viewCamera == null || volumeData == null) return;
            Ray ray = viewCamera.ScreenPointToRay(new Vector3(Screen.width * sx, Screen.height * sy, 0f));
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 12f, ~0, QueryTriggerInteraction.Ignore)) return;
            Vector3 localPoint = boneRoot.InverseTransformPoint(hit.point);
            Vector3 localNormal = boneRoot.InverseTransformDirection(hit.normal).normalized;
            SelectTool(tool);
            ToolSpec spec = tools[toolIndex];
            int steps = Mathf.Clamp(Mathf.RoundToInt(seconds * 60f), 1, 2400);
            for (int i = 0; i < steps; i++)
            {
                PaintVolumeAt(localPoint, localNormal, spec, 1f / 60f);
            }
            UploadVolume();
            RecalculateRemainingFromSamples();
            volumeDirty = false;
            UpdateHud();
        }

        private void RecalculateRemainingFromSamples()
        {
            remainingDust = remainingClay = remainingCrust = 0f;
            for (int i = 0; i < surfaceSamples.Count; i++)
            {
                SurfaceSample s = surfaceSamples[i];
                float md, mc, mk, mw;
                SampleVolume(s.localPos, out md, out mc, out mk, out mw);
                remainingDust += s.dust * md;
                remainingClay += s.clay * mc;
                remainingCrust += s.crust * mk;
            }
            remainingMass = remainingDust * LayerMassWeight[0] +
                            remainingClay * LayerMassWeight[1] +
                            remainingCrust * LayerMassWeight[2];
        }
    }
}
