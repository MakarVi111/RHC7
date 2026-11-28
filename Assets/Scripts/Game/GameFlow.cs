using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single-room specimen prep loop:
/// crate -> take a bone -> clean it -> identify the species -> next bone.
/// There is no menu: all state and prompts live in the HUD.
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum State
    {
        Closed,
        Opening,
        Inspecting,
        Taking,
        Cleaning,
        BookReady,
        Identifying,
        Identified,
        Returning,
        Complete
    }

    [Header("Scene")]
    public Camera cam;
    public Transform crateRoot;
    public Transform crateLid;
    public Transform prepPad;
    public Transform referenceBook;
    public Transform bookLid;
    public Transform[] finishedSlots;
    public LevelDefinition level;
    public SpeciesDefinition[] speciesDefinitions;
    public Light crateLight;
    public GameBone[] bones;
    public BoneCleaningV2 cleaning;
    public ToolShelfInteraction tools;

    [Header("Cameras")]
    public Vector3 crateViewPosition = new Vector3(2.00f, 3.80f, -0.90f);
    public Vector3 crateViewTarget = new Vector3(2.30f, 1.00f, 0.42f);
    public float crateViewFov = 50f;

    public Vector3 prepViewPosition = new Vector3(0.24f, 1.80f, -1.72f);
    public Vector3 prepViewTarget = new Vector3(0.24f, 0.98f, -0.02f);
    public float prepViewFov = 46f;
    public Vector3 finishedViewPosition = new Vector3(-2.00f, 3.80f, -0.90f);
    public Vector3 finishedViewTarget = new Vector3(-2.30f, 1.00f, 0.42f);
    public float finishedViewFov = 50f;



    [Header("Animation")]
    public float lidOpenAngle = 105f;
    public float openDuration = 0.55f;
    public float cameraDuration = 0.72f;
    public float flightDuration = 0.95f;


    [Header("Cleaning")]
    [Range(0.30f, 0.95f)] public float cleanThreshold = 0.60f;

    [Header("HUD")]
    public bool showHud = true;

    public GameBone CurrentBone => current;
    public int SolvedCount => solved;
    public int BoneCount => bones != null ? bones.Length : 0;
    public bool Complete => state == State.Complete;
    public bool IsBookReady => state == State.BookReady;
    public bool IsIdentifying => state == State.Identifying;
    public string CurrentHint => current != null ? current.identificationHint : "";

    State state = State.Closed;
    GameBone current;
    GameBone hovered;
    bool hoverCrate;
    int solved;
    int wrongAttempts;
    float messageTimer;
    string message = "";

    Vector3[] cratePos;
    Quaternion[] crateRot;
    Vector3[] crateScale;

    Quaternion lidClosed;
    float lidT;

    bool cameraMoving;
    float cameraT;
    float cameraTime;
    Vector3 camStartPos, camEndPos;
    Quaternion camStartRot, camEndRot;
    float camStartFov, camEndFov;

    bool boneMoving;
    float boneT;
    float boneTime;
    Vector3 boneStartPos, boneEndPos;
    Quaternion boneStartRot, boneEndRot;
    Vector3 boneStartScale, boneEndScale;
    float boneArc;
    float boneClearance;

    Quaternion bookClosed;
    float bookT;

    GUIStyle panelStyle, titleStyle, bodyStyle, smallStyle, buttonStyle, messageStyle;
    GUIStyle bookTitleStyle, bookBodyStyle, bookSmallStyle, bookButtonStyle;
    bool stylesReady;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cleaning == null) cleaning = Object.FindFirstObjectByType<BoneCleaningV2>();
        if (tools == null) tools = Object.FindFirstObjectByType<ToolShelfInteraction>();

        if (bones == null || bones.Length == 0)
            bones = Object.FindObjectsByType<GameBone>(FindObjectsSortMode.None);

        cratePos = new Vector3[bones.Length];
        crateRot = new Quaternion[bones.Length];
        crateScale = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            bones[i].Cache();
            cratePos[i] = bones[i].transform.position;
            crateRot[i] = bones[i].transform.rotation;
            crateScale[i] = bones[i].transform.localScale;
            bones[i].SetPickable(false);
        }

        if (crateLid != null) lidClosed = crateLid.localRotation;
        if (bookLid != null) bookClosed = bookLid.localRotation;
        if (cleaning != null)
        {
            cleaning.enabled = false;
            // The old text panel is replaced by the paper note.
            cleaning.showHud = false;
            cleaning.SetWorkView(prepViewPosition,
                Quaternion.LookRotation((prepViewTarget - prepViewPosition).normalized));
        }
        if (tools != null) tools.enabled = false;
        if (crateLight != null) crateLight.enabled = false;
        if (prepPad == null)
        {
            var pad = GameObject.Find("BonePadV2");
            if (pad != null) prepPad = pad.transform;
        }

        state = State.Closed;
        solved = 0;
        wrongAttempts = 0;
        message = "";
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        SetCamera(crateViewPosition, crateViewTarget, crateViewFov);
        SetLid(0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        UpdateCamera(dt);
        UpdateBoneFlight(dt);
        UpdateLid(dt);
        UpdateBook(dt);

        if (messageTimer > 0f) messageTimer -= dt;

        switch (state)
        {
            case State.Closed:
                UpdateHover();
                if (hoverCrate && Input.GetMouseButtonDown(0)) BeginOpening();
                break;

            case State.Opening:
                if (lidT >= 1f && !cameraMoving && !boneMoving)
                {
                    state = State.Inspecting;
                    SetBonesPickable(true);
                    hovered = null;
                }
                break;

            case State.Inspecting:
                UpdateHover();
                if (hovered != null && Input.GetMouseButtonDown(0))
                    BeginTake(hovered);
                break;

            case State.Taking:
                if (!boneMoving && !cameraMoving) FinishTake();
                break;

            case State.Cleaning:
                if (cleaning != null && cleaning.TotalCleanFraction >= cleanThreshold)
                    BeginBookReady();
                break;

            case State.BookReady:
                if (Input.GetMouseButtonDown(0) && BookClicked())
                    BeginIdentifying();
                break;

            case State.Identified:
                if (!boneMoving)
                {
                    if (solved >= bones.Length)
                    {
                        state = State.Complete;
                    }
                    else
                    {
                        MoveCamera(crateViewPosition, crateViewTarget, crateViewFov);
                        state = State.Returning;
                    }
                }
                break;

            case State.Returning:
                if (!cameraMoving)
                {
                    current = null;
                    hovered = null;
                    SetBonesPickable(true);
                    state = State.Inspecting;
                }
                break;
        }
    }

    // ------------------------------------------------------------- crate

    void UpdateHover()
    {
        hoverCrate = false;
        hovered = null;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            return;

        if (state == State.Closed)
        {
            if (crateRoot != null && hit.collider.transform.IsChildOf(crateRoot))
                hoverCrate = true;
            return;
        }

        if (state == State.Inspecting)
        {
            var bone = hit.collider.GetComponentInParent<GameBone>();
            if (bone != null && !bone.identified && bone.gameObject.activeInHierarchy)
                hovered = bone;
        }
    }

    void BeginOpening()
    {
        state = State.Opening;
        lidT = 0f;
        hoverCrate = false;
        hovered = null;
        SetBonesPickable(false);
        if (crateLight != null) crateLight.enabled = true;
        MoveCamera(crateViewPosition, crateViewTarget, crateViewFov);
        ShowMessage("ЯЩИК ОТКРЫТ · ВЫБЕРИТЕ КОСТЬ", 2f);
    }

    void UpdateLid(float dt)
    {
        if (state != State.Opening && state != State.Inspecting &&
            state != State.Taking && state != State.Cleaning)
            return;

        float target = state == State.Opening ? 1f : 1f;
        lidT = Mathf.MoveTowards(lidT, target, dt / Mathf.Max(0.01f, openDuration));
        SetLid(lidT);
    }

    void SetLid(float t)
    {
        if (crateLid == null) return;
        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
        crateLid.localRotation = lidClosed * Quaternion.Euler(lidOpenAngle * e, 0f, 0f);
    }

    // ------------------------------------------------------------- take / clean

    void BeginTake(GameBone bone)
    {
        if (bone == null) return;
        state = State.Taking;
        current = bone;
        hovered = null;
        hoverCrate = false;
        SetBonesPickable(false);

        Quaternion prepRot = Quaternion.Euler(bone.prepEuler);
        Quaternion crateRotation = bone.transform.rotation;
        Vector3 startPivot = bone.transform.position;
        Bounds startBounds = bone.WorldBounds();
        float startBottom = startPivot.y - startBounds.min.y;
        bone.transform.rotation = prepRot;

        Bounds b = bone.WorldBounds();
        float padTop = prepPad != null
            ? prepPad.GetComponentInChildren<Renderer>().bounds.max.y
            : 0.945f;
        Vector3 padCenter = prepPad != null ? prepPad.position : Vector3.zero;
        Vector3 targetPos = bone.transform.position + new Vector3(
            padCenter.x - b.center.x,
            padTop - b.min.y,
            padCenter.z - b.center.z);
        bone.transform.rotation = crateRotation;

        // Lift clear of the crate before travelling sideways. The rim sits at
        // the lid pivot; the bone's lowest point must clear it with margin.
        float rimY = crateLid != null ? crateLid.position.y : startPivot.y + 0.85f;
        float peakPivotY = rimY + 0.20f + startBottom;
        float clearance = Mathf.Max(0.75f, peakPivotY - Mathf.Min(startPivot.y, targetPos.y));
        StartBoneFlight(targetPos, prepRot, bone.transform.localScale, flightDuration, 0.18f, clearance);
        MoveCamera(prepViewPosition, prepViewTarget, prepViewFov);
        ShowMessage(bone.partName.ToUpperInvariant() + " · ОЧИСТИТЕ ОБРАЗЕЦ", 2.2f);
    }

    void FinishTake()
    {
        if (current == null) return;
        state = State.Cleaning;

        if (cleaning != null)
        {
            cleaning.enabled = true;
            cleaning.SetWorkView(prepViewPosition,
                Quaternion.LookRotation((prepViewTarget - prepViewPosition).normalized));
            cleaning.SetTarget(current.boneRenderer, current.boneMesh, current.transform,
                current.boneMaterial, null, current.partName);
        }
        if (tools != null) tools.enabled = true;
        StartCoroutine(SelectDefaultTool());
    }

    System.Collections.IEnumerator SelectDefaultTool()
    {
        yield return null;
        if (tools != null && state == State.Cleaning)
            tools.Select(0, true);
    }

    void BeginBookReady()
    {
        state = State.BookReady;
        if (tools != null)
        {
            tools.ResetToShelf();
            tools.enabled = false;
        }
        if (cleaning != null) cleaning.enabled = false;
        ShowMessage("КОСТЬ ОЧИЩЕНА · ОТКРОЙТЕ КНИГУ-ОПРЕДЕЛИТЕЛЬ", 4.0f);
    }

    void BeginIdentifying()
    {
        if (state != State.BookReady) return;
        state = State.Identifying;
        ShowMessage("ОПРЕДЕЛИТЕ ПОРОДУ ПО КНИГЕ", 2.5f);
    }

    bool BookClicked()
    {
        if (referenceBook == null || cam == null) return false;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        return hit.collider.transform.IsChildOf(referenceBook);
    }

    void UpdateBook(float dt)
    {
        if (bookLid == null) return;
        float target = state == State.Identifying ? 1f : 0f;
        bookT = Mathf.MoveTowards(bookT, target, dt / 0.4f);
        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(bookT));
        bookLid.localRotation = bookClosed * Quaternion.Euler(108f * e, 0f, 0f);
    }

    // ------------------------------------------------------------- identify

    void SubmitIdentification(DinoSpecies guess)
    {
        if (state != State.Identifying || current == null) return;

        if (guess == current.species)
        {
            solved++;
            wrongAttempts = 0;
            current.identified = true;
            current.SetPickable(false);
            state = State.Identified;

            string speciesName = current.species == DinoSpecies.Trex
                ? "ТИРАННОЗАВР"
                : "ТРИЦЕРАТОПС";
            ShowMessage("ВЕРНО · " + speciesName + " · " + current.partName.ToUpperInvariant(), 2.4f);

            // The identified specimen gets a paper tag and moves to the
            // matching collection crate on the left.
            if (current.nameTag != null) current.nameTag.gameObject.SetActive(true);

            int index = System.Array.IndexOf(bones, current);
            if (index < 0) index = 0;
            int slotCount = finishedSlots != null ? finishedSlots.Length : 0;
            int slotIndex = Mathf.Clamp(solved - 1, 0, Mathf.Max(0, slotCount - 1));
            Transform slot = slotCount > 0 ? finishedSlots[slotIndex] : null;
            Vector3 endPos = slot != null ? slot.position : cratePos[index];
            Quaternion endRot = slot != null ? slot.rotation : crateRot[index];
            Vector3 endScale = crateScale[index];

            float bottom = current.transform.position.y - current.WorldBounds().min.y;
            float rimY = crateLid != null ? crateLid.position.y : endPos.y + 0.85f;
            float peakPivotY = rimY + 0.20f + bottom;
            float clearance = Mathf.Max(0.75f,
                peakPivotY - Mathf.Min(current.transform.position.y, endPos.y));
            StartBoneFlight(endPos, endRot, endScale, 1.0f, 0.12f, clearance);
            MoveCamera(finishedViewPosition, finishedViewTarget, finishedViewFov);
        }
        else
        {
            wrongAttempts++;
            string speciesName = guess == DinoSpecies.Trex ? "ТИРАННОЗАВР" : "ТРИЦЕРАТОПС";
            string hint = wrongAttempts >= 1 && !string.IsNullOrEmpty(current.identificationHint)
                ? "\n" + current.identificationHint : "";
            ShowMessage("НЕВЕРНО · " + speciesName + "?" + hint, 3.2f);
        }
    }

    // ------------------------------------------------------------- motion

    void SetBonesPickable(bool on)
    {
        if (bones == null) return;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            bones[i].SetPickable(on && !bones[i].identified);
        }
    }

    void StartBoneFlight(Vector3 endPos, Quaternion endRot, Vector3 endScale,
                         float duration, float arc, float crateClearance = 0f)
    {
        if (current == null) return;
        boneStartPos = current.transform.position;
        boneStartRot = current.transform.rotation;
        boneStartScale = current.transform.localScale;
        boneEndPos = endPos;
        boneEndRot = endRot;
        boneEndScale = endScale;
        boneTime = Mathf.Max(0.01f, duration);
        boneT = 0f;
        boneArc = arc;
        boneClearance = crateClearance;
        boneMoving = true;
    }

    void UpdateBoneFlight(float dt)
    {
        if (!boneMoving || current == null) return;

        boneT = Mathf.Min(1f, boneT + dt / boneTime);
        if (boneClearance > 0.001f)
        {
            // Vertical lift -> level transfer -> vertical lowering. Rotating
            // only once clear of the crate stops the mesh cutting the walls.
            const float liftEnd = 0.34f;
            const float moveEnd = 0.70f;
            float eMove = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(liftEnd, moveEnd, boneT));
            float lift = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0f, liftEnd, boneT));
            float lower = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(moveEnd, 1f, boneT));

            Vector3 position = Vector3.Lerp(boneStartPos, boneEndPos, eMove);
            position.y = Mathf.Lerp(boneStartPos.y, boneEndPos.y, eMove)
                + boneClearance * lift * (1f - lower);
            current.transform.position = position;
            current.transform.rotation = Quaternion.Slerp(boneStartRot, boneEndRot, eMove);
            current.transform.localScale = Vector3.Lerp(boneStartScale, boneEndScale, eMove);
        }
        else
        {
            float e = Mathf.SmoothStep(0f, 1f, boneT);
            Vector3 arc = Vector3.up * Mathf.Sin(boneT * Mathf.PI) * boneArc;
            current.transform.position = Vector3.Lerp(boneStartPos, boneEndPos, e) + arc;
            current.transform.rotation = Quaternion.Slerp(boneStartRot, boneEndRot, e);
            current.transform.localScale = Vector3.Lerp(boneStartScale, boneEndScale, e);
        }

        if (boneT >= 1f)
        {
            current.transform.position = boneEndPos;
            current.transform.rotation = boneEndRot;
            current.transform.localScale = boneEndScale;
            boneMoving = false;
        }
    }

    void MoveCamera(Vector3 pos, Vector3 target, float fov)
    {
        if (cam == null) return;
        camStartPos = cam.transform.position;
        camStartRot = cam.transform.rotation;
        camStartFov = cam.fieldOfView;
        camEndPos = pos;
        camEndRot = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
        camEndFov = fov;
        cameraT = 0f;
        cameraTime = Mathf.Max(0.01f, cameraDuration);
        cameraMoving = true;
    }

    void SetCamera(Vector3 pos, Vector3 target, float fov)
    {
        if (cam == null) return;
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
        cam.fieldOfView = fov;
    }

    void UpdateCamera(float dt)
    {
        if (!cameraMoving || cam == null) return;
        cameraT = Mathf.Min(1f, cameraT + dt / cameraTime);
        float e = Mathf.SmoothStep(0f, 1f, cameraT);
        cam.transform.position = Vector3.Lerp(camStartPos, camEndPos, e);
        cam.transform.rotation = Quaternion.Slerp(camStartRot, camEndRot, e);
        cam.fieldOfView = Mathf.Lerp(camStartFov, camEndFov, e);
        if (cameraT >= 1f)
        {
            cam.transform.position = camEndPos;
            cam.transform.rotation = camEndRot;
            cam.fieldOfView = camEndFov;
            cameraMoving = false;
        }
    }

    void ShowMessage(string text, float duration)
    {
        message = text;
        messageTimer = duration;
    }

    // ------------------------------------------------------------- HUD

    void EnsureStyles()
    {
        if (stylesReady) return;
        stylesReady = true;

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.fontSize = 14;
        panelStyle.alignment = TextAnchor.UpperLeft;
        panelStyle.padding = new RectOffset(14, 14, 12, 12);
        panelStyle.normal.textColor = new Color(0.93f, 0.89f, 0.79f);

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.90f, 0.74f, 0.45f);

        bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.fontSize = 14;
        bodyStyle.wordWrap = true;
        bodyStyle.normal.textColor = new Color(0.90f, 0.86f, 0.76f);

        smallStyle = new GUIStyle(bodyStyle);
        smallStyle.fontSize = 12;
        smallStyle.normal.textColor = new Color(0.76f, 0.72f, 0.64f);

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 16;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.alignment = TextAnchor.MiddleCenter;

        messageStyle = new GUIStyle(GUI.skin.label);
        messageStyle.fontSize = 18;
        messageStyle.fontStyle = FontStyle.Bold;
        messageStyle.alignment = TextAnchor.MiddleCenter;
        messageStyle.wordWrap = true;
        messageStyle.normal.textColor = new Color(1f, 0.90f, 0.66f);

        Font bookFont = null;
        try
        {
            bookFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Georgia", "Cambria", "Times New Roman", "Arial" }, 20);
        }
        catch { }

        bookTitleStyle = new GUIStyle(GUI.skin.label);
        bookTitleStyle.font = bookFont;
        bookTitleStyle.fontSize = 25;
        bookTitleStyle.fontStyle = FontStyle.Bold;
        bookTitleStyle.alignment = TextAnchor.MiddleCenter;
        bookTitleStyle.normal.textColor = new Color(0.22f, 0.13f, 0.07f);

        bookBodyStyle = new GUIStyle(GUI.skin.label);
        bookBodyStyle.font = bookFont;
        bookBodyStyle.fontSize = 15;
        bookBodyStyle.wordWrap = true;
        bookBodyStyle.alignment = TextAnchor.UpperCenter;
        bookBodyStyle.normal.textColor = new Color(0.28f, 0.19f, 0.11f);

        bookSmallStyle = new GUIStyle(GUI.skin.label);
        bookSmallStyle.font = bookFont;
        bookSmallStyle.fontSize = 13;
        bookSmallStyle.fontStyle = FontStyle.Italic;
        bookSmallStyle.alignment = TextAnchor.MiddleCenter;
        bookSmallStyle.wordWrap = true;
        bookSmallStyle.normal.textColor = new Color(0.45f, 0.16f, 0.10f);

        bookButtonStyle = new GUIStyle(GUI.skin.label);
        bookButtonStyle.font = bookFont;
        bookButtonStyle.fontSize = 21;
        bookButtonStyle.fontStyle = FontStyle.Bold;
        bookButtonStyle.alignment = TextAnchor.MiddleCenter;
        bookButtonStyle.normal.textColor = new Color(0.34f, 0.12f, 0.07f);
        bookButtonStyle.hover.textColor = new Color(0.62f, 0.18f, 0.08f);
    }

    void OnGUI()
    {
        if (!showHud) return;
        EnsureStyles();

        if (state == State.Identifying) DrawIdentificationPanel();
        if (state == State.Complete) DrawCompletePanel();

        if (messageTimer > 0f && !string.IsNullOrEmpty(message))
        {
            var r = new Rect(Screen.width * 0.18f, 20f, Screen.width * 0.64f, 72f);
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 24, r.height - 16), message, messageStyle);
        }
    }

    void DrawStatusPanel()
    {
        float w = 330f;
        var r = new Rect(Screen.width - w - 16f, 16f, w, 158f);
        GUI.Box(r, GUIContent.none, panelStyle);
        GUILayout.BeginArea(new Rect(r.x + 14, r.y + 12, r.width - 28, r.height - 24));
        GUILayout.Label("РХЦ-7 · ПОДГОТОВКА", titleStyle);
        GUILayout.Label("ОПРЕДЕЛЕНО: " + solved + " / " + bones.Length, bodyStyle);
        GUILayout.Space(2);

        switch (state)
        {
            case State.Closed:
                GUILayout.Label("Ящик закрыт.", bodyStyle);
                GUILayout.Label("ЛКМ по ящику — открыть.", bodyStyle);
                break;
            case State.Opening:
                GUILayout.Label("Ящик открывается…", bodyStyle);
                break;
            case State.Inspecting:
                GUILayout.Label("Выберите кость в ящике.", bodyStyle);
                if (hovered != null)
                    GUILayout.Label("Наведено: " + hovered.partName, smallStyle);
                break;
            case State.Taking:
                GUILayout.Label("Кость переносится на стол.", bodyStyle);
                break;
            case State.Cleaning:
                GUILayout.Label("Очистите кость.", bodyStyle);
                if (cleaning != null)
                    GUILayout.Label("Очищено: " + (cleaning.TotalCleanFraction * 100f).ToString("F1") + "%" +
                                    " · нужно " + (cleanThreshold * 100f).ToString("F0") + "%", smallStyle);
                GUILayout.Label("Инструмент — на лотке справа.", smallStyle);
                break;
            case State.Identifying:
                GUILayout.Label("Кость очищена.", bodyStyle);
                GUILayout.Label("Определите вид справа.", bodyStyle);
                break;
            case State.Identified:
                GUILayout.Label("Образец определён.", bodyStyle);
                break;
            case State.Returning:
                GUILayout.Label("Возврат к ящику.", bodyStyle);
                break;
            case State.Complete:
                GUILayout.Label("Все 3 образца определены.", bodyStyle);
                break;
        }
        GUILayout.EndArea();
    }

    void DrawIdentificationPanel()
    {
        if (current == null) return;
        float s = Mathf.Clamp(Screen.height / 1080f, 0.82f, 1.12f);
        float w = 1010f * s;
        float h = 610f * s;
        var r = new Rect(Screen.width * 0.5f - w * 0.5f,
                         Screen.height * 0.5f - h * 0.5f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.40f);
        GUI.DrawTexture(new Rect(r.x + 9, r.y + 12, r.width, r.height),
            Texture2D.whiteTexture);
        GUI.color = new Color(0.17f, 0.08f, 0.045f, 1f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        var pages = new Rect(r.x + 25f * s, r.y + 30f * s,
                             r.width - 50f * s, r.height - 55f * s);
        GUI.color = new Color(0.86f, 0.80f, 0.66f, 1f);
        GUI.DrawTexture(pages, Texture2D.whiteTexture);

        float spineW = 16f * s;
        float pageW = (pages.width - spineW) * 0.5f;
        var left = new Rect(pages.x, pages.y, pageW, pages.height);
        var right = new Rect(pages.x + pageW + spineW, pages.y, pageW, pages.height);
        GUI.color = new Color(0.37f, 0.27f, 0.16f, 0.60f);
        GUI.DrawTexture(new Rect(pages.center.x - spineW * 0.5f, pages.y,
                                 spineW, pages.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.25f, 0.16f, 0.09f, 0.30f);
        GUI.DrawTexture(new Rect(left.x, left.y, 1, left.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(right.xMax - 1, right.y, 1, right.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(pages.x, r.y + 5f * s, pages.width, 24f * s),
            "ОПРЕДЕЛИТЕЛЬ КОСТЕЙ · " + current.partName.ToUpperInvariant(),
            bookSmallStyle);

        SpeciesDefinition leftSpecies = speciesDefinitions != null && speciesDefinitions.Length > 0
            ? speciesDefinitions[0] : null;
        SpeciesDefinition rightSpecies = speciesDefinitions != null && speciesDefinitions.Length > 1
            ? speciesDefinitions[1] : null;
        DrawBookPage(left, leftSpecies);
        DrawBookPage(right, rightSpecies);

        float btnY = pages.yMax - 68f * s;
        float btnW = pageW - 64f * s;
        if (leftSpecies != null && GUI.Button(
                new Rect(left.x + 32f * s, btnY, btnW, 48f * s),
                "ЭТО " + leftSpecies.displayName, bookButtonStyle))
            SubmitIdentification(leftSpecies.speciesValue);
        if (rightSpecies != null && GUI.Button(
                new Rect(right.x + 32f * s, btnY, btnW, 48f * s),
                "ЭТО " + rightSpecies.displayName, bookButtonStyle))
            SubmitIdentification(rightSpecies.speciesValue);

        if (wrongAttempts > 0 && current != null &&
            !string.IsNullOrEmpty(current.identificationHint))
        {
            GUI.Label(new Rect(pages.x + 30f, btnY - 46f, pages.width - 60f, 38f),
                "ПОДСКАЗКА: " + current.identificationHint.ToUpperInvariant(),
                bookSmallStyle);
        }
    }

    void DrawBookPage(Rect page, SpeciesDefinition species)
    {
        float pad = 26f;
        if (species == null)
        {
            GUI.Label(new Rect(page.x + pad, page.y + 28f,
                page.width - pad * 2, 40f), "НЕТ ДАННЫХ ВИДА", bookSmallStyle);
            return;
        }
        GUI.Label(new Rect(page.x + pad, page.y + 12f, page.width - pad * 2, 18f),
            species.pageEyebrow, bookSmallStyle);
        GUI.Label(new Rect(page.x + pad, page.y + 31f, page.width - pad * 2, 34f),
            species.pageTitle, bookTitleStyle);

        float artH = species.extraArt != null ? 128f : 190f;
        var artRect = new Rect(page.x + pad, page.y + 70f,
                               page.width - pad * 2, artH);
        if (species.mainArt != null)
            GUI.DrawTexture(artRect, species.mainArt, ScaleMode.ScaleToFit);
        float descriptionY = page.y + 266f;
        if (species.extraArt != null)
        {
            var extra = new Rect(page.x + page.width * 0.30f, artRect.yMax + 2f,
                                 page.width * 0.40f, 76f);
            GUI.DrawTexture(extra, species.extraArt, ScaleMode.ScaleToFit);
            descriptionY = page.y + 288f;
        }
        GUI.Label(new Rect(page.x + pad, descriptionY,
                           page.width - pad * 2, 76f), species.description, bookBodyStyle);
        GUI.color = new Color(0.28f, 0.18f, 0.10f, 0.35f);
        GUI.DrawTexture(new Rect(page.x + pad, page.yMax - 91f,
                                 page.width - pad * 2, 1), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawCompletePanel()
    {
        float w = 500f;
        var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - 190f, w, 158f);
        GUI.Box(r, GUIContent.none, panelStyle);
        GUILayout.BeginArea(new Rect(r.x + 18, r.y + 16, r.width - 36, r.height - 32));
        GUILayout.Label("ВСЕ 3 ОБРАЗЦА ОПРЕДЕЛЕНЫ", titleStyle);
        GUILayout.Label("Tyrannosaurus rex и Triceratops horridus", bodyStyle);
        GUILayout.Space(10);
        if (GUILayout.Button("НАЧАТЬ ЗАНОВО", buttonStyle, GUILayout.Height(52)))
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }
        GUILayout.EndArea();
    }
}
