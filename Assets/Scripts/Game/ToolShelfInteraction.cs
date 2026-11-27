using UnityEngine;

/// <summary>
/// Picks prep tools from the side tray by clicking them. The selected tool
/// smoothly flies into a camera-relative hand pose, the previous one flies
/// back to its slot. No hands rig — just the tool floating into place.
/// </summary>
public class ToolShelfInteraction : MonoBehaviour
{
    public BoneCleaningV2 cleaning;
    public Camera cam;
    public float pickDistance = 6f;

    [Header("Held pose (camera space)")]
    public Vector3 handLocalPosition = new Vector3(0.33f, -0.27f, 0.95f);
    public Vector3 handLocalEuler = new Vector3(0f, 0f, 20f);
    [Range(0.5f, 1.2f)] public float heldScale = 0.85f;
    // Extra roll around each tool's long axis. The new brush is a flat head:
    // 70 + the base 20 gives the wide face horizontal.
    public float[] heldExtraRoll = new float[] { 70f, 0f, 0f };
    // The chisel/hammer pair is physically wider than a single tool; pull it
    // slightly toward the hand and lift it off the table edge.
    public float[] heldScaleMultiplier = new float[] { 1f, 1f, 0.82f };
    public float[] heldExtraLift = new float[] { 0f, 0f, 0.10f };

    [Header("Chisel + hammer pair")]
    public Transform chiselHammer;
    // Negative X: the chisel root is turned 180° around Y, so this lands on
    // the right-hand end of the tray, clear of the scraper.
    public Vector3 hammerTrayLocalPosition = new Vector3(-0.145f, -0.021f, 0f);
    public Vector3 hammerTrayLocalEuler = Vector3.zero;
    [Tooltip("Hammer head targets in the chisel root's local space.")]
    public Vector3 hammerIdleHeadLocal = new Vector3(0f, 0.050f, -0.090f);
    public Vector3 hammerRaisedHeadLocal = new Vector3(0f, 0.070f, -0.150f);
    public Vector3 hammerContactHeadLocal = new Vector3(0f, 0f, -0.030f);
    [Tooltip("Handle direction in chisel-local space. Local X of the hammer is turned to the chisel axis so the flat face strikes.")]
    public Vector3 hammerHandleDirectionLocal = new Vector3(-0.90f, 0.45f, 0f);
    public float hammerStrikesPerSecond = 1.8f;
    public float hammerPoseSharpness = 40f;

    [Header("Tool motion")]
    public float idleSwayPosition = 0.004f;
    public float idleSwayDegrees = 1.2f;
    public float workBlendSharpness = 9f;

    [Header("Contact with the specimen")]
    public bool followCursorOnBone = true;
    public float contactClearance = 0.008f;
    public float minHeldDepth = 0.30f;
    public float followSharpness = 22f;
    public float scrubAmplitude = 0.0035f;
    public float scrubSpeed = 42f;
    public float scrubRoll = 2.5f;

    [Header("Flight animation")]
    public float flyDuration = 0.45f;
    public float returnDuration = 0.35f;
    public float arcHeight = 0.06f;

    Transform[] tools;
    Vector3[] homePosition;
    Quaternion[] homeRotation;
    Vector3[] homeScale;
    Collider[][] toolColliders;
    float[] tipLocalZ;

    float[] flyT;
    bool[] returning;
    Vector3[] flyStartPos;
    Quaternion[] flyStartRot;
    Vector3[] flyStartScale;

    int selected = -1;
    bool initialized;
    int lastActive = -1;
    Vector3 hammerHeadLocal;
    float workTime;
    float workBlend;
    float strikePhase;
    float strikeImpact;
    bool wasWorking;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (cleaning == null) cleaning = Object.FindFirstObjectByType<BoneCleaningV2>();

        tools = new Transform[3];
        tools[0] = transform.Find("PrepBrush");
        tools[1] = transform.Find("PrepScraper");
        tools[2] = transform.Find("PrepChisel");

