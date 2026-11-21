using System.Collections.Generic;
using UnityEngine;

namespace RHC7
{
    /// <summary>
    /// Builds a simple first-person hand + tool presentation at runtime and
    /// lets it sway with the mouse while cleaning.
    /// </summary>
    public class HandToolView : MonoBehaviour
    {
        private class ToolRig
        {
            public Transform root;
            public Vector3 basePos;
            public Quaternion baseRot;
        }

        private readonly Dictionary<ToolKind, ToolRig> rigs = new Dictionary<ToolKind, ToolRig>();
        private Dictionary<ToolKind, Material> metalMats;
        private ToolKind current = ToolKind.Brush;
        private ToolRig active;
        private float raise = 0f;
        private Vector3 swayOffset;
        private float strokePhase;

        private Material woodMat;
        private Material metalMat;
        private Material bristleMat;
        private Material skinMat;
        private Material sleeveMat;

        private static readonly Vector3 BaseLocalPos = new Vector3(0.35f, -0.145f, 0.52f);

        public void Init(Camera camera)
        {
            BuildMaterials();
            BuildRig(ToolKind.Brush, camera.transform);
            BuildRig(ToolKind.Scraper, camera.transform);
            BuildRig(ToolKind.Chisel, camera.transform);
            SetTool(ToolKind.Brush);
        }

        public void SetTool(ToolKind kind)
        {
            current = kind;
            foreach (var pair in rigs)
            {
                pair.Value.root.gameObject.SetActive(pair.Key == kind);
            }

            if (rigs.TryGetValue(kind, out var rig))
            {
                active = rig;
                raise = 0f;
                active.root.localPosition = rig.basePos + Vector3.down * 0.12f;
                active.root.localRotation = rig.baseRot;
            }
        }

        public void SetStroking(bool stroking, Vector2 mouseDelta, float dt)
        {
            if (dt <= 0f) return;

            Vector2 clamped = Vector2.ClampMagnitude(mouseDelta, 40f);
            Vector3 targetSway = new Vector3(-clamped.x * 0.00018f, -clamped.y * 0.00018f, 0f);
            swayOffset = Vector3.Lerp(swayOffset, targetSway, 1f - Mathf.Exp(-14f * dt));

            if (stroking) strokePhase += dt * 15f;
        }

        private void LateUpdate()
        {
            if (active == null) return;

            float dt = Time.unscaledDeltaTime;
            raise = Mathf.MoveTowards(raise, 1f, dt * 5.5f);

            float breathing = Mathf.Sin(Time.unscaledTime * 1.7f) * 0.0015f;
            float strokeBob = Mathf.Sin(strokePhase) * 0.0045f;
            float strokeRoll = Mathf.Sin(strokePhase * 0.5f) * 1.6f;

            Vector3 pos = active.basePos + Vector3.up * (breathing + strokeBob) + swayOffset;
            pos.y -= (1f - raise) * 0.12f;

            Quaternion rot = active.baseRot * Quaternion.Euler(0f, 0f, strokeRoll + swayOffset.x * 55f);
            active.root.localPosition = Vector3.Lerp(active.root.localPosition, pos, 1f - Mathf.Exp(-18f * dt));
            active.root.localRotation = Quaternion.Slerp(active.root.localRotation, rot, 1f - Mathf.Exp(-18f * dt));
        }

        private void BuildMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            woodMat = MakeMaterial(lit, new Color(0.35f, 0.23f, 0.14f), 0f, 0.35f);
            metalMat = MakeMaterial(lit, new Color(0.55f, 0.56f, 0.57f), 0.85f, 0.65f);
            bristleMat = MakeMaterial(lit, new Color(0.62f, 0.50f, 0.33f), 0f, 0.15f);
            skinMat = MakeMaterial(lit, new Color(0.50f, 0.35f, 0.26f), 0f, 0.25f);
            sleeveMat = MakeMaterial(lit, new Color(0.09f, 0.10f, 0.09f), 0f, 0.2f);
        }

