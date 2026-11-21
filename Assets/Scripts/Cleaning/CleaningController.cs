using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

namespace RHC7
{
    /// <summary>
    /// Core cleaning loop: hold LMB and move the mouse over the bone.
    /// Speed both removes rock and wears the bone; the last 20% is the risky part.
    /// </summary>
    public class CleaningController : MonoBehaviour
    {
        [Header("Scene references")]
        public BoneSurface bone;
        public Camera viewCamera;
        public CinemachineCamera cineCamera;
        public CleaningHud hud;
        public HandToolView handView;
        public LayerMask boneMask;
        public LayerMask toolMask;
        public Transform shardRoot;
        public Material shardMaterial;

        [Header("Bone sequence")]
        public Mesh[] boneMeshes;
        public RockType[] boneRocks;
        public string[] boneNames;
        public float[] boneAspects;
        public float[] boneCircumferences;
        public Vector3[] boneRotations;
        public int currentBoneIndex;

        [Header("Atlas")]
        public string[] speciesNames = { "ТЕРОПОД", "ЦЕРАТОПС", "ЗАУРОПОД" };
        public string[] speciesDescriptions =
        {
            "Узкая и лёгкая кость, плавный изгиб, небольшие суставы. Порода: песчаник.",
            "Короткая и массивная кость, широкие концы. Порода: глина.",
            "Длинная и крупная кость, тяжёлые суставы. Порода: известняк. Нужно зубило."
        };
        public int[] boneSpecies = { 0, 1, 2 };

        private AtlasBook atlasBook;
        private bool awaitingIdentification;
        private int[] boneLabels;
        private int correctLabels;

        [Header("Tuning")]
        public ToolDefinition[] tools;
        public float uvAspect = 2.17f;
        public float boneCircumference = 0.138f;
        public float removalScale = 4.0f;
        public float wearScale = 0.02f;

        private int toolIndex;
        private ToolPickup hoveredPickup;
        private Vector2 lastHitUV;
        private bool hasLastUV;
        private Vector2 lastMousePos;
        private float smoothedSpeed;
        private float baseFov = 42f;
        private ParticleSystem dust;
        private float startTime;
        private float dbgTimer;
        public bool debugInput = false;

        private bool tutorialActive;
        private int tutorialStep;
        private bool hasTutorialLayer;
        private RockLayer lastTutorialLayer;

        private static readonly Color Ivory = new Color(0.93f, 0.88f, 0.78f, 1f);
        private static readonly Color Gold = new Color(0.85f, 0.71f, 0.43f, 1f);
        private static readonly Color Danger = new Color(0.80f, 0.32f, 0.23f, 1f);

        private void Awake()
        {
            ConnectBoneEvents();
        }

        private void ConnectBoneEvents()
        {
            if (bone == null) return;
            bone.StageChanged -= OnStageChanged;
            bone.StageChanged += OnStageChanged;
            bone.Cleaned -= OnCleaned;
            bone.Cleaned += OnCleaned;
            bone.Destroyed -= OnDestroyed;
            bone.Destroyed += OnDestroyed;
        }

        private int BoneCount => boneMeshes != null && boneMeshes.Length > 0 ? boneMeshes.Length : 1;

        private float EffectiveRadiusUV(ToolDefinition tool)
        {
            float circ = bone != null ? Mathf.Max(0.02f, bone.circumference) : 0.138f;
            return tool.radiusUV * (0.138f / circ);
        }

        private float ToolRemovalMultiplier(RockType rock, ToolKind tool)
        {
            if (rock == RockType.Limestone)
            {
                if (tool == ToolKind.Brush) return 0.02f;
                if (tool == ToolKind.Scraper) return 0.15f;
                return 1.15f;
            }

            if (rock == RockType.Clay)
            {
                if (tool == ToolKind.Brush) return 0.65f;
                if (tool == ToolKind.Scraper) return 1.20f;
                return 1.0f;
            }

            return 1.0f;
        }

