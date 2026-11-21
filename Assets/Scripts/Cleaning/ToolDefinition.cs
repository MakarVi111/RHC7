using UnityEngine;

namespace RHC7
{
    public enum ToolKind
    {
        Brush = 0,
        Scraper = 1,
        Chisel = 2
    }

    [System.Serializable]
    public class ToolDefinition
    {
        public ToolKind kind;
        public string displayName;
        public string useHint;

        [Tooltip("Условная скорость снятия породы")]
        public float removal = 6f;

        [Tooltip("Условная скорость износа кости")]
        public float wear = 0.8f;

        [Tooltip("Радиус пятна контакта в UV (по окружности кости)")]
        public float radiusUV = 0.05f;

        [Range(0.1f, 0.95f)]
        public float softness = 0.7f;

        public Color dustColor = new Color(0.55f, 0.44f, 0.30f, 1f);

        public static ToolDefinition Brush()
        {
            return new ToolDefinition
            {
                kind = ToolKind.Brush,
                displayName = "Кисть",
                useHint = "Очень широкая и мягкая. Медленно, но безопасно.",
                removal = 6f,
                wear = 0.8f,
                radiusUV = 0.12f,
                softness = 0.98f,
                dustColor = new Color(0.62f, 0.50f, 0.35f, 1f)
            };
        }

        public static ToolDefinition Scraper()
        {
            return new ToolDefinition
            {
                kind = ToolKind.Scraper,
                displayName = "Скребок",
                useHint = "Средняя зона. Быстрее кисти, на контакте опасен.",
                removal = 18f,
                wear = 7f,
                radiusUV = 0.045f,
                softness = 0.95f,
                dustColor = new Color(0.52f, 0.40f, 0.27f, 1f)
            };
        }

        public static ToolDefinition Chisel()
        {
            return new ToolDefinition
            {
                kind = ToolKind.Chisel,
                displayName = "Зубило",
                useHint = "Широкий скос. Быстро берёт плотную породу, легко оставляет скол.",
                removal = 40f,
                wear = 4f,
                radiusUV = 0.080f,
                softness = 0.95f,
                dustColor = new Color(0.45f, 0.34f, 0.22f, 1f)
            };
        }

        public static ToolDefinition[] CreateDefaultSet()
        {
            return new[] { Brush(), Scraper(), Chisel() };
        }
    }
}
