using System;
using UnityEngine;

namespace RHC7
{
    public enum DamageStage
    {
        None = 0,
        Scratch = 1,
        Chip = 2,
        Crack = 3,
        Destroyed = 4
    }

    public enum RockType
    {
        Sandstone = 0,
        Clay = 1,
        Limestone = 2,
        Pyrite = 3
    }

    public enum RockLayer
    {
        Loose = 0,
        Packed = 1,
        Contact = 2
    }

    /// <summary>
    /// Runtime state of a single bone: two masks packed into one texture
    /// (R = dirt remaining, G = wear), plus damage accounting.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoneSurface : MonoBehaviour
    {
        [Header("Mask")]
        [SerializeField] private int maskSize = 512;

        [Header("Rock")]
        public RockType rock = RockType.Sandstone;

        [Header("Bone info")]
        public string boneName = "Кость";
        public float uvAspect = 2.17f;
        public float circumference = 0.138f;

        [Header("Rendering")]
        public Renderer boneRenderer;

        public event Action<DamageStage> StageChanged;
        public event Action Cleaned;
        public event Action Destroyed;

        public const float ScratchThreshold = 0.25f;
        public const float ChipThreshold = 0.50f;
        public const float CrackThreshold = 0.75f;
        public const float DestroyThreshold = 1.00f;

        private Texture2D maskTexture;
        private Color32[] mask;
        private float[] wearValues;
        private Material materialInstance;

        private float initialDirt;
        private float remainingDirt;
        private float maxWear;
        private Vector2 maxWearUV;
        private DamageStage stage = DamageStage.None;
        private bool cleaned;
        private bool destroyed;
        private bool dirty;

        public float Progress
        {
            get
            {
                if (cleaned) return 1f;
                if (initialDirt <= 0f) return 0f;
                return Mathf.Clamp01(1f - remainingDirt / initialDirt);
            }
        }

        public float WearNormalized => Mathf.Clamp01(maxWear);
        public DamageStage Stage => stage;
        public bool IsCleaned => cleaned;
        public bool IsDestroyed => destroyed;
        public Texture2D MaskTexture => maskTexture;
        public Material BoneMaterial => materialInstance;

        public float ValueMultiplier
        {
            get
            {
                switch (stage)
                {
                    case DamageStage.Chip: return 0.90f;
                    case DamageStage.Crack: return 0.85f;
                    case DamageStage.Destroyed: return 0.60f;
                    default: return 1f;
                }
            }
        }

        public float RockRemovalMultiplier
        {
            get
            {
                switch (rock)
                {
                    case RockType.Sandstone: return 1.3f;
                    case RockType.Clay: return 1.1f;
                    case RockType.Limestone: return 0.6f;
                    case RockType.Pyrite: return 1.0f;
                    default: return 1f;
                }
            }
        }

        public float RockWearMultiplier
        {
            get
            {
                switch (rock)
                {
                    case RockType.Sandstone: return 1.0f;
                    case RockType.Clay: return 1.2f;
                    case RockType.Limestone: return 1.3f;
                    case RockType.Pyrite: return 2.0f;
                    default: return 1f;
                }
            }
        }

        private void Awake()
        {
            if (boneRenderer == null) boneRenderer = GetComponent<Renderer>();
            Initialize();
        }

        public void Initialize()
        {
            if (maskTexture != null) return;

            maskSize = Mathf.Clamp(maskSize, 128, 1024);
            maskTexture = new Texture2D(maskSize, maskSize, TextureFormat.RGBA32, false, true);
            maskTexture.name = "BoneMask_" + name;
            maskTexture.wrapMode = TextureWrapMode.Clamp;
            maskTexture.filterMode = FilterMode.Bilinear;
            maskTexture.hideFlags = HideFlags.DontSave;

            mask = new Color32[maskSize * maskSize];
            wearValues = new float[mask.Length];
            var pixel = new Color32(255, 0, 0, 255);
            for (int i = 0; i < mask.Length; i++) mask[i] = pixel;
            maskTexture.SetPixels32(mask);
            maskTexture.Apply(false);

            initialDirt = mask.Length;
            remainingDirt = initialDirt;

            if (boneRenderer != null)
            {
                var source = boneRenderer.sharedMaterial;
                materialInstance = source != null ? new Material(source) : null;
                if (materialInstance != null)
                {
                    materialInstance.hideFlags = HideFlags.DontSave;
                    materialInstance.SetTexture("_DirtMap", maskTexture);
                    boneRenderer.material = materialInstance;
                    ApplyRockColors();
                }
            }
        }

        public void ApplyRockColors()
        {
            if (materialInstance == null) return;

            Color loose;
            Color contact;
            switch (rock)
            {
                case RockType.Clay:
                    loose = new Color(0.42f, 0.22f, 0.13f);
                    contact = new Color(0.15f, 0.07f, 0.04f);
                    break;
                case RockType.Limestone:
                    loose = new Color(0.44f, 0.42f, 0.36f);
                    contact = new Color(0.17f, 0.16f, 0.14f);
                    break;
                case RockType.Pyrite:
                    loose = new Color(0.30f, 0.22f, 0.10f);
                    contact = new Color(0.10f, 0.08f, 0.05f);
                    break;
                default:
                    loose = new Color(0.34f, 0.25f, 0.15f);
                    contact = new Color(0.10f, 0.075f, 0.05f);
                    break;
            }

            materialInstance.SetColor("_LooseDirtColor", loose);
            materialInstance.SetColor("_ContactDirtColor", contact);
        }

        public void ApplyBone(Mesh mesh, RockType newRock, string newName, float newAspect, float newCircumference, Vector3 euler)
        {
            rock = newRock;
            boneName = newName;
            uvAspect = Mathf.Max(0.1f, newAspect);
            circumference = Mathf.Max(0.02f, newCircumference);

            var filter = GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = mesh;

            var collider = GetComponent<MeshCollider>();
            if (collider != null)
            {
                collider.enabled = false;
                collider.sharedMesh = mesh;
                collider.enabled = true;
            }

            transform.rotation = Quaternion.Euler(euler);
            if (mesh != null)
            {
                Vector3 p = transform.position;
                p.y = 0.916f + mesh.bounds.extents.y + 0.002f;
                transform.position = p;
            }

            if (boneRenderer != null) boneRenderer.enabled = true;
            ResetBone();
            ApplyRockColors();
        }

        public float LocalDirt01(Vector2 uv)
        {
            if (mask == null) return 1f;
            int x = WrapX(Mathf.RoundToInt(uv.x * (maskSize - 1)));
            int y = ClampY(Mathf.RoundToInt(uv.y * (maskSize - 1)));
            return mask[y * maskSize + x].r / 255f;
        }

        public RockLayer LayerAt(Vector2 uv)
        {
            float dirt = LocalDirt01(uv);
            if (dirt > 0.50f) return RockLayer.Loose;
            return RockLayer.Contact;
        }

        /// <summary>
        /// Paints one circular tool stamp. removalPerSecond and wearPerSecond are
        /// already scaled by the tool, speed and global tuning.
        /// </summary>
        public void Paint(Vector2 uv, float radiusU, float softness, float removalPerSecond, float wearPerSecond, float dt, float uvAspect)
        {
            if (mask == null || destroyed || cleaned || dt <= 0f) return;

            float aspect = Mathf.Max(0.05f, uvAspect);
            float radiusV = Mathf.Max(0.001f, radiusU / aspect);
            float inv = 1f / (maskSize - 1);

            int cx = Mathf.RoundToInt(uv.x * (maskSize - 1));
            int cy = Mathf.RoundToInt(uv.y * (maskSize - 1));
            int rx = Mathf.CeilToInt(radiusU * maskSize) + 1;
            int ry = Mathf.CeilToInt(radiusV * maskSize) + 1;

            int yMin = Mathf.Max(0, cy - ry);
            int yMax = Mathf.Min(maskSize - 1, cy + ry);

            float rockRemoval = RockRemovalMultiplier;
            float rockWear = RockWearMultiplier;
            float soft = Mathf.Clamp(softness, 0.05f, 0.95f);
            float edgeSpan = Mathf.Max(0.001f, 1f - soft);

            float newMaxWear = maxWear;
            Vector2 newMaxUV = maxWearUV;
            bool touched = false;
            float addedWearForStats = 0f;

            for (int y = yMin; y <= yMax; y++)
            {
                float dv = (y * inv - uv.y) / radiusV;
                int row = y * maskSize;

                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    int wx = WrapX(x);
                    float du = (wx * inv - uv.x);
                    if (du > 0.5f) du -= 1f;
                    if (du < -0.5f) du += 1f;
                    du /= radiusU;

                    float dist = Mathf.Sqrt(du * du + dv * dv);
                    if (dist >= 1f) continue;

                    float edge = Mathf.Clamp01((1f - dist) / edgeSpan);
                    float falloff = edge * edge * (3f - 2f * edge);
                    if (falloff <= 0.0001f) continue;

                    int idx = row + wx;
                    Color32 c = mask[idx];
                    float d = c.r / 255f;
                    float w = wearValues[idx];

                    float layerRemove;
                    float layerWear;
                    if (d > 0.50f) { layerRemove = 1.0f; layerWear = 0.5f; }
                    else { layerRemove = 0.60f; layerWear = 2.5f; }

                    float removed = removalPerSecond * layerRemove * rockRemoval * falloff * dt;
                    float addedWear = wearPerSecond * layerWear * rockWear * falloff * dt;

                    float nd = d - removed;
                    if (nd < 0.004f) nd = 0f;
                    float nw = w + addedWear;
                    if (nw > 1f) nw = 1f;

                    if (nd != d)
                    {
                        remainingDirt += nd - d;
                        c.r = (byte)Mathf.RoundToInt(nd * 255f);
                    }
                    if (nw != w)
                    {
                        addedWearForStats += nw - w;
                        wearValues[idx] = nw;
                        c.g = (byte)Mathf.Max((int)c.g, Mathf.RoundToInt(nw * 255f));
                    }

                    if (nw > newMaxWear)
                    {
                        newMaxWear = nw;
                        newMaxUV = new Vector2(wx * inv, y * inv);
                    }

                    mask[idx] = c;
                    touched = true;
                }
            }

            if (!touched) return;

            remainingDirt = Mathf.Max(0f, remainingDirt);
            maxWear = newMaxWear;
            maxWearUV = newMaxUV;
            dirty = true;

            if (addedWearForStats > 0f) EvaluateDamage();

            if (!cleaned && !destroyed && remainingDirt <= initialDirt * 0.18f)
            {
                cleaned = true;
                remainingDirt = 0f;
                // The UV seam and the back side are hard to reach; once the
                // player has cleaned most of the crust, polish off the rest so
                // the finished bone reads as clean.
                for (int i = 0; i < mask.Length; i++)
                {
                    Color32 c = mask[i];
                    c.r = 0;
                    mask[i] = c;
                }
                maskTexture.SetPixels32(mask);
                maskTexture.Apply(false);
                Cleaned?.Invoke();
            }
        }

