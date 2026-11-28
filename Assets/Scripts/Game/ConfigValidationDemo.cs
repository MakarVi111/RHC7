using UnityEngine;

/// <summary>
/// Demo card for the invalid-data test scene. It validates a deliberately
/// broken level and shows which records are usable without entering play with
/// a broken state.
/// </summary>
public class ConfigValidationDemo : MonoBehaviour
{
    public LevelDefinition level;
    LevelValidationReport report;

    void Start()
    {
        report = LevelValidator.Validate(level);
        Debug.Log("[RHC7 validations] " + (report == null ? "none" : string.Join("\n", report.errors)));
    }

    void OnGUI()
    {
        if (report == null) return;
        GUI.skin.label.fontSize = 16;
        GUI.skin.label.wordWrap = true;

        var panel = new Rect(40, 40, Mathf.Min(760, Screen.width - 80), Mathf.Min(620, Screen.height - 80));
        GUI.color = new Color(0.13f, 0.08f, 0.05f, 0.92f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        float x = panel.x + 26;
        float y = panel.y + 22;
        float w = panel.width - 52;
        GUI.Label(new Rect(x, y, w, 30), "ПРОВЕРКА КОНФИГУРАЦИИ · RHC7", Title());
        y += 36;
        GUI.Label(new Rect(x, y, w, 24),
            "Валидных образцов: " + report.validCount + " / " + (level != null && level.specimens != null ? level.specimens.Count : 0),
            Body());
        y += 30;
        GUI.Label(new Rect(x, y, w, 24),
            report.ok ? "Состояние безопасно: можно запускать." : "Ошибки данных: невалидные записи будут пропущены.",
            Body());
        y += 34;

        foreach (var e in report.errors)
        {
            GUI.Label(new Rect(x, y, w, 24), "• " + e, Body());
            y += 24;
        }
        if (y < panel.yMax - 60)
            GUI.Label(new Rect(x, panel.yMax - 48, w, 24),
                "Игра продолжает работу с валидным подмножеством, без изменения исходного кода.", Small());
    }

    static GUIStyle Title()
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = 22;
        s.fontStyle = FontStyle.Bold;
        s.normal.textColor = new Color(0.88f, 0.72f, 0.42f);
        return s;
    }

    static GUIStyle Body()
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = 15;
        s.wordWrap = true;
        s.normal.textColor = new Color(0.88f, 0.82f, 0.68f);
        return s;
    }

    static GUIStyle Small()
    {
        var s = Body();
        s.fontSize = 12;
        s.normal.textColor = new Color(0.72f, 0.66f, 0.55f);
        return s;
    }
}
