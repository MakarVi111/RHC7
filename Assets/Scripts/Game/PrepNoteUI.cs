using UnityEngine;

/// <summary>
/// Replaces the rectangular HUD with a single aged paper note. It shows the
/// current specimen's species, which tool removes each dirt layer, and a small
/// handwritten progress line.
/// </summary>
public class PrepNoteUI : MonoBehaviour
{
    public GameFlow flow;
    public BoneCleaningV2 cleaning;
    public Texture2D brushIcon;
    public Texture2D scraperIcon;
    public Texture2D chiselIcon;

    Texture2D paper;
    GUIStyle titleStyle;
    GUIStyle subtitleStyle;
    GUIStyle speciesStyle;
    GUIStyle partStyle;
    GUIStyle bodyStyle;
    GUIStyle smallStyle;
    GUIStyle noteStyle;
    bool stylesReady;

    readonly Color ink = new Color(0.20f, 0.13f, 0.075f);
    readonly Color fadedInk = new Color(0.36f, 0.27f, 0.17f);
    readonly Color redPencil = new Color(0.55f, 0.16f, 0.10f, 0.9f);
    readonly Color paperTint = new Color(0.88f, 0.82f, 0.68f);

    void Awake()
    {
        if (flow == null) flow = Object.FindFirstObjectByType<GameFlow>();
        if (cleaning == null) cleaning = Object.FindFirstObjectByType<BoneCleaningV2>();
        BuildPaper();
        // GUI.skin is only legal inside OnGUI; styles are built there lazily.
    }