        private void EvaluateDamage()
        {
            DamageStage next = stage;
            if (maxWear >= DestroyThreshold) next = DamageStage.Destroyed;
            else if (maxWear >= CrackThreshold) next = DamageStage.Crack;
            else if (maxWear >= ChipThreshold) next = DamageStage.Chip;
            else if (maxWear >= ScratchThreshold) next = DamageStage.Scratch;

            if (next <= stage) return;

            stage = next;

            if (stage == DamageStage.Chip)
            {
                StampWear(maxWearUV, 0.012f, 0.92f);
            }
            else if (stage == DamageStage.Crack)
            {
                DrawCrack(maxWearUV);
            }

            StageChanged?.Invoke(stage);

            if (stage == DamageStage.Destroyed)
            {
                destroyed = true;
                Destroyed?.Invoke();
            }
        }

        private void DrawCrack(Vector2 start)
        {
            float u = start.x;
            float v = start.y;
            float dir = UnityEngine.Random.value > 0.5f ? 1f : -1f;

            for (int i = 0; i < 30; i++)
            {
                u += (UnityEngine.Random.value - 0.5f) * 0.020f + dir * 0.002f;
                v += 0.014f + UnityEngine.Random.value * 0.011f;
                if (v >= 0.97f || u <= 0.02f || u >= 0.98f) break;
                float width = i < 5 ? 0.0050f : 0.0032f;
                StampWear(new Vector2(u, v), width, 0.98f);
                if (i % 3 == 0)
                {
                    StampWear(new Vector2(u + 0.006f * dir, v), width * 0.7f, 0.94f);
                }
            }
            dirty = true;
        }