        int n = tools.Length;
        homePosition = new Vector3[n];
        homeRotation = new Quaternion[n];
        homeScale = new Vector3[n];
        toolColliders = new Collider[n][];
        tipLocalZ = new float[n];
        flyT = new float[n];
        returning = new bool[n];
        flyStartPos = new Vector3[n];
        flyStartRot = new Quaternion[n];
        flyStartScale = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            if (tools[i] == null) continue;
            homePosition[i] = tools[i].localPosition;
            homeRotation[i] = tools[i].localRotation;
            homeScale[i] = tools[i].localScale;
            toolColliders[i] = tools[i].GetComponentsInChildren<Collider>(true);
            if (i == 2)
            {
                // Measure only the steel chisel. If the hammer is included,
                // the computed reach is too long and the tip floats off the bone.
                Transform chiselChild = tools[i].Find("PrepChisel_Visual");
                if (chiselChild == null) chiselChild = tools[i].Find("ChiselTool");
                tipLocalZ[i] = chiselChild != null
                    ? MeasureTipLocalZInRoot(tools[i], chiselChild)
                    : MeasureTipLocalZ(tools[i]);
            }
            else
            {
                tipLocalZ[i] = MeasureTipLocalZ(tools[i]);
            }
            flyT[i] = 1f;
            returning[i] = false;
        }

        if (chiselHammer == null && tools[2] != null)
            chiselHammer = tools[2].Find("HammerPivot");
        if (chiselHammer != null)
            hammerHeadLocal = MeasureHammerHeadLocal(chiselHammer);
    }

    /// <summary>Distance from the tool pivot to its working tip along local +Z.</summary>
    static float MeasureTipLocalZ(Transform tool)
    {
        bool has = false;
        Bounds local = default(Bounds);
        foreach (var mf in tool.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            Bounds mb = mesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3(
                    (c & 1) == 0 ? mb.min.x : mb.max.x,
                    (c & 2) == 0 ? mb.min.y : mb.max.y,
                    (c & 4) == 0 ? mb.min.z : mb.max.z);
                Vector3 p = tool.InverseTransformPoint(
                    mf.transform.TransformPoint(corner));
                if (!has) { local = new Bounds(p, Vector3.zero); has = true; }
                else local.Encapsulate(p);
            }
        }
        return has ? Mathf.Max(0.04f, local.max.z) : 0.11f;
    }

    /// <summary>Tip of one child measured in the tool root's local +Z.</summary>
    static float MeasureTipLocalZInRoot(Transform root, Transform visual)
    {
        bool has = false;
        float maxZ = float.MinValue;
        foreach (var mf in visual.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            Bounds mb = mesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3(
                    (c & 1) == 0 ? mb.min.x : mb.max.x,
                    (c & 2) == 0 ? mb.min.y : mb.max.y,
                    (c & 4) == 0 ? mb.min.z : mb.max.z);
                Vector3 p = root.InverseTransformPoint(
                    mf.transform.TransformPoint(corner));
                if (!has || p.z > maxZ)
                {
                    maxZ = p.z;
                    has = true;
                }
            }
        }
        return has ? Mathf.Max(0.04f, maxZ) : 0.11f;
    }

    /// <summary>Centre of the hammer head in its pivot's local space.</summary>
    static Vector3 MeasureHammerHeadLocal(Transform pivot)
    {
        bool has = false;
        Bounds local = default(Bounds);
        foreach (var renderer in pivot.GetComponentsInChildren<Renderer>())
        {
            Bounds world = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? world.min.x : world.max.x,
                    (i & 2) == 0 ? world.min.y : world.max.y,
                    (i & 4) == 0 ? world.min.z : world.max.z);
                Vector3 p = pivot.InverseTransformPoint(corner);
                if (!has) { local = new Bounds(p, Vector3.zero); has = true; }
                else local.Encapsulate(p);
            }
        }
        if (!has) return new Vector3(0f, 0f, 0.22f);
        return new Vector3(local.center.x, local.center.y,
            local.max.z - local.size.z * 0.18f);
    }

    void SetToolColliders(int index, bool enabled)
    {
        if (toolColliders == null || index < 0 || index >= toolColliders.Length)
            return;
        var cols = toolColliders[index];
        if (cols == null) return;
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) cols[i].enabled = enabled;
    }

    /// <summary>Put every tool back in its slot and forget the selection.</summary>
    public void ResetToShelf()
    {
        if (tools == null) return;
        for (int i = 0; i < tools.Length; i++)
        {
            if (tools[i] == null) continue;
            tools[i].localPosition = homePosition[i];
            tools[i].localRotation = homeRotation[i];
            tools[i].localScale = homeScale[i];
            flyT[i] = 1f;
            returning[i] = false;
            SetToolColliders(i, true);
        }
        if (chiselHammer != null)
        {
            chiselHammer.localPosition = hammerTrayLocalPosition;
            chiselHammer.localRotation = Quaternion.Euler(hammerTrayLocalEuler);
        }
        selected = -1;
        if (cleaning != null) lastActive = ToolToIndex(cleaning.tool);
    }

    void Update()
    {
        if (cam == null) return;

        if (cleaning != null)
        {
            int active = ToolToIndex(cleaning.tool);
            if (!initialized)
            {
                initialized = true;
                lastActive = active;
            }
            else if (active != lastActive)
            {
                Select(active, false);
                lastActive = active;
            }
        }

        if (Input.GetMouseButtonDown(0)) TryPick();

        float dt = Time.deltaTime;
        bool working = selected >= 0 && cleaning != null && cleaning.IsCleaningNow;
        workBlend = Mathf.MoveTowards(workBlend, working ? 1f : 0f,
            dt * workBlendSharpness);
        if (working)
        {
            workTime += dt;
            if (selected == 2)
            {
                // Begin just before the strike, never with the hammer already
                // pressed into the chisel.
                if (!wasWorking) strikePhase = 0.45f;
                else strikePhase = Mathf.Repeat(
                    strikePhase + dt * hammerStrikesPerSecond, 1f);
            }
        }
        else
        {
            workTime = 0f;
            strikePhase = 0f;
        }
        wasWorking = working;
        strikeImpact = working && selected == 2 ? HammerImpact(strikePhase) : 0f;

        AnimateTools(dt);
        AnimateHammer(dt);
    }

    void AnimateHammer(float dt)
    {
        if (chiselHammer == null || tools == null || tools[2] == null) return;

        bool held = selected == 2;
        if (!held)
        {
            float trayK = 1f - Mathf.Exp(-14f * Mathf.Max(0.0001f, dt));
            chiselHammer.localPosition = Vector3.Lerp(
                chiselHammer.localPosition, hammerTrayLocalPosition, trayK);
            chiselHammer.localRotation = Quaternion.Slerp(
                chiselHammer.localRotation, Quaternion.Euler(hammerTrayLocalEuler), trayK);
            return;
        }

        float t = Time.time;
        Vector3 idleHead = hammerIdleHeadLocal + new Vector3(
            Mathf.Sin(t * 1.35f) * 0.005f,
            Mathf.Sin(t * 1.75f) * 0.009f,
            Mathf.Cos(t * 1.15f) * 0.004f);
        float swing = HammerSwing(strikePhase);
        Vector3 workHead = Vector3.Lerp(hammerContactHeadLocal, hammerRaisedHeadLocal, swing);
        // A tiny recoil away from the cap after the blow.
        if (strikePhase > 0.70f)
            workHead.z -= Mathf.Sin((strikePhase - 0.70f) / 0.30f * Mathf.PI) * 0.006f;
        Vector3 headLocal = Vector3.Lerp(idleHead, workHead, workBlend);

        Transform chisel = tools[2];
        Vector3 headWorld = chisel.TransformPoint(headLocal);

        // Rotate the hammer so its flat head face, not the barrel, meets the
        // chisel cap. The head's local X axis is aligned with the chisel axis;
        // the handle stays perpendicular and points down-right from the cap.
        Vector3 faceAxis = chisel.forward.normalized;
        Vector3 handleAxis = chisel.TransformDirection(hammerHandleDirectionLocal).normalized;
        handleAxis -= faceAxis * Vector3.Dot(handleAxis, faceAxis);
        if (handleAxis.sqrMagnitude < 0.000001f) handleAxis = chisel.up;
        handleAxis.Normalize();
        Vector3 upAxis = Vector3.Cross(handleAxis, faceAxis).normalized;

        Quaternion targetRot = Quaternion.LookRotation(handleAxis, upAxis);
        Vector3 headOffset = Vector3.Scale(hammerHeadLocal, chiselHammer.lossyScale);
        Vector3 targetPos = headWorld - targetRot * headOffset;

        // Responsive during the fast strike, softer while simply holding.
        float sharpness = workBlend > 0.5f ? 65f : 18f;
        float k = 1f - Mathf.Exp(-sharpness * Mathf.Max(0.0001f, dt));
        // For the visibly held contact, pin the head to the chisel cap
        // instead of letting the smoothing trail a few millimetres behind it.
        if (workBlend > 0.9f && strikePhase >= 0.70f && strikePhase < 0.88f)
            k = 1f;
        chiselHammer.position = Vector3.Lerp(chiselHammer.position, targetPos, k);
        chiselHammer.rotation = Quaternion.Slerp(chiselHammer.rotation, targetRot, k);
    }

    static float HammerSwing(float phase)
    {
        if (phase < 0.45f) return Ease(phase / 0.45f);       // deliberate lift
        if (phase < 0.60f) return 1f;                        // anticipation
        if (phase < 0.70f)
        {
            float t = (phase - 0.60f) / 0.10f;
            return 1f - t * t;                               // accelerating strike
        }
        return 0f;                                           // held on the cap
    }

    static float HammerImpact(float phase)
    {
        return phase >= 0.70f ? 1f - (phase - 0.70f) / 0.30f : 0f;
    }

    void TryPick()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, pickDistance)) return;
        if (hit.collider == null) return;

        for (int i = 0; i < tools.Length; i++)
        {
            if (tools[i] != null && hit.collider.transform.IsChildOf(tools[i]))
            {
                Select(i, true);
                return;
            }
        }
    }

    public void Select(int index, bool applyToCleaning)
    {
        if (tools == null || index < 0 || index >= tools.Length || tools[index] == null) return;

        if (selected == index)
        {
            if (applyToCleaning) ApplyTool(index);
            return;
        }

        if (selected >= 0) StartReturn(selected);

        selected = index;
        lastActive = index;
        workBlend = 0f;
        workTime = 0f;
        wasWorking = false;
        strikePhase = index == 2 ? 0.45f : 0f;
        returning[index] = false;
        flyT[index] = 0f;
        flyStartPos[index] = tools[index].position;
        flyStartRot[index] = tools[index].rotation;
        flyStartScale[index] = tools[index].lossyScale;
        SetToolColliders(index, false);

        if (applyToCleaning) ApplyTool(index);
    }

    void ApplyTool(int index)
    {
        if (cleaning == null) return;
        cleaning.SetTool(IndexToTool(index));
    }

    void StartReturn(int index)
    {
        if (tools[index] == null) return;
        returning[index] = true;
        flyT[index] = 0f;
        flyStartPos[index] = tools[index].position;
        flyStartRot[index] = tools[index].rotation;
        flyStartScale[index] = tools[index].lossyScale;
        SetToolColliders(index, false);
    }

    void AnimateTools(float dt)
    {
        float step = Mathf.Max(0.0001f, dt);

        for (int i = 0; i < tools.Length; i++)
        {
            if (tools[i] == null) continue;

            if (i == selected)
            {
                HandPose(i, out Vector3 handPos, out Quaternion handRot, out Vector3 handScale);

                if (flyT[i] < 1f)
                {
                    flyT[i] = Mathf.Min(1f, flyT[i] + step / Mathf.Max(0.01f, flyDuration));
                    float e = Ease(flyT[i]);
                    Vector3 arc = Vector3.up * Mathf.Sin(e * Mathf.PI) * arcHeight;
                    tools[i].position = Vector3.Lerp(flyStartPos[i], handPos, e) + arc;
                    tools[i].rotation = Quaternion.Slerp(flyStartRot[i], handRot, e);
                    tools[i].localScale = Vector3.Lerp(flyStartScale[i], handScale, e);
                }
                else
                {
                    float k = 1f - Mathf.Exp(-followSharpness * step);
                    tools[i].position = Vector3.Lerp(tools[i].position, handPos, k);
                    tools[i].rotation = Quaternion.Slerp(tools[i].rotation, handRot, k);
                    tools[i].localScale = Vector3.Lerp(tools[i].localScale, handScale, k);
                }
            }
            else if (returning[i])
            {
                HomePose(i, out Vector3 homePos, out Quaternion homeRot, out Vector3 homeWorldScale);

                if (flyT[i] < 1f)
                {
                    flyT[i] = Mathf.Min(1f, flyT[i] + step / Mathf.Max(0.01f, returnDuration));
                    float e = Ease(flyT[i]);
                    Vector3 arc = Vector3.up * Mathf.Sin(e * Mathf.PI) * arcHeight;
                    tools[i].position = Vector3.Lerp(flyStartPos[i], homePos, e) + arc;
                    tools[i].rotation = Quaternion.Slerp(flyStartRot[i], homeRot, e);
                    tools[i].localScale = Vector3.Lerp(flyStartScale[i], homeWorldScale, e);
                }

                if (flyT[i] >= 1f)
                {
                    tools[i].localPosition = homePosition[i];
                    tools[i].localRotation = homeRotation[i];
                    tools[i].localScale = homeScale[i];
                    returning[i] = false;
                    SetToolColliders(i, true);
                }
            }
        }
    }

    void HandPose(int index, out Vector3 position, out Quaternion rotation, out Vector3 worldScale)
    {
        worldScale = Vector3.Scale(homeScale[index], transform.lossyScale) * heldScale;
        if (heldScaleMultiplier != null && index < heldScaleMultiplier.Length)
            worldScale *= heldScaleMultiplier[index];

        Vector3 target = cleaning != null && cleaning.targetRenderer != null
            ? cleaning.targetRenderer.bounds.center
            : cam.transform.TransformPoint(handLocalPosition);

        float distance = Vector3.Distance(cam.transform.position, target);
        float reach = tipLocalZ[index] * Mathf.Max(0.01f, worldScale.z);

        // When the cursor is over the specimen, place the working tip on the
        // surface and pull the handle back toward the camera. This makes the
        // tool read as actually touching the bone and stops it from sinking
        // into the mesh when the camera zooms in.
        if (followCursorOnBone && cleaning != null && cleaning.IsReady &&
            cleaning.targetRenderer != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (cleaning.TryGetSurfaceHit(ray, out Vector3 surfacePoint, out Vector3 surfaceNormal))
            {
                Vector3 tip = surfacePoint + surfaceNormal.normalized * contactClearance;
                // Put the actual tip on the cursor, but hold the handle out to
                // the lower-right so the shaft and hammer stay visible in
                // profile instead of pointing straight into the camera.
                Vector3 handAnchor = tip
                    - cam.transform.forward * 0.16f
                    + cam.transform.right * 0.10f
                    - cam.transform.up * 0.08f;
                Vector3 approach = tip - handAnchor;
                if (approach.sqrMagnitude < 0.0001f) approach = cam.transform.forward;
                approach.Normalize();

                position = tip - approach * (reach + contactClearance);
                rotation = Quaternion.LookRotation(approach, cam.transform.up)
                           * ToolEuler(index);

                // Distinct contact motions for each prep tool. The amplitudes
                // stay inside the cleaning radius, so the visual tip remains
                // on the same spot that the exact cursor raycast cleans.
                if (index == 0)
                {
                    float s = Mathf.Sin(workTime * 10f);
                    position += cam.transform.right * (s * 0.010f * workBlend);
                    position += approach * (Mathf.Cos(workTime * 10f) * 0.003f * workBlend);
                    rotation *= Quaternion.Euler(0f, 0f, s * 4f * workBlend);
                }
                else if (index == 1)
                {
                    float s = Mathf.Sin(workTime * 7f);
                    position += approach * (s * 0.008f * workBlend);
                    position += surfaceNormal.normalized *
                        (Mathf.Max(0f, -s) * 0.003f * workBlend);
                    rotation *= Quaternion.Euler(s * 3f * workBlend, 0f, 0f);
                }
                else
                {
                    // Hammer impact drives the chisel a few millimetres forward,
                    // with a short vibration rather than a continuous scrub.
                    position += approach * (0.003f * strikeImpact);
                    position += cam.transform.up *
                        (Mathf.Sin(Time.time * 95f) * 0.0015f * strikeImpact);
                    rotation *= Quaternion.Euler(
                        strikeImpact * 1.2f, 0f, strikeImpact * 0.8f);
                }
                ApplyIdleSway(index, ref position, ref rotation, 0.30f);
                return;
            }
        }

        // No surface under the cursor (or cleaning is off): keep the old
        // camera-relative grip, but bring it closer as the camera zooms so it
        // cannot end up behind the specimen.
        float surfaceRadius = cleaning != null && cleaning.targetRenderer != null
            ? cleaning.targetRenderer.bounds.extents.magnitude * 0.65f
            : 0.18f;
        float fallbackDepth = Mathf.Clamp(distance - surfaceRadius - 0.16f,
                                          minHeldDepth, handLocalPosition.z);
        float fallbackScale = fallbackDepth / Mathf.Max(0.01f, handLocalPosition.z);

        position = cam.transform.TransformPoint(new Vector3(
            handLocalPosition.x * fallbackScale,
            handLocalPosition.y * fallbackScale,
            fallbackDepth));
        if (heldExtraLift != null && index < heldExtraLift.Length)
            position += cam.transform.up * heldExtraLift[index];

        Vector3 dir = target + Vector3.up * 0.03f - position;
        if (dir.sqrMagnitude < 0.0001f) dir = cam.transform.forward;

        rotation = Quaternion.LookRotation(dir.normalized, cam.transform.up)
                   * ToolEuler(index);
        ApplyIdleSway(index, ref position, ref rotation, 1f);
    }

    void ApplyIdleSway(int index, ref Vector3 position, ref Quaternion rotation, float weight)
    {
        float phase = Time.time + index * 0.73f;
        float amount = idleSwayPosition * weight;
        position += cam.transform.right * (Mathf.Sin(phase * 1.35f) * amount);
        position += cam.transform.up * (Mathf.Sin(phase * 1.80f + 0.6f) * amount * 1.15f);
        float degrees = idleSwayDegrees * weight;
        rotation *= Quaternion.Euler(
            Mathf.Sin(phase * 1.15f) * degrees,
            Mathf.Cos(phase * 1.40f) * degrees * 0.65f,
            Mathf.Sin(phase * 1.65f + 0.4f) * degrees);
    }

    Quaternion ToolEuler(int index)
    {
        float extra = heldExtraRoll != null && index < heldExtraRoll.Length
            ? heldExtraRoll[index] : 0f;
        return Quaternion.Euler(handLocalEuler.x, handLocalEuler.y,
                                handLocalEuler.z + extra);
    }

    void HomePose(int index, out Vector3 position, out Quaternion rotation, out Vector3 worldScale)
    {
        position = transform.TransformPoint(homePosition[index]);
        rotation = transform.rotation * homeRotation[index];
        worldScale = Vector3.Scale(homeScale[index], transform.lossyScale);
    }

    static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static BoneCleaningV2.Tool IndexToTool(int index)
    {
        if (index == 0) return BoneCleaningV2.Tool.Dust;
        if (index == 1) return BoneCleaningV2.Tool.Clay;
        return BoneCleaningV2.Tool.Crust;
    }

    static int ToolToIndex(BoneCleaningV2.Tool tool)
    {
        if (tool == BoneCleaningV2.Tool.Dust) return 0;
        if (tool == BoneCleaningV2.Tool.Clay) return 1;
        return 2;
    }
}
