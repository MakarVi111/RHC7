using UnityEngine;

/// <summary>
/// Bone cleaning prototype for a single Triceratops skull.
/// Dirt layers are stored in mesh vertex colors:
///   R = dust, G = clay, B = crust.
/// Tools: 1 dust brush, 2 clay scraper, 3 crust pick.
/// LMB cleans, RMB rotates the skull, wheel zooms, R resets the pose.
/// </summary>
public class BoneCleaningV2 : MonoBehaviour
{
    public enum Tool
    {
        Crust = 0,
        Clay = 1,
        Dust = 2
    }

    [Header("Target")]
    public Renderer targetRenderer;
    public MeshFilter targetMesh;
    public Transform rotateTarget;
    public Material baseMaterial;
    public Texture2D dirtMask;

    [Header("Brush")]
    public Tool tool = Tool.Dust;
    [Range(0.004f, 0.10f)] public float radiusWorld = 0.05f;
    [Range(0.1f, 20f)] public float strength = 8f;
    public float maxDistance = 6f;

    [Header("View")]
    public float rotateSensitivity = 0.35f;
    public float zoomStep = 0.22f;
    public float zoomSmoothing = 12f;
    public float minDistance = 0.28f;
    public float maxDistanceZoom = 3.0f;

    [Header("Progress")]
    [Range(0.01f, 0.10f)] public float autoCleanThreshold = 0.04f;

    [Header("Read-only state")]
    [Range(0f, 1f)] public float dustRemaining = 1f;
    [Range(0f, 1f)] public float clayRemaining = 1f;
    [Range(0f, 1f)] public float crustRemaining = 1f;

    [Header("HUD")]
    public bool showHud = true;
    public string targetName = "ТРИЦЕРАТОПС";

    Mesh runtimeMesh;
    Color32[] weights;
    Vector3[] vertices;
    Vector3[] normals;
    long initialDust;
    long initialClay;
    long initialCrust;
    bool dustCleared;
    bool clayCleared;
    bool crustCleared;
    float progressTimer;
    float messageTimer;
    string message;
    bool ready;

    LineRenderer cursorRing;
    Material cursorMaterial;

    ParticleSystem particles;
    Material particleMaterial;

    Vector3 initialPosition;
    Quaternion initialRotation;
    bool rotating;
    bool settling;
    float settleTargetY;
    float padTop;


    Vector3 meshLocalCenter;
    float rotationRadius;
    Vector3 rotationRestCenter;
    float rotationLift;
    float rotationTargetLift;
    Vector3 lastRotationMouse;

    struct BvhNode
    {
        public Vector3 min;
        public Vector3 max;
        public int start;
        public int count;
        public int left;
        public int right;
    }

    BvhNode[] bvhNodes;
    int bvhNodeCount;
    int[] bvhTriOrder;
    Vector3[] bvhCentroids;
    int[] bvhTriangles;
    bool bvhReady;

    int surfaceCacheFrame = -1;
    Vector3 surfaceCacheOrigin;
    Vector3 surfaceCacheDirection;
    Vector3 surfaceCachePoint;
    Vector3 surfaceCacheNormal;
    bool surfaceCacheValid;

    Vector3 viewOrigin;
    Vector3 viewForward;
    Quaternion viewRotation;
    bool hasPresetView;
    Vector3 presetViewOrigin;
    Quaternion presetViewRotation;
    float zoomOffset;
    float currentZoomOffset;
    float minZoomOffset;
    float maxZoomOffset;

    public bool IsReady => ready;

    /// <summary>True while LMB is held over the specimen's surface.</summary>
    public bool IsCleaningNow { get; private set; }

    public bool IsFullyClean =>
        ready &&
        (initialDust == 0 || dustCleared) &&
        (initialClay == 0 || clayCleared) &&
        (initialCrust == 0 || crustCleared);

    /// <summary>
    /// Point the cleaning rig at a new specimen. The previous runtime mesh stays
    /// with its bone, so a cleaned bone keeps its result if it comes back later.
    /// </summary>
    public void SetTarget(Renderer renderer, MeshFilter mesh, Transform rotate,
                          Material baseMat, Texture2D mask, string displayName)
    {
        enabled = false;
        targetRenderer = renderer;
        targetMesh = mesh;
        rotateTarget = rotate;
        baseMaterial = baseMat;
        dirtMask = mask;
        targetName = string.IsNullOrEmpty(displayName)
            ? "ОБРАЗЕЦ"
            : displayName.ToUpperInvariant();

        runtimeMesh = null;
        weights = null;
        vertices = null;
        normals = null;
        ready = false;
        initialDust = 0;
        initialClay = 0;
        initialCrust = 0;
        dustCleared = false;
        clayCleared = false;
        crustCleared = false;
        dustRemaining = 1f;
        clayRemaining = 1f;
        crustRemaining = 1f;
        progressTimer = 0f;
        messageTimer = 0f;
        message = "";
        rotating = false;
        settling = false;
        rotationLift = 0f;
        zoomOffset = 0f;
        currentZoomOffset = 0f;

        enabled = true;
        Init();
    }