        private void StampWear(Vector2 uv, float radius, float value)
        {
            float radiusV = radius;
            int cx = Mathf.RoundToInt(uv.x * (maskSize - 1));
            int cy = Mathf.RoundToInt(uv.y * (maskSize - 1));
            int rx = Mathf.CeilToInt(radius * maskSize) + 1;
            int ry = Mathf.CeilToInt(radiusV * maskSize) + 1;
            float inv = 1f / (maskSize - 1);
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);

            for (int y = Mathf.Max(0, cy - ry); y <= Mathf.Min(maskSize - 1, cy + ry); y++)
            {
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    int wx = WrapX(x);
                    float du = wx * inv - uv.x;
                    if (du > 0.5f) du -= 1f;
                    if (du < -0.5f) du += 1f;
                    float dv = y * inv - uv.y;
                    if (du * du + dv * dv > radius * radius) continue;

                    int idx = y * maskSize + wx;
                    Color32 c = mask[idx];
                    if (c.g < b) { c.g = b; mask[idx] = c; }
                }
            }
            dirty = true;
        }

        public void Flush()
        {
            if (!dirty || maskTexture == null || mask == null) return;
            maskTexture.SetPixels32(mask);
            maskTexture.Apply(false);
            dirty = false;
        }

        public void ResetBone()
        {
            if (mask == null) return;
            var pixel = new Color32(255, 0, 0, 255);
            for (int i = 0; i < mask.Length; i++) mask[i] = pixel;
            for (int i = 0; i < wearValues.Length; i++) wearValues[i] = 0f;
            remainingDirt = initialDirt;
            maxWear = 0f;
            maxWearUV = Vector2.zero;
            stage = DamageStage.None;
            cleaned = false;
            destroyed = false;
            dirty = true;
            Flush();
            StageChanged?.Invoke(DamageStage.None);
        }

        private int WrapX(int x)
        {
            int n = maskSize;
            x %= n;
            if (x < 0) x += n;
            return x;
        }

        private int ClampY(int y)
        {
            if (y < 0) return 0;
            if (y > maskSize - 1) return maskSize - 1;
            return y;
        }

        private void OnDestroy()
        {
            if (materialInstance != null) Destroy(materialInstance);
            if (maskTexture != null) Destroy(maskTexture);
        }
    }
}