        private float CurrentAspect => bone != null ? bone.uvAspect : 2.17f;

        private void ApplyBoneDefinition(int index, bool showBanner)
        {
            if (bone == null) return;
            if (boneMeshes == null || boneMeshes.Length == 0)
            {
                if (hud != null) hud.SetBone(1, 1, bone.boneName);
                return;
            }

            int count = BoneCount;
            index = Mathf.Clamp(index, 0, count - 1);
            currentBoneIndex = index;

            Mesh mesh = boneMeshes[index];
            RockType rock = boneRocks != null && index < boneRocks.Length ? boneRocks[index] : bone.rock;
            string boneName = boneNames != null && index < boneNames.Length ? boneNames[index] : "Кость";
            float aspect = boneAspects != null && index < boneAspects.Length ? boneAspects[index] : 2.17f;
            float circ = boneCircumferences != null && index < boneCircumferences.Length ? boneCircumferences[index] : 0.138f;
            Vector3 euler = boneRotations != null && index < boneRotations.Length ? boneRotations[index] : new Vector3(0f, 24f, 3f);

            bone.ApplyBone(mesh, rock, boneName, aspect, circ, euler);
            hasLastUV = false;
            awaitingIdentification = false;
            if (atlasBook != null) atlasBook.SetHighlight(false);
            EnsureBoneLabels();

            if (hud != null)
            {
                hud.SetBone(index + 1, count, boneName);
                hud.SetAtlasReady(false);
                if (boneLabels != null && index < boneLabels.Length && boneLabels[index] >= 0 &&
                    speciesNames != null && boneLabels[index] < speciesNames.Length)
                    hud.SetLabel(speciesNames[boneLabels[index]]);
                else
                    hud.SetLabel("—");
                if (showBanner)
                    hud.ShowBanner("КОСТЬ " + (index + 1) + " / " + count + "  ·  " + boneName.ToUpperInvariant(), Gold, 1.7f);
            }

            tutorialActive = index == 0;
            tutorialStep = 0;
            hasTutorialLayer = false;

            if (hud != null)
            {
                if (tutorialActive) ShowTutorialStep(0);
                else hud.HideTutorial();
            }

            RefreshToolSelection();
        }

        private void UpdateTutorialForLayer(RockLayer layer)
        {
            if (!hasTutorialLayer || layer != lastTutorialLayer)
            {
                hasTutorialLayer = true;
                lastTutorialLayer = layer;
                if (layer == RockLayer.Contact && tutorialStep < 1)
                {
                    ShowTutorialStep(1);
                    if (hud != null) hud.FlashLayer();
                }
            }
        }

        private void ShowTutorialStep(int step)
        {
            tutorialStep = step;
            if (hud == null) return;

            switch (step)
            {
                case 0:
                    hud.SetTutorial("ШАГ 1 · ВЕРХНИЙ СЛОЙ",
                        "Кисть уже в руке. Нажмите и удерживайте ЛКМ, ведите по кости: верхний слой снимается легко.");
                    break;
                case 1:
                    hud.SetTutorial("ШАГ 2 · НИЖНИЙ СЛОЙ",
                        "Порода у кости плотнее, износ растёт быстрее. Двигайте короткими движениями и следите за шкалой «Износ».");
                    break;
                case 2:
                    hud.SetTutorial("ШАГ 3 · ОПРЕДЕЛЕНИЕ",
                        "Кость очищена. Откройте атлас на столе слева, найдите вид по форме и породе и прикрепите ярлык.");
                    break;
                case 3:
                    hud.SetTutorial("КОСТЬ РАЗРУШЕНА",
                        "На нижнем слое зубило слишком опасно. Следующая кость уже в лотке — попробуйте другой инструмент.");
                    break;
            }
        }

        private void ClearShards()
        {
            if (shardRoot == null) return;
            for (int i = shardRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(shardRoot.GetChild(i).gameObject);
            }
        }