    public void SetTool(Tool newTool)
    {
        tool = newTool;
        UpdateCursorColor();
    }

    /// <summary>
    /// Lock cleaning to the prep-table camera. The view is shared by every
    /// specimen, so the tool tray always stays in frame no matter which bone
    /// was taken or how the previous session was left.
    /// </summary>
    public void SetWorkView(Vector3 position, Quaternion rotation)
    {
        presetViewOrigin = position;
        presetViewRotation = rotation;
        hasPresetView = true;
        if (!enabled || !ready) return;
        viewOrigin = position;
        viewRotation = rotation;
        viewForward = rotation * Vector3.forward;
        zoomOffset = 0f;
        currentZoomOffset = 0f;
    }

    public float CurrentRadius
    {
        get
        {
            float mul = tool == Tool.Dust ? 1.15f : (tool == Tool.Clay ? 0.95f : 0.80f);
            return radiusWorld * mul;
        }
    }

    /// <summary>
    /// Exact point under the cursor on the specimen's actual triangles, not a
    /// nearest-vertex guess and not the oversized box collider. The cursor
    /// ring, brush and held tool all use this point, so cleaning follows the
    /// mouse precisely.
    /// </summary>
    public bool TryGetSurfaceHit(Ray ray, out Vector3 point, out Vector3 normal)
    {
        if (surfaceCacheFrame == Time.frameCount &&
            (surfaceCacheOrigin - ray.origin).sqrMagnitude < 0.000001f &&
            Vector3.Dot(surfaceCacheDirection, ray.direction) > 0.99999f)
        {
            point = surfaceCachePoint;
            normal = surfaceCacheNormal;
            return surfaceCacheValid;
        }

        surfaceCacheFrame = Time.frameCount;
        surfaceCacheOrigin = ray.origin;
        surfaceCacheDirection = ray.direction;
        surfaceCacheValid = false;
        point = Vector3.zero;
        normal = Vector3.up;

        if (targetRenderer != null &&
            TryRaycastBvh(ray, out point, out normal))
        {
            // Do not clean through the pad, table, tray or another specimen.
            float dist = Vector3.Distance(ray.origin, point);
            if (Physics.Raycast(ray, out RaycastHit blocker, Mathf.Max(0f, dist - 0.002f),
                                ~0, QueryTriggerInteraction.Ignore) &&
                blocker.collider != null &&
                !blocker.collider.transform.IsChildOf(targetRenderer.transform))
            {
                return false;
            }

            surfaceCachePoint = point;
            surfaceCacheNormal = normal;
            surfaceCacheValid = true;
            return true;
        }

        // Fallback for a mesh without a usable BVH: use the collider hit as-is.
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance) ||
            hit.collider == null || targetRenderer == null ||
            !hit.collider.transform.IsChildOf(targetRenderer.transform))
            return false;

        point = hit.point;
        normal = hit.normal;
        surfaceCachePoint = point;
        surfaceCacheNormal = normal;
        surfaceCacheValid = true;
        return true;
    }

    void BuildSurfaceBvh(Mesh mesh)
    {
        bvhReady = false;
        if (mesh == null || vertices == null) return;

        bvhTriangles = mesh.triangles;
        if (bvhTriangles == null || bvhTriangles.Length < 3) return;

        int triCount = bvhTriangles.Length / 3;
        if (triCount > 250000) return;

        // The exact ray test below walks the triangles directly. Keep the
        // triangle list only; no acceleration structure is needed for the
        // 15-32k triangle playable skulls.
        bvhReady = triCount > 0;
    }

    int BuildBvhNode(int start, int count, int depth)
    {
        int index = bvhNodeCount++;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = start; i < start + count; i++)
        {
            int tri = bvhTriOrder[i] * 3;
            for (int k = 0; k < 3; k++)
            {
                Vector3 v = vertices[bvhTriangles[tri + k]];
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
        }

        BvhNode node = new BvhNode
        {
            min = min,
            max = max,
            start = start,
            count = count,
            left = -1,
            right = -1
        };
        bvhNodes[index] = node;

        if (count <= 8 || depth >= 26) return index;

        Vector3 ext = max - min;
        int axis = ext.x >= ext.y
            ? (ext.x >= ext.z ? 0 : 2)
            : (ext.y >= ext.z ? 1 : 2);
        System.Array.Sort(bvhTriOrder, start, count,
            new TriangleCentroidComparer(bvhCentroids, axis));

        int mid = count / 2;
        int left = BuildBvhNode(start, mid, depth + 1);
        int right = BuildBvhNode(start + mid, count - mid, depth + 1);
        node = bvhNodes[index];
        node.left = left;
        node.right = right;
        node.count = 0;
        bvhNodes[index] = node;
        return index;
    }

    bool TryRaycastBvh(Ray worldRay, out Vector3 point, out Vector3 normal)
    {
        point = Vector3.zero;
        normal = Vector3.up;
        if (targetMesh == null || vertices == null || bvhTriangles == null)
            return false;

        Transform tr = targetMesh.transform;
        Vector3 origin = tr.InverseTransformPoint(worldRay.origin);
        Vector3 dir = tr.InverseTransformDirection(worldRay.direction);
        float dirLength = dir.magnitude;
        if (dirLength < 0.000001f) return false;
        dir /= dirLength;

        // The playable skulls are decimated to 15-32k triangles. A direct
        // triangle ray test is both simpler and more reliable than an
        // accelerator for this size, and it always hits the exact cursor point.
        float bestT = float.MaxValue;
        int bestTri = -1;
        int triCount = bvhTriangles.Length / 3;
        for (int i = 0; i < triCount; i++)
        {
            int ti = i * 3;
            Vector3 a = vertices[bvhTriangles[ti]];
            Vector3 b = vertices[bvhTriangles[ti + 1]];
            Vector3 c = vertices[bvhTriangles[ti + 2]];
            if (RayTriangle(origin, dir, a, b, c, out float t) && t < bestT)
            {
                bestT = t;
                bestTri = i;
            }
        }

        if (bestTri < 0) return false;

        int hitTri = bestTri * 3;
        Vector3 va = vertices[bvhTriangles[hitTri]];
        Vector3 vb = vertices[bvhTriangles[hitTri + 1]];
        Vector3 vc = vertices[bvhTriangles[hitTri + 2]];
        Vector3 localPoint = origin + dir * bestT;
        Vector3 localNormal = Vector3.Cross(vb - va, vc - va);
        if (localNormal.sqrMagnitude < 0.0000001f) localNormal = Vector3.up;
        else localNormal.Normalize();

        Vector3 worldNormal = tr.TransformDirection(localNormal).normalized;
        if (Vector3.Dot(worldNormal, worldRay.direction) > 0f) worldNormal = -worldNormal;

        point = tr.TransformPoint(localPoint);
        normal = worldNormal;
        return true;
    }

    static bool RayAabb(Vector3 origin, Vector3 dir, Vector3 min, Vector3 max, float maxT)
    {
        float tmin = 0f;
        float tmax = maxT;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = origin[axis];
            float d = dir[axis];
            float lo = min[axis];
            float hi = max[axis];
            if (Mathf.Abs(d) < 0.0000001f)
            {
                if (o < lo || o > hi) return false;
                continue;
            }
            float inv = 1f / d;
            float t1 = (lo - o) * inv;
            float t2 = (hi - o) * inv;
            if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
            if (t1 > tmin) tmin = t1;
            if (t2 < tmax) tmax = t2;
            if (tmin > tmax) return false;
        }
        return true;
    }

    static bool RayTriangle(Vector3 origin, Vector3 dir,
                            Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0f;
        Vector3 e1 = b - a;
        Vector3 e2 = c - a;
        Vector3 p = Vector3.Cross(dir, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 0.0000001f) return false;
        float inv = 1f / det;
        Vector3 tv = origin - a;
        float u = Vector3.Dot(tv, p) * inv;
        if (u < 0f || u > 1f) return false;
        Vector3 q = Vector3.Cross(tv, e1);
        float v = Vector3.Dot(dir, q) * inv;
        if (v < 0f || u + v > 1f) return false;
        t = Vector3.Dot(e2, q) * inv;
        return t > 0.000001f;
    }

    class TriangleCentroidComparer : System.Collections.Generic.IComparer<int>
    {
        readonly Vector3[] centroids;
        readonly int axis;

        public TriangleCentroidComparer(Vector3[] centroids, int axis)
        {
            this.centroids = centroids;
            this.axis = axis;
        }

        public int Compare(int a, int b)
        {
            return centroids[a][axis].CompareTo(centroids[b][axis]);
        }
    }

    void Start()
    {
        Init();
    }

    void OnDisable()
    {
        if (cursorRing != null) cursorRing.enabled = false;
        if (particles != null) particles.Stop();
    }



    public void Init()
    {
        if (ready && runtimeMesh != null && weights != null) return;
        ready = false;

        if (targetRenderer == null)
        {
            var skull = GameObject.Find("TriceratopsSkull");
            if (skull != null) targetRenderer = skull.GetComponentInChildren<MeshRenderer>();
        }
        if (targetRenderer == null)
        {
            Debug.LogError("[BoneCleaningV2] No target renderer.");
            return;
        }
        if (targetMesh == null) targetMesh = targetRenderer.GetComponent<MeshFilter>();
        if (targetMesh == null || targetMesh.sharedMesh == null)
        {
            Debug.LogError("[BoneCleaningV2] No target MeshFilter/mesh.");
            return;
        }
        if (rotateTarget == null)
        {
            var skull = GameObject.Find("TriceratopsSkull");
            if (skull != null) rotateTarget = skull.transform;
        }
        if (baseMaterial == null) baseMaterial = targetRenderer.sharedMaterial;
        if (dirtMask == null && baseMaterial != null) dirtMask = baseMaterial.GetTexture("_DirtMask") as Texture2D;

        var source = targetMesh.sharedMesh;
        var baked = source.colors32;
        bool hasBakedDirt = baked != null && baked.Length == source.vertexCount;
        if (!hasBakedDirt && (source.uv2 == null || source.uv2.Length != source.vertexCount))
        {
            Debug.LogError("[BoneCleaningV2] Mesh has neither baked dirt colors nor a UV2 channel.");
            return;
        }

        runtimeMesh = Instantiate(source);
        runtimeMesh.name = source.name + "_RuntimeDirt";
        vertices = runtimeMesh.vertices;
        normals = runtimeMesh.normals;

        if (hasBakedDirt)
        {
            weights = (Color32[])baked.Clone();
        }
        else
        {
            weights = BuildInitialWeights(source, dirtMask);
        }
        runtimeMesh.colors32 = weights;
        runtimeMesh.UploadMeshData(false);
        targetMesh.sharedMesh = runtimeMesh;
        BuildSurfaceBvh(runtimeMesh);

        initialPosition = rotateTarget != null ? rotateTarget.position : Vector3.zero;
        initialRotation = rotateTarget != null ? rotateTarget.rotation : Quaternion.identity;

        meshLocalCenter = runtimeMesh.bounds.center;
        var boundsTr = targetMesh.transform;
        Vector3 boundsCenter = boundsTr.TransformPoint(meshLocalCenter);
        float radius = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            float d = (boundsTr.TransformPoint(vertices[i]) - boundsCenter).magnitude;
            if (d > radius) radius = d;
        }
        rotationRadius = radius;
        rotationRestCenter = boundsCenter;

        var mainCam = Camera.main;
        if (mainCam != null)
        {
            if (hasPresetView)
            {
                viewOrigin = presetViewOrigin;
                Vector3 toFocus = boundsCenter - viewOrigin;
                viewRotation = toFocus.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(toFocus.normalized, Vector3.up)
                    : presetViewRotation;
                viewForward = viewRotation * Vector3.forward;
            }
            else
            {
                viewOrigin = mainCam.transform.position;
                viewForward = mainCam.transform.forward;
                viewRotation = mainCam.transform.rotation;
            }

            // Zoom is a rail: positive offset moves toward the specimen centre.
            // Keep the camera outside the mesh and let the user pull back
            // farther than the starting framing.
            float dist = Vector3.Distance(viewOrigin, boundsCenter);
            float nearDistance = Mathf.Max(minDistance, radius * 0.65f + 0.10f);
            minZoomOffset = -Mathf.Max(0f, maxDistanceZoom - dist);
            maxZoomOffset = Mathf.Max(0f, dist - nearDistance);
            zoomOffset = 0f;
            currentZoomOffset = 0f;
        }

        var pad = GameObject.Find("BonePadV2");
        if (pad != null)
        {
            var pr = pad.GetComponentInChildren<Renderer>();
            padTop = pr != null ? pr.bounds.max.y : targetRenderer.bounds.min.y;
        }
        else
        {
            padTop = targetRenderer.bounds.min.y;
        }

        CreateCursor();
        CreateParticles();
        if (particles != null) particles.Play();
        RecomputeProgress(true);
        ready = true;
    }

    static Color32[] BuildInitialWeights(Mesh mesh, Texture2D mask)
    {
        var uv = mesh.uv2;
        var result = new Color32[mesh.vertexCount];
        if (mask == null)
        {
            for (int i = 0; i < result.Length; i++) result[i] = new Color32(0, 0, 0, 255);
            return result;
        }

        for (int i = 0; i < result.Length; i++)
        {
            Color c = mask.GetPixelBilinear(Mathf.Repeat(uv[i].x, 1f), Mathf.Repeat(uv[i].y, 1f));
            result[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                255);
        }
        return result;
    }

    void CreateCursor()
    {
        if (cursorRing != null) return;
        var go = new GameObject("BrushCursor");
        go.transform.SetParent(transform, false);
        cursorRing = go.AddComponent<LineRenderer>();
        cursorRing.useWorldSpace = true;
        cursorRing.loop = true;
        cursorRing.positionCount = 40;
        cursorRing.widthMultiplier = 0.0024f;
        cursorRing.numCapVertices = 0;
        cursorRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cursorRing.receiveShadows = false;
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh != null) cursorMaterial = new Material(sh);
        cursorRing.material = cursorMaterial;
        cursorRing.enabled = false;
        UpdateCursorColor();
    }

    void CreateParticles()
    {
        if (particles != null) return;
        var go = new GameObject("DirtParticles");
        go.transform.SetParent(transform, false);
        particles = go.AddComponent<ParticleSystem>();

        var main = particles.main;
        // Stay playing with emission disabled so manual Emit calls are
        // simulated immediately; a stopped system swallows the dust puffs.
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.7f;
        main.startSpeed = 0.12f;
        main.startSize = 0.012f;
        main.gravityModifier = 0.8f;
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.enabled = false;

        var shape = particles.shape;
        shape.enabled = false;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = grad;

        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            particleMaterial = new Material(sh);
            var psr = particles.GetComponent<ParticleSystemRenderer>();
            if (psr != null) psr.material = particleMaterial;
        }
        particles.Play();
    }

    Color ToolColor
    {
        get
        {
            switch (tool)
            {
                case Tool.Clay: return new Color(0.40f, 0.26f, 0.16f, 1f);
                case Tool.Crust: return new Color(0.32f, 0.30f, 0.26f, 1f);
                default: return new Color(0.72f, 0.68f, 0.58f, 1f);
            }
        }
    }

    void UpdateCursorColor()
    {
        if (cursorRing == null) return;
        Color c = ToolColor;
        c.a = 0.9f;
        cursorRing.startColor = c;
        cursorRing.endColor = c;
    }

    void UpdateCursor(Vector3 point, Vector3 normal, float radius)
    {
        if (cursorRing == null) return;
        Vector3 n = normal.normalized;
        Vector3 t = Vector3.Cross(n, Vector3.up);
        if (t.sqrMagnitude < 0.001f) t = Vector3.Cross(n, Vector3.right);
        t.Normalize();
        Vector3 b = Vector3.Cross(n, t);
        Vector3 center = point + n * 0.0015f;
        float r = radius * 0.92f;
        for (int i = 0; i < cursorRing.positionCount; i++)
        {
            float a = (i / (float)cursorRing.positionCount) * Mathf.PI * 2f;
            cursorRing.SetPosition(i, center + (t * Mathf.Cos(a) + b * Mathf.Sin(a)) * r);
        }
        cursorRing.enabled = true;
    }

    void Update()
    {
        IsCleaningNow = false;
        if (!ready) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) { tool = Tool.Dust; UpdateCursorColor(); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { tool = Tool.Clay; UpdateCursorColor(); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { tool = Tool.Crust; UpdateCursorColor(); }
        if (Input.GetKeyDown(KeyCode.R)) ResetPose();

        HandleBoneRotation();
        if (!rotating) UpdateSettle(Time.deltaTime);
        HandleZoomInput();
        ApplyZoom(Time.deltaTime);

        if (messageTimer > 0f) messageTimer -= Time.deltaTime;

        progressTimer += Time.deltaTime;
        if (progressTimer >= 0.2f)
        {
            RecomputeProgress(false);
            progressTimer = 0f;
        }

        if (rotating)
        {
            if (cursorRing != null) cursorRing.enabled = false;
            return;
        }

        var cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!TryGetSurfaceHit(ray, out Vector3 hitPoint, out Vector3 hitNormal))
        {
            if (cursorRing != null) cursorRing.enabled = false;
            return;
        }

        UpdateCursor(hitPoint, hitNormal, CurrentRadius);
        if (Input.GetMouseButton(0))
        {
            CleanAtPoint(hitPoint, CurrentRadius, Time.deltaTime);
            IsCleaningNow = true;
        }
    }

    void HandleBoneRotation()
    {
        if (rotateTarget == null) return;

        bool holding = Input.GetMouseButton(1) || Input.GetMouseButton(2);
        if (!holding)
        {
            if (rotating) EndBoneRotation();
            return;
        }
        if (!rotating) BeginBoneRotation();

        Vector2 delta = Input.mousePosition - lastRotationMouse;
        lastRotationMouse = Input.mousePosition;

        float roll = 0f;
        if (Input.GetKey(KeyCode.Q)) roll += 80f * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) roll -= 80f * Time.deltaTime;

        if (delta.sqrMagnitude < 0.01f && Mathf.Abs(roll) < 0.01f) return;
        RotateBoneBy(delta, roll, Time.deltaTime);
    }

    public void BeginBoneRotation()
    {
        if (rotateTarget == null) return;

        rotating = true;
        settling = false;
        lastRotationMouse = Input.mousePosition;
        rotationRestCenter = WorldCenter();
        rotationLift = 0f;

        float clearance = rotationRestCenter.y - padTop;
        rotationTargetLift = Mathf.Max(0.02f, rotationRadius - clearance + 0.02f);
    }

    /// <summary>
    /// Full 3-axis rotation around the skull centre. The bone is lifted just
    /// enough for its bounding sphere to clear the pad, so it never intersects
    /// the table and never needs per-frame height corrections (no wobble).
    /// </summary>
    public void RotateBoneBy(Vector2 delta, float roll, float dt)
    {
        if (rotateTarget == null) return;
        if (float.IsNaN(delta.x) || float.IsNaN(delta.y)) return;

        float px = Mathf.Clamp(delta.x, -400f, 400f);
        float py = Mathf.Clamp(delta.y, -400f, 400f);

        // Lift first, then turn; do not allow turning into the table during the lift.
        float step = rotationTargetLift / 0.12f * Mathf.Max(0.0001f, dt);
        rotationLift = Mathf.MoveTowards(rotationLift, rotationTargetLift, step);
        float safe = rotationTargetLift > 0.0001f
            ? Mathf.Clamp01(rotationLift / rotationTargetLift)
            : 1f;

        Vector3 center = WorldCenter();
        Vector3 desiredCenter = rotationRestCenter + Vector3.up * rotationLift;
        rotateTarget.position += desiredCenter - center;
        center = desiredCenter;

        float yaw = -px * rotateSensitivity * safe;
        float pitch = py * rotateSensitivity * safe;
        float rollAngle = roll * safe;

        rotateTarget.RotateAround(center, Vector3.up, yaw);

        var cam = Camera.main;
        if (cam != null)
        {
            rotateTarget.RotateAround(center, cam.transform.right, pitch);
            if (Mathf.Abs(rollAngle) > 0.001f)
            {
                rotateTarget.RotateAround(center, cam.transform.forward, rollAngle);
            }
        }
    }

    public void EndBoneRotation()
    {
        if (!rotating) return;
        rotating = false;
        settling = true;
        float minY = MinYAll();
        settleTargetY = rotateTarget.position.y + (padTop - minY);
    }

    void UpdateSettle(float dt)
    {
        if (!settling || rotateTarget == null) return;

        Vector3 p = rotateTarget.position;
        float k = 1f - Mathf.Exp(-14f * Mathf.Max(0.0001f, dt));
        p.y = Mathf.Lerp(p.y, settleTargetY, k);
        if (Mathf.Abs(p.y - settleTargetY) < 0.0004f)
        {
            p.y = settleTargetY;
            settling = false;
        }
        rotateTarget.position = p;
    }

    Vector3 WorldCenter()
    {
        return targetMesh.transform.TransformPoint(meshLocalCenter);
    }

    float MinYAll()
    {
        var tr = targetMesh.transform;
        float min = float.MaxValue;
        for (int i = 0; i < vertices.Length; i++)
        {
            float y = tr.TransformPoint(vertices[i]).y;
            if (y < min) min = y;
        }
        return min;
    }

    void HandleZoomInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f) return;
        // Wheel up pulls the camera toward the bone; wheel down backs away.
        zoomOffset = Mathf.Clamp(
            zoomOffset + scroll * zoomStep, minZoomOffset, maxZoomOffset);
    }

    void ApplyZoom(float dt)
    {
        var cam = Camera.main;
        if (cam == null) return;

        if (Mathf.Abs(currentZoomOffset - zoomOffset) < 0.00001f)
        {
            currentZoomOffset = zoomOffset;
            cam.transform.position = viewOrigin + viewForward * currentZoomOffset;
            cam.transform.rotation = viewRotation;
            return;
        }

        float k = 1f - Mathf.Exp(-zoomSmoothing * Mathf.Max(0.0001f, dt));
        currentZoomOffset = Mathf.Lerp(currentZoomOffset, zoomOffset, k);
        cam.transform.position = viewOrigin + viewForward * currentZoomOffset;
        cam.transform.rotation = viewRotation;
    }

    void ResetPose()
    {
        if (rotateTarget != null)
        {
            rotateTarget.position = initialPosition;
            rotateTarget.rotation = initialRotation;
        }
        rotating = false;
        settling = false;
        rotationLift = 0f;
        zoomOffset = 0f;
        ShowMessage("ПОЗА СБРОШЕНА");
    }

    /// <summary>Paint one brush step at a world-space point. Returns true if anything changed.</summary>
    public bool CleanAtPoint(Vector3 worldPoint, float radius, float dt)
    {
        if (!ready) Init();
        if (!ready) return false;

        Transform tr = targetMesh.transform;
        Vector3 local = tr.InverseTransformPoint(worldPoint);
        var cam = Camera.main;
        Vector3 eye = cam != null ? cam.transform.position : worldPoint + Vector3.up * 0.3f;
        Vector3 viewLocal = tr.InverseTransformDirection((eye - worldPoint).normalized).normalized;

        float scale = Mathf.Max(0.0001f, tr.lossyScale.x);
        float localRadius = radius / scale;
        float r2 = localRadius * localRadius;
        float invR = 1f / Mathf.Max(0.000001f, localRadius);
        // Dust is optional. The scraper and chisel are allowed to bite their
        // layer at once and also clear the dust (and the chisel, the clay)
        // they physically cut through, so the result is visible immediately.
        float dustRate = 0f, clayRate = 0f, crustRate = 0f;
        switch (tool)
        {
            case Tool.Dust:
                dustRate = 1.70f;
                break;
            case Tool.Clay:
                dustRate = 1.10f;
                clayRate = 1.15f;
                break;
            case Tool.Crust:
                dustRate = 0.85f;
                clayRate = 0f;      // clay belongs to the scraper
                crustRate = 0.75f;
                break;
        }
        float amount = strength * Mathf.Max(0.0001f, dt) * 0.5f;

        bool changed = false;
        for (int i = 0; i < vertices.Length; i++)
        {
            float d2 = (vertices[i] - local).sqrMagnitude;
            if (d2 > r2) continue;
            if (Vector3.Dot(normals[i], viewLocal) < 0.10f) continue;

            float falloff = 1f - Mathf.Sqrt(d2) * invR;
            falloff = falloff * falloff * (3f - 2f * falloff);
            int dec = Mathf.RoundToInt(amount * falloff * 255f);
            if (dec <= 0) continue;

            Color32 c = weights[i];
            bool touched = false;

            if (dustRate > 0f && c.r > 0)
            {
                c.r = (byte)Mathf.Max(0, c.r - Mathf.RoundToInt(dec * dustRate));
                touched = true;
            }
            if (clayRate > 0f && c.g > 0)
            {
                c.g = (byte)Mathf.Max(0, c.g - Mathf.RoundToInt(dec * clayRate));
                touched = true;
            }
            if (crustRate > 0f && c.b > 0)
            {
                c.b = (byte)Mathf.Max(0, c.b - Mathf.RoundToInt(dec * crustRate));
                touched = true;
            }
            if (!touched) continue;

            weights[i] = c;
            changed = true;
        }

        if (changed)
        {
            runtimeMesh.colors32 = weights;
            runtimeMesh.UploadMeshData(false);
            EmitDirtParticles(worldPoint, radius);
        }
        return changed;
    }

    void EmitDirtParticles(Vector3 point, float radius)
    {
        if (particles == null) return;
        if (!particles.isPlaying) particles.Play();
        Color c = ToolColor;
        int count = tool == Tool.Dust ? 5 : (tool == Tool.Clay ? 4 : 2);
        float sizeMin = tool == Tool.Dust ? 0.007f : 0.009f;
        float sizeMax = tool == Tool.Dust ? 0.016f : (tool == Tool.Clay ? 0.020f : 0.026f);
        for (int i = 0; i < count; i++)
        {
            var ep = new ParticleSystem.EmitParams();
            ep.position = point + Random.insideUnitSphere * radius * 0.45f;
            ep.velocity = Random.insideUnitSphere * 0.10f + Vector3.up * 0.08f;
            ep.startColor = c;
            ep.startSize = Random.Range(sizeMin, sizeMax);
            ep.startLifetime = Random.Range(0.45f, 0.9f);
            particles.Emit(ep, 1);
        }
    }

    public void RecomputeProgress(bool captureInitial)
    {
        if (weights == null) return;

        long dr = 0, dg = 0, db = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            dr += weights[i].r;
            dg += weights[i].g;
            db += weights[i].b;
        }

        if (captureInitial)
        {
            initialDust = dr;
            initialClay = dg;
            initialCrust = db;
            dustCleared = false;
            clayCleared = false;
            crustCleared = false;
            dustRemaining = 1f;
            clayRemaining = 1f;
            crustRemaining = 1f;
            return;
        }

        dustRemaining = initialDust > 0 ? (float)dr / initialDust : 0f;
        clayRemaining = initialClay > 0 ? (float)dg / initialClay : 0f;
        crustRemaining = initialCrust > 0 ? (float)db / initialCrust : 0f;

        // Clay and matrix are the structural dirt. Dust is optional: it never
        // blocks them, and any dust left at the end is whisked away silently.
        if (!clayCleared && initialClay > 0 && clayRemaining <= autoCleanThreshold)
        {
            clayCleared = true;
            ClearLayer(1);
            clayRemaining = 0f;
            bool crustDoneNow = crustCleared || crustRemaining <= autoCleanThreshold;
            ShowMessage(crustDoneNow
                ? "ГЛИНА СНЯТА — КОСТЬ ЧИСТАЯ"
                : "ГЛИНА СНЯТА — ТЕПЕРЬ МАТРИЦА");
        }

        bool clayDone = clayCleared || clayRemaining <= autoCleanThreshold;
        if (!crustCleared && initialCrust > 0 && crustRemaining <= autoCleanThreshold)
        {
            crustCleared = true;
            ClearLayer(2);
            crustRemaining = 0f;
            bool clayDoneNow = clayCleared || clayRemaining <= autoCleanThreshold;
            ShowMessage(clayDoneNow
                ? "МАТРИЦА СНЯТА — КОСТЬ ЧИСТАЯ"
                : "МАТРИЦА СНЯТА — ОСТАЛАСЬ ГЛИНА");
        }

        bool crustDone = crustCleared || crustRemaining <= autoCleanThreshold;
        bool structuralDone = clayDone && crustDone;
        if (!dustCleared && initialDust > 0 &&
            (dustRemaining <= autoCleanThreshold || structuralDone))
        {
            dustCleared = true;
            ClearLayer(0);
            dustRemaining = 0f;
            if (!structuralDone) ShowMessage("ПЫЛЬ СНЯТА");
        }
    }

    void ClearLayer(int channel)
    {
        for (int i = 0; i < weights.Length; i++)
        {
            Color32 c = weights[i];
            if (channel == 0) c.r = 0;
            else if (channel == 1) c.g = 0;
            else c.b = 0;
            weights[i] = c;
        }
        runtimeMesh.colors32 = weights;
        runtimeMesh.UploadMeshData(false);
    }

    void ShowMessage(string text)
    {
        message = text;
        messageTimer = 2.5f;
    }

    /// <summary>Fraction of all three layers removed.</summary>
    public float TotalCleanFraction
    {
        get
        {
            long total = initialDust + initialClay + initialCrust;
            if (total <= 0 || weights == null) return 0f;
            long left = 0;
            for (int i = 0; i < weights.Length; i++) left += weights[i].r + weights[i].g + weights[i].b;
            return 1f - (float)left / total;
        }
    }

    void OnGUI()
    {
        if (!showHud || !ready) return;

        var box = new GUIStyle(GUI.skin.box);
        box.fontSize = 14;
        box.alignment = TextAnchor.UpperLeft;
        box.padding = new RectOffset(12, 12, 10, 10);

        bool anyInitial = initialDust > 0 || initialClay > 0 || initialCrust > 0;
        bool allDone = (initialDust == 0 || dustCleared) &&
                       (initialClay == 0 || clayCleared) &&
                       (initialCrust == 0 || crustCleared);
        bool hasDirt = anyInitial && !allDone;

        GUILayout.BeginArea(new Rect(16, 16, 334, hasDirt ? 272 : 158), box);
        GUILayout.Label(hasDirt ? "ЧИСТКА КОСТИ — " + targetName : "ОСМОТР КОСТИ — " + targetName);

        if (!hasDirt)
        {
            GUILayout.Space(4);
            GUILayout.Label("КОСТЬ ЧИСТАЯ");
            GUILayout.Label("Клик по инструменту — взять");
            GUILayout.Label("ПКМ/СКМ — вращать, Q/E — крен");
            GUILayout.Label("Колесо — зум, R — сброс позы");
            GUILayout.EndArea();
        }
        else
        {
            GUILayout.Space(4);
            GUILayout.Label((tool == Tool.Dust ? "> " : "   ") + "[1] Кисть — пыль (необяз.) " + Status(dustCleared, dustRemaining));
            GUILayout.Label((tool == Tool.Clay ? "> " : "   ") + "[2] Скребок — глина   " + Status(clayCleared, clayRemaining));
            GUILayout.Label((tool == Tool.Crust ? "> " : "   ") + "[3] Зубило + молоток — матрица " + Status(crustCleared, crustRemaining));
            GUILayout.Space(4);
            GUILayout.Label("Очищено: " + Percent(TotalCleanFraction));
            GUILayout.Label("Клик по инструменту — взять");
            GUILayout.Label("Веди мышью по кости, держа ЛКМ");
            GUILayout.Label("ПКМ/СКМ — вращать, Q/E — крен");
            GUILayout.Label("Колесо — зум, R — сброс");
            GUILayout.EndArea();
        }

        if (messageTimer > 0f && !string.IsNullOrEmpty(message))
        {
            var st = new GUIStyle(GUI.skin.label);
            st.fontSize = 20;
            st.alignment = TextAnchor.MiddleCenter;
            st.normal.textColor = new Color(1f, 0.92f, 0.72f);
            GUI.Label(new Rect(Screen.width * 0.5f - 260f, 26f, 520f, 34f), message, st);
        }
    }

    static string Status(bool cleared, float remaining)
    {
        if (cleared) return "снято 100%";
        return "снято " + (Mathf.Clamp01(1f - remaining) * 100f).ToString("F1") + "%";
    }

    static string Percent(float v)
    {
        return (Mathf.Clamp01(v) * 100f).ToString("F1") + "%";
    }
}