        private static Material MakeMaterial(Shader shader, Color color, float metallic, float smoothness)
        {
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private void BuildRig(ToolKind kind, Transform cameraTransform)
        {
            var root = new GameObject("HeldTool_" + kind);
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = BaseLocalPos;
            root.transform.localRotation = Quaternion.Euler(-8f, 0f, 34f);
            root.transform.localScale = Vector3.one * 0.84f;

            BuildHand(root.transform);

            switch (kind)
            {
                case ToolKind.Brush: BuildBrush(root.transform); break;
                case ToolKind.Scraper: BuildScraper(root.transform); break;
                default: BuildChisel(root.transform); break;
            }

            rigs[kind] = new ToolRig
            {
                root = root.transform,
                basePos = BaseLocalPos,
                baseRot = Quaternion.Euler(-8f, 0f, 34f)
            };
        }

        private void BuildHand(Transform parent)
        {
            var fist = Primitive(PrimitiveType.Sphere, "Fist", parent, skinMat);
            fist.transform.localPosition = new Vector3(-0.004f, -0.012f, 0.005f);
            fist.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            fist.transform.localScale = new Vector3(0.052f, 0.042f, 0.060f);

            var thumb = Primitive(PrimitiveType.Capsule, "Thumb", parent, skinMat);
            thumb.transform.localPosition = new Vector3(-0.030f, 0.004f, 0.018f);
            thumb.transform.localRotation = Quaternion.Euler(18f, 0f, 38f);
            thumb.transform.localScale = new Vector3(0.014f, 0.026f, 0.014f);

            var forearm = Primitive(PrimitiveType.Cylinder, "Forearm", parent, skinMat);
            forearm.transform.localPosition = new Vector3(0.012f, -0.145f, 0.005f);
            forearm.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            forearm.transform.localScale = new Vector3(0.040f, 0.145f, 0.040f);

            var sleeve = Primitive(PrimitiveType.Cylinder, "Sleeve", parent, sleeveMat);
            sleeve.transform.localPosition = new Vector3(0.030f, -0.305f, 0.005f);
            sleeve.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            sleeve.transform.localScale = new Vector3(0.047f, 0.095f, 0.047f);

            var fill = new GameObject("HandFill");
            fill.transform.SetParent(parent, false);
            fill.transform.localPosition = new Vector3(0f, -0.04f, 0.30f);
            var light = fill.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.66f);
            light.intensity = 0.35f;
            light.range = 1.1f;
            light.shadows = LightShadows.None;
        }

        private void BuildBrush(Transform parent)
        {
            var handle = Primitive(PrimitiveType.Cylinder, "Handle", parent, woodMat);
            handle.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            handle.transform.localRotation = Quaternion.identity;
            handle.transform.localScale = new Vector3(0.013f, 0.085f, 0.013f);

            var ferrule = Primitive(PrimitiveType.Cylinder, "Ferrule", parent, metalMat);
            ferrule.transform.localPosition = new Vector3(0f, 0.104f, 0f);
            ferrule.transform.localScale = new Vector3(0.015f, 0.010f, 0.015f);

            var bristles = Primitive(PrimitiveType.Cube, "Bristles", parent, bristleMat);
            bristles.transform.localPosition = new Vector3(0f, 0.132f, 0f);
            bristles.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            bristles.transform.localScale = new Vector3(0.060f, 0.050f, 0.024f);
        }

        private void BuildScraper(Transform parent)
        {
            var handle = Primitive(PrimitiveType.Cylinder, "Handle", parent, woodMat);
            handle.transform.localPosition = new Vector3(0f, 0.020f, 0f);
            handle.transform.localScale = new Vector3(0.014f, 0.090f, 0.014f);

            var neck = Primitive(PrimitiveType.Cube, "Neck", parent, metalMat);
            neck.transform.localPosition = new Vector3(0f, 0.112f, 0f);
            neck.transform.localScale = new Vector3(0.012f, 0.030f, 0.008f);

            var blade = Primitive(PrimitiveType.Cube, "Blade", parent, metalMat);
            blade.transform.localPosition = new Vector3(0f, 0.142f, 0.010f);
            blade.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
            blade.transform.localScale = new Vector3(0.045f, 0.004f, 0.032f);
        }

        private void BuildChisel(Transform parent)
        {
            var handle = Primitive(PrimitiveType.Cylinder, "Handle", parent, woodMat);
            handle.transform.localPosition = new Vector3(0f, -0.018f, 0f);
            handle.transform.localScale = new Vector3(0.017f, 0.070f, 0.017f);

            var shaft = Primitive(PrimitiveType.Cylinder, "Shaft", parent, metalMat);
            shaft.transform.localPosition = new Vector3(0f, 0.075f, 0f);
            shaft.transform.localScale = new Vector3(0.0075f, 0.062f, 0.0075f);

            var tip = Primitive(PrimitiveType.Cube, "Tip", parent, metalMat);
            tip.transform.localPosition = new Vector3(0f, 0.140f, 0.004f);
            tip.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            tip.transform.localScale = new Vector3(0.045f, 0.004f, 0.026f);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }
    }
}