        private void EnsureBoneLabels()
        {
            int count = BoneCount;
            if (boneLabels != null && boneLabels.Length == count) return;
            boneLabels = new int[count];
            for (int i = 0; i < count; i++) boneLabels[i] = -1;
            correctLabels = 0;
        }

        private void RecountLabels()
        {
            correctLabels = 0;
            if (boneLabels == null || boneSpecies == null) return;
            for (int i = 0; i < boneLabels.Length && i < boneSpecies.Length; i++)
            {
                if (boneLabels[i] >= 0 && boneLabels[i] == boneSpecies[i]) correctLabels++;
            }
        }

        private string FinalExtra()
        {
            return "Верно определено: " + correctLabels + " / " + BoneCount;
        }

        private void Start()
        {
            if (tools == null || tools.Length == 0) tools = ToolDefinition.CreateDefaultSet();
            if (bone != null) bone.Initialize();
            atlasBook = FindFirstObjectByType<AtlasBook>();
            EnsureBoneLabels();

            if (hud != null)
            {
                hud.Init();
                hud.RestartRequested += RestartRun;
                hud.ResetCurrentRequested += ResetAll;
                hud.AtlasRequested += TryOpenAtlas;
            }

            if (handView != null && viewCamera != null)
            {
                handView.Init(viewCamera);
                handView.SetTool(tools[toolIndex].kind);
            }

            if (hud != null)
            {
                ApplyBoneDefinition(currentBoneIndex, false);
                hud.SetTool(tools[toolIndex]);
            }

            if (bone != null)
            {
                bone.boneRenderer.enabled = true;
            }
            ConnectBoneEvents();

            BuildDust();
            lastMousePos = Input.mousePosition;
            RefreshToolSelection();

            if (cineCamera != null) baseFov = cineCamera.Lens.FieldOfView;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.None;
            Application.runInBackground = true;

            startTime = Time.time;
            if (hud != null && !tutorialActive)
            {
                hud.ShowBanner("Снимите породу с кости. Не пережмите.", Ivory, 4.5f);
            }
        }

        private void OnDisable()
        {
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (bone != null)
            {
                bone.StageChanged -= OnStageChanged;
                bone.Cleaned -= OnCleaned;
                bone.Destroyed -= OnDestroyed;
            }
        }

