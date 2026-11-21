using UnityEngine;

namespace RHC7
{
    /// <summary>
    /// Diegetic atlas on the work table. The controller opens the
    /// identification UI when this book is clicked after cleaning.
    /// </summary>
    public class AtlasBook : MonoBehaviour
    {
        private GameObject highlight;

        public void SetHighlight(bool value)
        {
            if (value)
            {
                if (highlight == null) CreateHighlight();
                highlight.SetActive(true);
            }
            else if (highlight != null)
            {
                highlight.SetActive(false);
            }
        }

        private void CreateHighlight()
        {
            highlight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            highlight.name = "AtlasHighlight";
            highlight.transform.SetParent(transform, false);
            highlight.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            highlight.transform.localScale = Vector3.one * 0.022f;

            var collider = highlight.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", new Color(1f, 0.78f, 0.28f, 1f));
            var renderer = highlight.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (highlight == null || !highlight.activeSelf) return;

            float pulse = 1f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            highlight.transform.localScale = Vector3.one * 0.022f * pulse;
            highlight.transform.localPosition = new Vector3(0f, 0.16f + 0.01f * Mathf.Sin(Time.unscaledTime * 3f), 0f);
        }
    }
}
