using UnityEngine;

namespace RHC7
{
    public class ToolPickup : MonoBehaviour
    {
        public ToolKind kind = ToolKind.Brush;
        public string displayName = "";
        public float hoverLift = 0.014f;

        private Vector3 baseLocalPos;
        private Quaternion baseLocalRot;
        private bool hovered;
        private bool selected;
        private GameObject tutorialGlow;

        private void Awake()
        {
            baseLocalPos = transform.localPosition;
            baseLocalRot = transform.localRotation;
        }

        public void SetHovered(bool value)
        {
            hovered = value;
        }

        public void SetSelected(bool value)
        {
            selected = value;
        }

        public void SetTutorialGlow(bool value)
        {
            if (value)
            {
                if (tutorialGlow == null) CreateTutorialGlow();
                tutorialGlow.SetActive(true);
            }
            else if (tutorialGlow != null)
            {
                tutorialGlow.SetActive(false);
            }
        }

        private void CreateTutorialGlow()
        {
            tutorialGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tutorialGlow.name = "TutorialGlow";
            tutorialGlow.transform.SetParent(transform, false);
            tutorialGlow.transform.localPosition = new Vector3(0f, 0.23f, 0f);
            tutorialGlow.transform.localScale = Vector3.one * 0.018f;

            var collider = tutorialGlow.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", new Color(1f, 0.78f, 0.28f, 1f));
            var renderer = tutorialGlow.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            bool lifted = hovered || selected;
            Vector3 targetPos = baseLocalPos + (lifted ? Vector3.up * hoverLift : Vector3.zero);
            Quaternion targetRot = baseLocalRot * (lifted ? Quaternion.Euler(-4f, 3f, 0f) : Quaternion.identity);
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, k);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, k);

            if (tutorialGlow != null && tutorialGlow.activeSelf)
            {
                float pulse = 1f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
                tutorialGlow.transform.localScale = Vector3.one * 0.018f * pulse;
                tutorialGlow.transform.localPosition = new Vector3(0f, 0.23f + 0.008f * Mathf.Sin(Time.unscaledTime * 3f), 0f);
            }
        }
    }
}