        private void Update()
        {
            if (bone == null || viewCamera == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            if (Input.GetKeyDown(KeyCode.R) && (hud == null || !hud.IsAtlasOpen))
            {
                ResetAll();
                return;
            }

            Vector2 mouse = Input.mousePosition;
            Vector2 mouseDelta = mouse - lastMousePos;
            lastMousePos = mouse;

            float rawSpeed = mouseDelta.magnitude / Mathf.Max(1f, Screen.height) / dt;
            rawSpeed = Mathf.Clamp(rawSpeed, 0f, 2.5f);

            bool dragging = Input.GetMouseButton(0) && !bone.IsDestroyed;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            bool overTool = false;
            ToolPickup pickup = null;
            AtlasBook atlas = null;
            if (!overUI)
            {
                Ray toolRay = viewCamera.ScreenPointToRay(mouse);
                if (Physics.Raycast(toolRay, out RaycastHit toolHit, 20f, toolMask))
                {
                    atlas = toolHit.collider.GetComponentInParent<AtlasBook>();
                    if (atlas == null)
                        pickup = toolHit.collider.GetComponentInParent<ToolPickup>();
                    overTool = pickup != null || atlas != null;
                }
            }

            if (pickup != hoveredPickup)
            {
                if (hoveredPickup != null) hoveredPickup.SetHovered(false);
                hoveredPickup = pickup;
                if (hoveredPickup != null) hoveredPickup.SetHovered(true);
            }

            bool equippedThisFrame = false;
            if (overTool && Input.GetMouseButtonDown(0))
            {
                if (atlas != null)
                    TryOpenAtlas();
                else if (hoveredPickup != null)
                    EquipTool(hoveredPickup.kind);
                equippedThisFrame = true;
            }

            bool overBone = false;
            RaycastHit hit = default;
            if (!overUI)
            {
                Ray ray = viewCamera.ScreenPointToRay(mouse);
                if (Physics.Raycast(ray, out hit, 20f, boneMask))
                {
                    var surface = hit.collider.GetComponentInParent<BoneSurface>();
                    overBone = surface == bone && hit.textureCoord.x >= 0f;
                }
            }

            if (dragging && !overBone) hasLastUV = false;

            if (dragging && overBone && !overUI && !equippedThisFrame)
            {
                smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, 1f - Mathf.Exp(-14f * dt));
                Vector2 uv = hit.textureCoord;
                ToolDefinition tool = tools[toolIndex];

                float removal = tool.removal * removalScale * (0.5f + smoothedSpeed * 0.5f) * ToolRemovalMultiplier(bone.rock, tool.kind);
                float wear = tool.wear * wearScale * (1f + smoothedSpeed * 0.5f);

                if (hasLastUV)
                {
                    PaintSegment(lastHitUV, uv, tool, removal, wear, dt);
                }
                else
                {
                    bone.Paint(uv, EffectiveRadiusUV(tool), tool.softness, removal, wear, dt, CurrentAspect);
                }

                lastHitUV = uv;
                hasLastUV = true;

                EmitDust(hit, tool, smoothedSpeed);
                if (handView != null) handView.SetStroking(true, mouseDelta, dt);
            }
            else
            {
                smoothedSpeed = Mathf.Lerp(smoothedSpeed, 0f, 1f - Mathf.Exp(-8f * dt));
                hasLastUV = false;
                if (handView != null) handView.SetStroking(false, mouseDelta, dt);
            }

            bone.Flush();

            // Lens: wheel controls the base zoom, RMB acts as a loupe.
            if (cineCamera != null)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    if (overBone)
                    {
                        // Wheel over the bone turns it, so the hidden side can be cleaned.
                        bone.transform.Rotate(Vector3.right, Mathf.Sign(scroll) * 35f, Space.Self);
                        bone.transform.Rotate(Vector3.up, Mathf.Sign(scroll) * 4f, Space.World);
                        hasLastUV = false;
                    }
                    else
                    {
                        baseFov = Mathf.Clamp(baseFov - Mathf.Sign(scroll) * 3f, 26f, 55f);
                    }
                }

                bool magnifier = Input.GetMouseButton(1);
                float targetFov = magnifier ? baseFov * 0.56f : baseFov;
                LensSettings lens = cineCamera.Lens;
                lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, 1f - Mathf.Exp(-11f * dt));
                cineCamera.Lens = lens;

