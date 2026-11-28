using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders small, paper-toned reference pictures for the preparator's note
/// directly from the game's bone and tool models. Runs during scene build.
/// </summary>
public static class NoteImageBuilder
{
    const string OutDir = "Assets/Textures/Notes";
    static readonly Vector3 Stage = new Vector3(2000f, 0f, 2000f);

    public static void BuildAll(GameBone[] bones, Transform toolShelf)
    {
        Directory.CreateDirectory(OutDir);
        string[] required =
        {
            "NoteTrexSkull.png", "NoteTrikeSkull.png", "NoteTrexFemur.png",
            "NoteBrush.png", "NoteScraper.png", "NoteChiselHammer.png"
        };
        bool allReady = true;
        foreach (var file in required)
            if (!File.Exists(OutDir + "/" + file)) { allReady = false; break; }
        if (allReady) return;

        var stage = new GameObject("NotePreviewStage").transform;
        stage.position = Stage;

        var camGo = new GameObject("NotePreviewCamera");
        camGo.transform.SetParent(stage, false);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.84f, 0.79f, 0.66f, 1f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 20f;
        cam.enabled = false;

        var lightGo = new GameObject("NotePreviewLight");
        lightGo.transform.SetParent(stage, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.93f, 0.80f);
        light.shadows = LightShadows.None;
        lightGo.transform.rotation = Quaternion.Euler(32f, -35f, 0f);

        var previousAmbientMode = RenderSettings.ambientMode;
        var previousAmbient = RenderSettings.ambientLight;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.43f, 0.36f);

        foreach (var bone in bones)
        {
            if (bone == null) continue;
            string file;
            Quaternion view;
            if (bone.name == "TrexSkull")
            {
                file = "NoteTrexSkull.png";
                view = Quaternion.Euler(0f, 200f, 8f);
            }
            else if (bone.name == "TrikeSkull")
            {
                file = "NoteTrikeSkull.png";
                view = Quaternion.Euler(0f, 180f, 270f);
            }
            else if (bone.name == "TrexFemur")
            {
                file = "NoteTrexFemur.png";
                view = Quaternion.Euler(0f, 70f, 4f);
            }
            else
            {
                // Additional data-driven bones do not replace the base book art.
                continue;
            }
            RenderObject(cam, bone.gameObject, view, file, 640, 480);
        }

        if (toolShelf != null)
        {
            RenderTool(cam, toolShelf, "PrepBrush", Quaternion.Euler(0f, 200f, 80f),
                28f, "NoteBrush.png");
            RenderTool(cam, toolShelf, "PrepScraper", Quaternion.Euler(0f, 200f, 10f),
                -26f, "NoteScraper.png");
            RenderTool(cam, toolShelf, "PrepChisel", Quaternion.Euler(0f, 200f, 12f),
                24f, "NoteChiselHammer.png");
        }

        Object.DestroyImmediate(stage.gameObject);
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientLight = previousAmbient;
        AssetDatabase.Refresh();
        ConfigureImports();
    }

    static void RenderTool(Camera cam, Transform shelf, string name, Quaternion view,
                           float roll, string file)
    {
        var source = shelf.Find(name);
        if (source != null)
            RenderObject(cam, source.gameObject, view, file, 512, 512, roll, 1.48f);
    }

    static void RenderObject(Camera cam, GameObject source, Quaternion view,
                             string fileName, int width, int height,
                             float roll = 0f, float margin = 1.18f)
    {
        var clone = Object.Instantiate(source);
        clone.name = "NotePreview_" + source.name;
        clone.transform.SetParent(null, false);
        clone.transform.position = Stage;
        clone.transform.rotation = view;
        clone.transform.localScale = source.transform.lossyScale;
        foreach (var c in clone.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        Bounds b = GetBounds(clone);
        if (b.size.sqrMagnitude < 0.000001f)
        {
            Object.DestroyImmediate(clone);
            return;
        }
        clone.transform.position += Stage - b.center;
        b = GetBounds(clone);

        float aspect = width / (float)height;
        float halfHeight = Mathf.Max(b.extents.y, b.extents.x / aspect);
        cam.orthographicSize = Mathf.Max(0.06f, halfHeight * margin);
        Vector3 eye = b.center + new Vector3(0.10f, 0.12f, -3f) * (cam.orthographicSize * 2f + 1f);
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.LookRotation(
            (b.center - eye).normalized, Vector3.up)
            * Quaternion.Euler(0f, 0f, roll);

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        var previous = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = previous;
        RenderTexture.active = null;
        rt.Release();
        Object.DestroyImmediate(rt);

        string path = OutDir + "/" + fileName;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(clone);
    }

    static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static void ConfigureImports()
    {
        foreach (var file in new[]
        {
            "NoteTrexSkull.png", "NoteTrikeSkull.png", "NoteTrexFemur.png",
            "NoteBrush.png", "NoteScraper.png", "NoteChiselHammer.png"
        })
        {
            string path = OutDir + "/" + file;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