    void OnGUI()
    {
        if (flow != null && (flow.Complete || flow.IsIdentifying)) return;
        if (!stylesReady) BuildStyles();
        if (paper == null) BuildPaper();

        float s = Mathf.Clamp(Screen.height / 1080f, 0.82f, 1.15f);
        var note = new Rect(18f * s, 18f * s, 392f * s, 430f * s);

        // Paper with a soft shadow and two pieces of old tape.
        GUI.color = new Color(0f, 0f, 0f, 0.25f);
        GUI.DrawTexture(new Rect(note.x + 5, note.y + 7, note.width, note.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(note, paper, ScaleMode.StretchToFill);
        GUI.color = new Color(0.78f, 0.70f, 0.50f, 0.55f);
        GUI.DrawTexture(new Rect(note.x - 11, note.y + 3, 66, 18), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(note.x + note.width - 55, note.y + 3, 66, 18),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        float x = note.x + 22f;
        float w = note.width - 44f;
        float y = note.y + 20f;

        GUI.Label(new Rect(x, y, w, 27), "ПАМЯТКА ПРЕПАРАТОРА", titleStyle);
        y += 25;
        GUI.Label(new Rect(x, y, w, 18),
            "РХЦ-7   ·   ОПРЕДЕЛЕНО: " + (flow != null ? flow.SolvedCount : 0) + " / " +
            (flow != null ? flow.BoneCount : 3) + "   ·   1934",
            subtitleStyle);
        y += 21;
        DrawRoughLine(x, y, w, fadedInk);
        y += 10;

        GameBone bone = flow != null ? flow.CurrentBone : null;
        GUI.Label(new Rect(x, y, w, 22),
            bone == null ? "ВОЗЬМИТЕ КОСТЬ ИЗ ЯЩИКА"
                         : "ОБРАЗЕЦ: " + bone.partName.ToUpperInvariant(),
            noteStyle);
        y += 22;
        GUI.Label(new Rect(x, y, w, 18),
            "Порода определяется по книге после чистки.", smallStyle);
        y += 27;

        DrawToolGuide(x, y, w);
        y += 188f;

        if (cleaning != null && cleaning.IsReady && flow != null && flow.CurrentBone != null)
            DrawProgress(x, y, w);
        y += 28f;

        if (flow != null && flow.IsBookReady)
        {
            GUI.Label(new Rect(x, y, w, 24),
                "КОСТЬ ОЧИЩЕНА — ОТКРОЙТЕ КНИГУ", noteStyle);
            y += 26f;
        }

        GUI.Label(new Rect(x, y, w, 36),
            "ЛКМ — чистить   ·   колесо — зум\nПКМ — вращать   ·   Q/E — крен",
            smallStyle);
    }

    void DrawToolGuide(float x, float y, float w)
    {
        GUI.Label(new Rect(x, y, w, 20), "СНЯТИЕ СЛОЁВ:", noteStyle);
        y += 23;
        int active = cleaning != null ? ToolRowIndex(cleaning.tool) : -1;
        DrawToolRow(x, y, w, 0, brushIcon, "ПЫЛЬ", "КИСТЬ", active);
        DrawToolRow(x, y + 52, w, 1, scraperIcon, "ГЛИНА", "СКРЕБОК", active);
        DrawToolRow(x, y + 104, w, 2, chiselIcon, "МАТРИЦА", "ЗУБИЛО + МОЛОТОК", active);
    }

    void DrawToolRow(float x, float y, float w, int index, Texture2D icon,
                     string layer, string tool, int active)
    {
        bool on = index == active;
        if (on)
        {
            GUI.color = new Color(0.48f, 0.14f, 0.08f, 0.10f);
            GUI.DrawTexture(new Rect(x - 5, y - 3, w + 10, 50), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        var ir = new Rect(x, y - 2, 52, 46);
        if (icon != null)
        {
            GUI.color = on ? Color.white : new Color(1f, 1f, 1f, 0.86f);
            GUI.DrawTextureWithTexCoords(ir, icon,
                new Rect(0.13f, 0.10f, 0.74f, 0.80f));
            GUI.color = Color.white;
        }
        GUI.Label(new Rect(x + 60, y - 1, w - 60, 19),
            (index + 1) + " · " + layer, noteStyle);
        GUI.Label(new Rect(x + 60, y + 18, w - 60, 19), tool, bodyStyle);
        if (on) DrawRoughLine(x + 60, y + 40, 108f, redPencil);
    }

    static int ToolRowIndex(BoneCleaningV2.Tool tool)
    {
        if (tool == BoneCleaningV2.Tool.Dust) return 0;
        if (tool == BoneCleaningV2.Tool.Clay) return 1;
        return 2;
    }

    void DrawProgress(float x, float y, float w)
    {
        float p = Mathf.Clamp01(cleaning.TotalCleanFraction);
        GUI.Label(new Rect(x, y, w, 18),
            "ОЧИЩЕНО: " + (p * 100f).ToString("F0") + "%", noteStyle);
        var bar = new Rect(x, y + 20, w, 6);
        GUI.color = new Color(ink.r, ink.g, ink.b, 0.22f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = redPencil;
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * p, bar.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawArt(Rect rect, Texture2D art)
    {
        if (art == null) return;
        GUI.color = new Color(0.30f, 0.23f, 0.14f, 0.35f);
        GUI.DrawTexture(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4),
            Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(rect, art, ScaleMode.ScaleToFit);
        GUI.color = new Color(0.24f, 0.16f, 0.09f, 0.35f);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax, rect.width, 1), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawRoughLine(float x, float y, float w, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, w, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + 14, y + 2, w - 27, 1), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void BuildStyles()
    {
        Font font = null;
        try
        {
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Georgia", "Cambria", "Times New Roman", "Arial" }, 20);
        }
        catch { }

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.font = font;
        titleStyle.fontSize = 20;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = ink;

        subtitleStyle = new GUIStyle(GUI.skin.label);
        subtitleStyle.font = font;
        subtitleStyle.fontSize = 12;
        subtitleStyle.normal.textColor = fadedInk;

        speciesStyle = new GUIStyle(GUI.skin.label);
        speciesStyle.font = font;
        speciesStyle.fontSize = 22;
        speciesStyle.fontStyle = FontStyle.Bold;
        speciesStyle.normal.textColor = ink;

        partStyle = new GUIStyle(GUI.skin.label);
        partStyle.font = font;
        partStyle.fontSize = 13;
        partStyle.fontStyle = FontStyle.Italic;
        partStyle.normal.textColor = fadedInk;

        bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.font = font;
        bodyStyle.fontSize = 13;
        bodyStyle.normal.textColor = ink;

        smallStyle = new GUIStyle(GUI.skin.label);
        smallStyle.font = font;
        smallStyle.fontSize = 11;
        smallStyle.normal.textColor = fadedInk;
        smallStyle.wordWrap = true;

        noteStyle = new GUIStyle(GUI.skin.label);
        noteStyle.font = font;
        noteStyle.fontSize = 14;
        noteStyle.fontStyle = FontStyle.Bold;
        noteStyle.normal.textColor = ink;

        stylesReady = true;
    }

    void BuildPaper()
    {
        const int w = 512;
        const int h = 720;
        paper = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        var rng = new System.Random(1934);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float fibre = Mathf.PerlinNoise(x * 0.035f, y * 0.012f);
                float grain = Hash(x, y, 17);
                float shade = 0.91f + fibre * 0.06f + (grain - 0.5f) * 0.045f;
                float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                if (edge < 16f) shade *= Mathf.Lerp(0.82f, 1f, edge / 16f);
                pixels[y * w + x] = new Color(
                    paperTint.r * shade, paperTint.g * shade, paperTint.b * shade, 1f);
            }
        }

        // Sparse age spots and a faint fold line.
        for (int i = 0; i < 16; i++)
        {
            int cx = rng.Next(w);
            int cy = rng.Next(h);
            int radius = 2 + rng.Next(8);
            float strength = 0.04f + (float)rng.NextDouble() * 0.06f;
            for (int yy = -radius; yy <= radius; yy++)
            {
                for (int xx = -radius; xx <= radius; xx++)
                {
                    if (xx * xx + yy * yy > radius * radius) continue;
                    int px = cx + xx, py = cy + yy;
                    if (px < 0 || px >= w || py < 0 || py >= h) continue;
                    Color c = pixels[py * w + px];
                    c.r *= 1f - strength;
                    c.g *= 1f - strength * 1.1f;
                    c.b *= 1f - strength * 1.3f;
                    pixels[py * w + px] = c;
                }
            }
        }
        int foldX = w / 2;
        for (int y = 0; y < h; y++)
        {
            Color c = pixels[y * w + foldX];
            pixels[y * w + foldX] = new Color(c.r * 0.94f, c.g * 0.93f, c.b * 0.91f);
        }

        paper.SetPixels(pixels);
        paper.Apply();
        paper.filterMode = FilterMode.Bilinear;
        paper.wrapMode = TextureWrapMode.Clamp;
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 1274126177u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777215f;
        }
    }
}