                if (hud != null) hud.SetMagnifier(magnifier);
            }

            if (hud != null)
            {
                RockLayer layer = overBone ? bone.LayerAt(hit.textureCoord) : RockLayer.Loose;
                if (tutorialActive && overBone) UpdateTutorialForLayer(layer);
                hud.SetReadout(layer, bone.Progress, bone.WearNormalized, bone.Stage, bone.ValueMultiplier, bone.IsCleaned);

                float fov = cineCamera != null ? cineCamera.Lens.FieldOfView : viewCamera.fieldOfView;
                float distance = overBone ? Mathf.Max(0.15f, hit.distance) : 1.0f;
                float pxPerMeter = 1080f / (2f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));
                ToolDefinition tool = tools[toolIndex];
                float cursorRadiusUV = EffectiveRadiusUV(tool);
                float cursorCircumference = bone != null ? bone.circumference : 0.138f;
                float diameter = 2f * cursorRadiusUV * cursorCircumference * pxPerMeter;
                diameter = Mathf.Clamp(diameter, 6f, 260f);

                bool cursorVisible = true;
                hud.SetCursor(mouse, diameter, cursorVisible, overTool);
                string hoverName = null;
                if (overTool)
                {
                    if (atlas != null) hoverName = "Атлас";
                    else if (hoveredPickup != null) hoverName = hoveredPickup.displayName;
                }
                hud.SetTooltip(hoverName, mouse);
                hud.UpdateVisuals(dt);
            }

            dbgTimer += dt;
            if (debugInput && dbgTimer >= 0.6f)
            {
                dbgTimer = 0f;
                Debug.Log("[RHC7-INPUT] mouse=" + mouse + " lmb=" + Input.GetMouseButton(0) + " overUI=" + overUI +
                          " overBone=" + overBone + " overTool=" + overTool +
                          " uv=" + (overBone ? hit.textureCoord.ToString() : "-") +
                          " pickup=" + (hoveredPickup != null ? hoveredPickup.name : "-") +
                          " tool=" + toolIndex + " prog=" + bone.Progress.ToString("F4"));
            }
        }

        private void PaintSegment(Vector2 from, Vector2 to, ToolDefinition tool, float removal, float wear, float dt)
        {
            float du = to.x - from.x;
            if (du > 0.5f) du -= 1f;
            if (du < -0.5f) du += 1f;
            float dv = to.y - from.y;

            float radiusUV = EffectiveRadiusUV(tool);
            float aspect = CurrentAspect;
            float radiusV = Mathf.Max(0.001f, radiusUV / aspect);
            float distNorm = Mathf.Sqrt((du / radiusUV) * (du / radiusUV) + (dv / radiusV) * (dv / radiusV));
            int steps = Mathf.Clamp(Mathf.CeilToInt(distNorm / 0.5f), 1, 10);

            float stepDt = dt / steps;
            Vector2 target = from + new Vector2(du, dv);

            for (int i = 1; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, target, i / (float)steps);
                bone.Paint(p, radiusUV, tool.softness, removal, wear, stepDt, aspect);
            }
        }

        private void TryOpenAtlas()
        {
            if (hud == null || bone == null) return;

            if (!bone.IsCleaned || bone.IsDestroyed)
            {
                hud.ShowBanner("Сначала очистите кость", Gold, 1.7f);
                return;
            }

            if (!awaitingIdentification) return;

            hud.OpenAtlas(bone.boneName, speciesNames, speciesDescriptions, AttachLabel);
        }

        private void EquipTool(ToolKind kind)
        {
            for (int i = 0; i < tools.Length; i++)
            {
                if (tools[i].kind != kind) continue;
                toolIndex = i;
                if (hud != null) hud.SetTool(tools[i]);
                if (handView != null) handView.SetTool(kind);
                RefreshToolSelection();
                return;
            }
        }

        private void RefreshToolSelection()
        {
            var pickups = FindObjectsByType<ToolPickup>(FindObjectsSortMode.None);
            foreach (var pickup in pickups)
            {
                pickup.SetSelected(pickup.kind == tools[toolIndex].kind);
                pickup.SetTutorialGlow(tutorialActive && pickup.kind == tools[toolIndex].kind);
            }

            UpdateRockToolHint();
        }

        private void UpdateRockToolHint()
        {
            if (hud == null || tutorialActive) return;

            if (bone != null && !bone.IsCleaned && !bone.IsDestroyed &&
                bone.rock == RockType.Limestone && tools[toolIndex].kind != ToolKind.Chisel)
            {
                hud.SetTutorial("ИЗВЕСТНЯК · НУЖНО ЗУБИЛО",
                    "Кисть и скребок почти не берут эту породу. Возьмите зубило на стойке.");
            }
            else
            {
                hud.HideTutorial();
            }
        }

        private void OnStageChanged(DamageStage stage)
        {
            if (hud == null) return;

            switch (stage)
            {
                case DamageStage.Scratch:
                    hud.ShowBanner("ЦАРАПИНА", Gold, 2.2f);
                    break;
                case DamageStage.Chip:
                    hud.ShowBanner("СКОЛ  ·  −10 %", Danger, 2.6f);
                    hud.FlashDamage();
                    break;
                case DamageStage.Crack:
                    hud.ShowBanner("ТРЕЩИНА  ·  −15 %", Danger, 2.8f);
                    hud.FlashDamage();
                    break;
            }
        }

        private void OnCleaned()
        {
            awaitingIdentification = true;
            if (atlasBook != null) atlasBook.SetHighlight(true);

            if (hud != null)
            {
                hud.SetAtlasReady(true);
                if (tutorialActive && currentBoneIndex == 0)
                    ShowTutorialStep(2);
                else
                    hud.ShowBanner("КОСТЬ ОЧИЩЕНА · ОТКРОЙТЕ АТЛАС", Gold, 3.0f);
            }
        }

        private void OnDestroyed()
        {
            awaitingIdentification = false;
            if (atlasBook != null) atlasBook.SetHighlight(false);
            if (bone.boneRenderer != null) bone.boneRenderer.enabled = false;
            SpawnShards();

            if (hud != null)
            {
                hud.HideCompletion();
                if (tutorialActive && currentBoneIndex == 0)
                    ShowTutorialStep(3);
                else
                    hud.ShowBanner("КОСТЬ РАЗРУШЕНА", Danger, 2.2f);
                hud.FlashDamage();
            }

            StartCoroutine(AdvanceAfterDelay(tutorialActive ? 2.0f : 1.5f, true));
        }

        private void AttachLabel(int selected)
        {
            if (!awaitingIdentification || bone == null) return;
            if (selected < 0 || speciesNames == null || selected >= speciesNames.Length) return;

            EnsureBoneLabels();
            boneLabels[currentBoneIndex] = selected;
            RecountLabels();
            awaitingIdentification = false;
            if (atlasBook != null) atlasBook.SetHighlight(false);

            if (hud != null)
            {
                hud.SetAtlasReady(false);
                hud.SetLabel(speciesNames[selected]);
                hud.ShowBanner("ЯРЛЫК ПРИКРЕПЛЁН · " + speciesNames[selected], Gold, 1.8f);
            }

            StartCoroutine(AdvanceAfterLabel(0.9f));
        }

        private IEnumerator AdvanceAfterLabel(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            if (BoneCount > 1 && currentBoneIndex < BoneCount - 1)
            {
                ApplyBoneDefinition(currentBoneIndex + 1, true);
                yield break;
            }

            if (hud != null)
                hud.ShowCompletion(bone.Progress, bone.WearNormalized, bone.Stage, bone.ValueMultiplier, false, FinalExtra());
        }

        private IEnumerator AdvanceAfterDelay(float seconds, bool destroyed)
        {
            yield return new WaitForSeconds(seconds);

            if (BoneCount > 1 && currentBoneIndex < BoneCount - 1)
            {
                ClearShards();
                ApplyBoneDefinition(currentBoneIndex + 1, true);
                yield break;
            }

            if (hud != null)
                hud.ShowCompletion(bone.Progress, bone.WearNormalized, bone.Stage, bone.ValueMultiplier, destroyed, FinalExtra());
        }

        private void SpawnShards()
        {
            if (shardRoot == null) return;

            Vector3 center = bone.transform.position;
            for (int i = 0; i < 4; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Shard_" + i;
                shard.transform.SetParent(shardRoot, false);
                shard.transform.position = center + new Vector3(
                    Random.Range(-0.07f, 0.07f),
                    0.012f + Random.Range(0f, 0.01f),
                    Random.Range(-0.05f, 0.05f));
                shard.transform.rotation = Random.rotation;
                shard.transform.localScale = new Vector3(
                    Random.Range(0.012f, 0.026f),
                    Random.Range(0.012f, 0.022f),
                    Random.Range(0.045f, 0.10f));

                var collider = shard.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = shard.GetComponent<Renderer>();
                if (shardMaterial != null) renderer.sharedMaterial = shardMaterial;
                else if (bone.boneRenderer != null) renderer.sharedMaterial = bone.boneRenderer.sharedMaterial;
            }
        }

        public void ResetAll()
        {
            StopAllCoroutines();
            ClearShards();
            awaitingIdentification = false;
            if (atlasBook != null) atlasBook.SetHighlight(false);

            if (bone != null)
            {
                if (bone.boneRenderer != null) bone.boneRenderer.enabled = true;
                bone.ResetBone();
            }

            hasLastUV = false;
            EnsureBoneLabels();
            if (currentBoneIndex >= 0 && currentBoneIndex < boneLabels.Length) boneLabels[currentBoneIndex] = -1;
            RecountLabels();

            if (currentBoneIndex == 0)
            {
                tutorialActive = true;
                tutorialStep = 0;
                hasTutorialLayer = false;
                if (hud != null) ShowTutorialStep(0);
                RefreshToolSelection();
            }

            if (hud != null)
            {
                hud.CloseAtlas();
                hud.SetAtlasReady(false);
                hud.SetLabel("—");
                hud.HideCompletion();
                hud.ShowBanner("СБРОС", Gold, 1.2f);
            }
        }

        public void RestartRun()
        {
            StopAllCoroutines();
            ClearShards();
            awaitingIdentification = false;
            if (atlasBook != null) atlasBook.SetHighlight(false);

            EnsureBoneLabels();
            for (int i = 0; i < boneLabels.Length; i++) boneLabels[i] = -1;
            correctLabels = 0;

            if (hud != null)
            {
                hud.CloseAtlas();
                hud.SetAtlasReady(false);
                hud.HideCompletion();
            }

            currentBoneIndex = 0;
            ApplyBoneDefinition(0, false);
            hasLastUV = false;

            if (hud != null)
                hud.ShowBanner("НОВАЯ СЕРИЯ", Gold, 1.5f);
        }

        private void BuildDust()
        {
            var go = new GameObject("DustFX");
            go.transform.SetParent(transform, false);
            dust = go.AddComponent<ParticleSystem>();

            var main = dust.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.02f;
            main.startSize = 0.004f;
            main.startColor = new Color(0.55f, 0.44f, 0.30f, 0.8f);
            main.maxParticles = 1200;
            main.gravityModifier = 0.08f;

            var emission = dust.emission;
            emission.enabled = false;

            var shape = dust.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingFudge = -20f;
            renderer.material = MakeDustMaterial();

            dust.Play();
        }

        private static Material MakeDustMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
            mat.SetTexture("_BaseMap", MakeSoftCircle(64));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            return mat;
        }

        private static Texture2D MakeSoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxD = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxD;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        private void EmitDust(RaycastHit hit, ToolDefinition tool, float speed)
        {
            if (dust == null) return;

            float t = Mathf.Clamp01(speed);
            int count = Mathf.Clamp(Mathf.RoundToInt(1f + t * 4f), 1, 5);
            Color layerColor = tool.dustColor;
            RockLayer layer = bone.LayerAt(hit.textureCoord);
            if (layer == RockLayer.Packed) layerColor *= 0.85f;
            else if (layer == RockLayer.Contact) layerColor *= 0.65f;
            layerColor.a = 1f;

            for (int i = 0; i < count; i++)
            {
                var ep = new ParticleSystem.EmitParams();
                ep.position = hit.point + hit.normal * Random.Range(0.001f, 0.004f);
                ep.velocity = hit.normal * Random.Range(0.01f, 0.05f) + Random.insideUnitSphere * 0.03f;
                ep.startSize = Random.Range(0.0015f, 0.0045f);
                ep.startLifetime = Random.Range(0.45f, 0.95f);
                ep.startColor = layerColor;
                dust.Emit(ep, 1);
            }
        }
    }
}
