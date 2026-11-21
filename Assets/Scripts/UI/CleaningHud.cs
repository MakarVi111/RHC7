using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RHC7
{
    /// <summary>
    /// Minimal, non-diegetic HUD used while the cleaning core is being tuned.
    /// The final game replaces most of this with objects on the table.
    /// </summary>
    public class CleaningHud : MonoBehaviour
    {
        private Canvas canvas;
        private Text boneSubtitle;
        private Text layerValue;
        private Text progressValue;
        private Text wearValue;
        private Text stateValue;
        private Text toolName;
        private Text toolHint;
        private Text banner;
        private Text tooltip;
        private Text warningText;
        private GameObject tutorialPanel;
        private Text tutorialTitle;
        private Text tutorialBody;
        private float layerFlash;
        private Text labelValue;
        private GameObject atlasOverlay;
        private Text atlasBoneLabel;
        private Button atlasConfirmButton;
        private Text atlasConfirmLabel;
        private readonly Image[] atlasCards = new Image[3];
        private readonly Text[] atlasCardTitles = new Text[3];
        private readonly Text[] atlasCardBodies = new Text[3];
        private int atlasSelected = -1;
        private Action<int> atlasCallback;
        private Text completionTitle;
        private Text completionStats;
        private GameObject completionCard;
        private GameObject focusOverlay;
        private Image cursorRing;
        private Image cursorDot;
        private Image magnifierVignette;
        private Image damageVignette;
        private Sprite ringSprite;
        private Sprite radialSprite;
        private Font font;

        private float bannerTimer;
        private float damageFlash;
        private float magnifierAmount;
        private float targetMagnifier;

        private static readonly Color Ivory = new Color(0.93f, 0.88f, 0.78f, 1f);
        private static readonly Color Muted = new Color(0.72f, 0.66f, 0.55f, 1f);
        private static readonly Color Gold = new Color(0.85f, 0.71f, 0.43f, 1f);
        private static readonly Color Danger = new Color(0.78f, 0.33f, 0.24f, 1f);
        private static readonly Color Panel = new Color(0.055f, 0.048f, 0.04f, 0.72f);

        public Action RestartRequested;
        public Action ResetCurrentRequested;
        public Action AtlasRequested;
        private Image atlasButtonImage;
        private Text atlasButtonLabel;

        public void Init()
        {
            font = LoadFont();
            BuildCanvas();
            BuildStats();
            BuildToolCard();
            BuildResetButton();
            BuildHelp();
            BuildBanner();
            BuildTooltip();
            BuildWarning();
            BuildTutorial();
            BuildCompletion();
            BuildAtlas();
            BuildFocusOverlay();
            BuildCursor();
            BuildVignettes();
        }

        public void SetTool(ToolDefinition tool)
        {
            if (toolName == null || tool == null) return;
            toolName.text = tool.displayName;
            toolHint.text = tool.useHint;
        }

        public void SetBone(int index, int total, string boneName)
        {
            if (boneSubtitle == null) return;
            string name = string.IsNullOrEmpty(boneName) ? "" : "  ·  " + boneName.ToUpperInvariant();
            boneSubtitle.text = "КОСТЬ " + index + " / " + total + name;
        }

        public void SetLabel(string text)
        {
            if (labelValue == null) return;
            bool hasLabel = !string.IsNullOrEmpty(text) && text != "—";
            labelValue.text = hasLabel ? text : "—";
            labelValue.color = hasLabel ? Gold : Muted;
        }

        public bool IsAtlasOpen => atlasOverlay != null && atlasOverlay.activeSelf;

        public void SetReadout(RockLayer layer, float progress, float wear, DamageStage stage, float valueMultiplier, bool cleaned)
        {
            if (layerValue == null) return;

            switch (layer)
            {
                case RockLayer.Loose: layerValue.text = "верхний слой"; break;
                default: layerValue.text = "нижний слой"; break;
            }
            layerValue.color = layer == RockLayer.Contact ? Danger : Ivory;
            if (layerFlash > 0f)
                layerValue.color = Color.Lerp(layerValue.color, Gold, Mathf.Clamp01(layerFlash));

            progressValue.text = Mathf.RoundToInt(progress * 100f) + " %";
            wearValue.text = Mathf.RoundToInt(wear * 100f) + " %";
            wearValue.color = wear > 0.5f ? Danger : (wear > 0.25f ? Gold : Ivory);

            stateValue.text = StageName(stage);
            stateValue.color = stage == DamageStage.None ? (cleaned ? Gold : Ivory) : Danger;

            if (cleaned && stage == DamageStage.None)
                stateValue.text = "без повреждений";
            else if (valueMultiplier < 0.999f)
                stateValue.text += "  (" + Mathf.RoundToInt(valueMultiplier * 100f) + " %)";

            UpdateWarning(layer, wear);
        }

        public void SetCursor(Vector2 screenPos, float diameter, bool visible, bool overTool)
        {
            if (cursorRing == null) return;
            cursorRing.enabled = visible;
            if (!visible) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPos, null, out Vector2 local);
            cursorRing.rectTransform.localPosition = local;
            cursorRing.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            cursorRing.color = overTool ? Gold : new Color(0.95f, 0.92f, 0.85f, 0.85f);

            if (cursorDot != null)
            {
                cursorDot.enabled = visible;
                cursorDot.rectTransform.localPosition = local;
                cursorDot.rectTransform.sizeDelta = new Vector2(overTool ? 4f : 3f, overTool ? 4f : 3f);
                cursorDot.color = overTool ? Gold : new Color(0.98f, 0.95f, 0.88f, 0.95f);
            }
        }

        public void SetMagnifier(bool on)
        {
            targetMagnifier = on ? 1f : 0f;
        }

        public void UpdateVisuals(float dt)
        {
            layerFlash = Mathf.Max(0f, layerFlash - dt * 2.2f);

            magnifierAmount = Mathf.Lerp(magnifierAmount, targetMagnifier, 1f - Mathf.Exp(-10f * dt));
            if (magnifierVignette != null)
                magnifierVignette.color = new Color(0.02f, 0.018f, 0.014f, 0.72f * magnifierAmount);

            if (damageFlash > 0f)
            {
                damageFlash = Mathf.Max(0f, damageFlash - dt * 1.8f);
                if (damageVignette != null)
                    damageVignette.color = new Color(0.55f, 0.08f, 0.05f, damageFlash * 0.32f);
            }

            if (bannerTimer > 0f)
            {
                bannerTimer -= dt;
                float a = Mathf.Clamp01(bannerTimer);
                var c = banner.color;
                c.a = a;
                banner.color = c;
            }
        }

        public void ShowBanner(string text, Color color, float seconds = 2f)
        {
            if (banner == null) return;
            banner.text = text;
            color.a = 1f;
            banner.color = color;
            bannerTimer = seconds;
        }

        public void FlashDamage()
        {
            damageFlash = 1f;
        }

        public void ShowCompletion(float progress, float wear, DamageStage stage, float valueMultiplier, bool destroyed, string extra = null)
        {
            if (completionCard == null) return;
            completionCard.SetActive(true);
            completionTitle.text = destroyed ? "КОСТЬ РАЗРУШЕНА" : "КОСТЬ ОЧИЩЕНА";
            completionTitle.color = destroyed
                ? new Color(0.55f, 0.18f, 0.12f, 1f)
                : new Color(0.18f, 0.15f, 0.11f, 1f);

            float score = valueMultiplier * 100f;
            string state = StageName(stage);
            string body = "Очистка  " + Mathf.RoundToInt(progress * 100f) + " %\n";
            body += "Износ  " + Mathf.RoundToInt(wear * 100f) + " %\n";
            body += "Состояние  " + state + "\n";
            body += "Оценка  " + Mathf.RoundToInt(score) + " %";
            if (destroyed) body += "\nСлот останется пустым.";
            if (!string.IsNullOrEmpty(extra)) body += "\n\n" + extra;
            completionStats.text = body;
        }

        public void HideCompletion()
        {
            if (completionCard != null) completionCard.SetActive(false);
        }

        private void BuildCanvas()
        {
            var go = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }
        }

        private void BuildStats()
        {
            var panel = MakePanel("Stats", canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(318f, 270f), Panel);

            MakeText(panel.transform, "Title", "РХЦ-7", 30, FontStyle.Bold, Ivory,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -16f), new Vector2(280f, 34f));

            boneSubtitle = MakeText(panel.transform, "Subtitle", "УЧАСТОК ЧИСТКИ", 12, FontStyle.Normal, Muted,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(19f, -50f), new Vector2(280f, 18f));

            MakeImage(panel.transform, "Line", new Color(0.85f, 0.71f, 0.43f, 0.45f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -74f), new Vector2(282f, 1f));

            layerValue = MakeStatRow(panel.transform, 88f, "СЛОЙ");
            progressValue = MakeStatRow(panel.transform, 122f, "ПРОГРЕСС");
            wearValue = MakeStatRow(panel.transform, 156f, "ИЗНОС");
            stateValue = MakeStatRow(panel.transform, 190f, "СОСТОЯНИЕ");
            labelValue = MakeStatRow(panel.transform, 224f, "ЯРЛЫК");
            labelValue.text = "—";
            labelValue.color = Muted;
        }

        private Text MakeStatRow(Transform parent, float y, string label)
        {
            MakeText(parent, label + " Label", label, 13, FontStyle.Normal, Muted,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -y), new Vector2(120f, 24f));

            return MakeText(parent, label + " Value", "", 17, FontStyle.Normal, Ivory,
                TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -y), new Vector2(190f, 24f));
        }

        private void BuildToolCard()
        {
            var panel = MakePanel("ToolCard", canvas.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(28f, 26f), new Vector2(420f, 98f),
                new Color(0.055f, 0.048f, 0.04f, 0.88f));

            toolName = MakeText(panel.transform, "ToolName", "Кисть", 23, FontStyle.Bold,
                new Color(0.96f, 0.90f, 0.78f, 1f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -14f), new Vector2(384f, 30f));

            toolHint = MakeText(panel.transform, "ToolHint", "", 14, FontStyle.Normal,
                new Color(0.82f, 0.76f, 0.64f, 1f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -48f), new Vector2(388f, 40f));
            toolHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            toolHint.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void BuildResetButton()
        {
            var go = new GameObject("ResetButton", typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(28f, 136f);
            rt.sizeDelta = new Vector2(150f, 38f);
            go.GetComponent<Image>().color = new Color(0.11f, 0.09f, 0.07f, 0.92f);

            var label = MakeText(go.transform, "Label", "СБРОС", 14, FontStyle.Bold, Gold,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            go.GetComponent<Button>().onClick.AddListener(() => ResetCurrentRequested?.Invoke());

            var atlas = new GameObject("AtlasButton", typeof(Image), typeof(Button));
            atlas.transform.SetParent(canvas.transform, false);
            var atlasRt = atlas.GetComponent<RectTransform>();
            atlasRt.anchorMin = new Vector2(0f, 0f);
            atlasRt.anchorMax = new Vector2(0f, 0f);
            atlasRt.pivot = new Vector2(0f, 0f);
            atlasRt.anchoredPosition = new Vector2(190f, 136f);
            atlasRt.sizeDelta = new Vector2(150f, 38f);
            atlasButtonImage = atlas.GetComponent<Image>();
            atlasButtonImage.color = new Color(0.11f, 0.09f, 0.07f, 0.92f);

            atlasButtonLabel = MakeText(atlas.transform, "Label", "АТЛАС", 14, FontStyle.Bold, Muted,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            atlasButtonLabel.rectTransform.offsetMin = Vector2.zero;
            atlasButtonLabel.rectTransform.offsetMax = Vector2.zero;
            atlas.GetComponent<Button>().onClick.AddListener(() => AtlasRequested?.Invoke());
        }

        public void SetAtlasReady(bool ready)
        {
            if (atlasButtonImage != null)
                atlasButtonImage.color = ready
                    ? new Color(0.34f, 0.26f, 0.13f, 0.96f)
                    : new Color(0.11f, 0.09f, 0.07f, 0.92f);
            if (atlasButtonLabel != null)
                atlasButtonLabel.color = ready ? Ivory : Muted;
        }

        private void BuildHelp()
        {
            MakeText(canvas.transform, "Help",
                "ЛКМ — чистить    ·    ПКМ — лупа    ·    клик по инструменту — взять\nколесо на столе — ближе / дальше    ·    колесо над костью — повернуть",
                14, FontStyle.Normal, new Color(0.78f, 0.72f, 0.61f, 0.85f),
                TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(1260f, 42f));

            MakeText(canvas.transform, "ResetHint", "R — начать заново", 13, FontStyle.Normal,
                new Color(0.62f, 0.57f, 0.48f, 0.8f), TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-28f, 26f), new Vector2(260f, 22f));
        }

        private void BuildBanner()
        {
            banner = MakeText(canvas.transform, "Banner", "", 34, FontStyle.Bold, Danger,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 190f), new Vector2(900f, 60f));
            banner.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void BuildTooltip()
        {
            tooltip = MakeText(canvas.transform, "Tooltip", "", 15, FontStyle.Normal, Gold,
                TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(260f, 24f));
            tooltip.gameObject.SetActive(false);
        }

        public void SetTooltip(string text, Vector2 screenPos)
        {
            if (tooltip == null) return;
            if (string.IsNullOrEmpty(text))
            {
                tooltip.gameObject.SetActive(false);
                return;
            }

            tooltip.gameObject.SetActive(true);
            tooltip.text = text;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPos, null, out Vector2 local);
            tooltip.rectTransform.localPosition = local + new Vector2(20f, -28f);
        }

        private void BuildWarning()
        {
            warningText = MakeText(canvas.transform, "Warning", "", 18, FontStyle.Bold, Danger,
                TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 72f), new Vector2(1000f, 26f));
            warningText.gameObject.SetActive(false);
        }

        private void BuildTutorial()
        {
            var panel = MakePanel("Tutorial", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(680f, 92f),
                new Color(0.055f, 0.048f, 0.04f, 0.86f));
            tutorialPanel = panel;

            MakeImage(panel.transform, "Accent", new Color(0.85f, 0.71f, 0.43f, 0.85f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(3f, 92f));

            tutorialTitle = MakeText(panel.transform, "Title", "", 14, FontStyle.Bold, Gold,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -10f), new Vector2(640f, 22f));

            tutorialBody = MakeText(panel.transform, "Body", "", 14, FontStyle.Normal, Ivory,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -34f), new Vector2(644f, 50f));
            tutorialBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            tutorialBody.verticalOverflow = VerticalWrapMode.Overflow;

            tutorialPanel.SetActive(false);
        }

        public void SetTutorial(string title, string body)
        {
            if (tutorialPanel == null) return;
            tutorialPanel.SetActive(true);
            tutorialTitle.text = title;
            tutorialBody.text = body;
        }

        public void HideTutorial()
        {
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
        }

        public void FlashLayer()
        {
            layerFlash = 1f;
        }

        private void UpdateWarning(RockLayer layer, float wear)
        {
            if (warningText == null) return;

            string warn = "";
            if (wear >= 0.75f) warn = "ИЗНОС " + Mathf.RoundToInt(wear * 100f) + " % — ТРЕЩИНА";
            else if (wear >= 0.50f) warn = "ИЗНОС " + Mathf.RoundToInt(wear * 100f) + " % — ЕЩЁ НЕМНОГО И СКОЛ";
            else if (wear >= 0.25f) warn = "ИЗНОС " + Mathf.RoundToInt(wear * 100f) + " % — ЦАРАПИНА";

            if (warn.Length > 0 && layer == RockLayer.Contact) warn += "   ·   КОНТАКТНЫЙ СЛОЙ";

            warningText.text = warn;
            warningText.gameObject.SetActive(warn.Length > 0);
        }

        private void BuildCompletion()
        {
            var panel = MakePanel("Completion", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 360f),
                new Color(0.90f, 0.86f, 0.75f, 0.97f));
            completionCard = panel;
            panel.GetComponent<Image>().raycastTarget = true;

            MakeText(panel.transform, "Act", "РАБОЧИЙ АКТ", 12, FontStyle.Normal,
                new Color(0.35f, 0.30f, 0.22f, 1f), TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -26f), new Vector2(500f, 18f));

            completionTitle = MakeText(panel.transform, "TitleText", "КОСТЬ ОЧИЩЕНА", 30, FontStyle.Bold,
                new Color(0.18f, 0.15f, 0.11f, 1f), TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -52f), new Vector2(540f, 38f));

            completionStats = MakeText(panel.transform, "Stats", "", 18, FontStyle.Normal,
                new Color(0.22f, 0.19f, 0.14f, 1f), TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                new Vector2(-210f, -118f), new Vector2(420f, 160f));
            completionStats.lineSpacing = 1.4f;

            var btnGo = new GameObject("Restart", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(panel.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0f, 26f);
            btnRect.sizeDelta = new Vector2(220f, 46f);
            btnGo.GetComponent<Image>().color = new Color(0.20f, 0.17f, 0.13f, 1f);

            var label = MakeText(btnGo.transform, "Label", "ПРОЙТИ ЗАНОВО", 15, FontStyle.Bold,
                new Color(0.93f, 0.88f, 0.78f, 1f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            btnGo.GetComponent<Button>().onClick.AddListener(() => RestartRequested?.Invoke());
            completionCard.SetActive(false);
        }

        private void BuildAtlas()
        {
            var overlay = new GameObject("AtlasOverlay", typeof(Image));
            overlay.transform.SetParent(canvas.transform, false);
            var background = overlay.GetComponent<Image>();
            background.color = new Color(0.025f, 0.021f, 0.017f, 0.84f);
            background.raycastTarget = true;
            Stretch(background.rectTransform);
            atlasOverlay = overlay;

            var panel = MakePanel("AtlasPanel", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 620f),
                new Color(0.88f, 0.84f, 0.72f, 0.98f));

            var darkInk = new Color(0.18f, 0.15f, 0.11f, 1f);

            MakeText(panel.transform, "Title", "АТЛАС", 32, FontStyle.Bold, darkInk,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(900f, 42f));

            atlasBoneLabel = MakeText(panel.transform, "BoneLabel", "КОСТЬ: —", 16, FontStyle.Bold, darkInk,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -68f), new Vector2(900f, 24f));

            MakeText(panel.transform, "Hint", "Сравните форму, размер и породу. Выберите один вид.", 14, FontStyle.Normal,
                darkInk, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -94f), new Vector2(900f, 22f));

            float[] xs = { -320f, 0f, 320f };
            for (int i = 0; i < 3; i++)
            {
                var card = new GameObject("AtlasCard_" + i, typeof(Image), typeof(Button));
                card.transform.SetParent(panel.transform, false);
                var rt = card.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(xs[i], 8f);
                rt.sizeDelta = new Vector2(300f, 326f);

                var image = card.GetComponent<Image>();
                image.color = new Color(0.13f, 0.11f, 0.085f, 1f);
                image.raycastTarget = true;
                atlasCards[i] = image;

                int index = i;
                card.GetComponent<Button>().onClick.AddListener(() => SelectAtlasOption(index));

                atlasCardTitles[i] = MakeText(card.transform, "Name", "", 20, FontStyle.Bold, Gold,
                    TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -18f), new Vector2(268f, 28f));

                atlasCardBodies[i] = MakeText(card.transform, "Body", "", 14, FontStyle.Normal, Ivory,
                    TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(16f, -58f), new Vector2(268f, 250f));
                atlasCardBodies[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                atlasCardBodies[i].verticalOverflow = VerticalWrapMode.Overflow;
                atlasCardBodies[i].lineSpacing = 1.25f;
            }

            var confirm = new GameObject("AtlasConfirm", typeof(Image), typeof(Button));
            confirm.transform.SetParent(panel.transform, false);
            var confirmRt = confirm.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(0.5f, 0f);
            confirmRt.anchorMax = new Vector2(0.5f, 0f);
            confirmRt.pivot = new Vector2(0.5f, 0f);
            confirmRt.anchoredPosition = new Vector2(0f, 24f);
            confirmRt.sizeDelta = new Vector2(280f, 46f);
            confirm.GetComponent<Image>().color = new Color(0.20f, 0.17f, 0.13f, 1f);
            atlasConfirmButton = confirm.GetComponent<Button>();
            atlasConfirmButton.interactable = false;

            atlasConfirmLabel = MakeText(confirm.transform, "Label", "ПРИКРЕПИТЬ ЯРЛЫК", 15, FontStyle.Bold, Muted,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            atlasConfirmLabel.rectTransform.offsetMin = Vector2.zero;
            atlasConfirmLabel.rectTransform.offsetMax = Vector2.zero;
            atlasConfirmButton.onClick.AddListener(ConfirmAtlas);

            var close = new GameObject("AtlasClose", typeof(Image), typeof(Button));
            close.transform.SetParent(panel.transform, false);
            var closeRt = close.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-18f, -18f);
            closeRt.sizeDelta = new Vector2(110f, 32f);
            close.GetComponent<Image>().color = new Color(0.20f, 0.17f, 0.13f, 1f);
            close.GetComponent<Button>().onClick.AddListener(CloseAtlas);

            var closeLabel = MakeText(close.transform, "Label", "ЗАКРЫТЬ", 13, FontStyle.Bold, Ivory,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            closeLabel.rectTransform.offsetMin = Vector2.zero;
            closeLabel.rectTransform.offsetMax = Vector2.zero;

            atlasOverlay.SetActive(false);
        }

        public void OpenAtlas(string boneName, string[] names, string[] descriptions, Action<int> onConfirm)
        {
            if (atlasOverlay == null) return;

            atlasCallback = onConfirm;
            atlasSelected = -1;
            atlasBoneLabel.text = "КОСТЬ: " + (string.IsNullOrEmpty(boneName) ? "—" : boneName.ToUpperInvariant());
            atlasConfirmButton.interactable = false;
            atlasConfirmLabel.color = Muted;

            for (int i = 0; i < 3; i++)
            {
                bool hasEntry = names != null && i < names.Length;
                atlasCards[i].gameObject.SetActive(hasEntry);
                if (!hasEntry) continue;

                atlasCards[i].color = new Color(0.13f, 0.11f, 0.085f, 1f);
                atlasCardTitles[i].text = names[i];
                atlasCardBodies[i].text = descriptions != null && i < descriptions.Length ? descriptions[i] : "";
            }

            atlasOverlay.SetActive(true);
        }

        private void SelectAtlasOption(int index)
        {
            if (atlasOverlay == null || !atlasOverlay.activeSelf) return;
            if (index < 0 || index >= 3) return;

            atlasSelected = index;
            for (int i = 0; i < 3; i++)
            {
                atlasCards[i].color = i == index
                    ? new Color(0.36f, 0.28f, 0.15f, 1f)
                    : new Color(0.13f, 0.11f, 0.085f, 1f);
            }

            atlasConfirmButton.interactable = true;
            atlasConfirmLabel.color = Ivory;
        }

        private void ConfirmAtlas()
        {
            if (atlasSelected < 0) return;

            int selected = atlasSelected;
            var callback = atlasCallback;
            CloseAtlas();
            callback?.Invoke(selected);
        }

        public void CloseAtlas()
        {
            if (atlasOverlay == null) return;
            atlasOverlay.SetActive(false);
            atlasCallback = null;
            atlasSelected = -1;
            if (atlasConfirmButton != null) atlasConfirmButton.interactable = false;
        }

        private void BuildFocusOverlay()
        {
            var go = new GameObject("FocusOverlay", typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.025f, 0.021f, 0.017f, 0.90f);
            img.raycastTarget = false;
            Stretch(img.rectTransform);

            MakeText(go.transform, "FocusTitle", "КЛИКНИТЕ ПО ОКНУ ИГРЫ", 34, FontStyle.Bold, Ivory,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 34f), new Vector2(1100f, 50f));

            MakeText(go.transform, "FocusHint", "мышь: чистка · лупа · колесо", 17, FontStyle.Normal, Muted,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -18f), new Vector2(1100f, 30f));

            focusOverlay = go;
            go.SetActive(false);
        }

        private static bool IsGameFocused()
        {
#if UNITY_EDITOR
            if (!Application.isFocused) return false;
            var window = UnityEditor.EditorWindow.focusedWindow;
            return window != null && window.GetType().Name == "GameView";
#else
            return Application.isFocused;
#endif
        }

        private void BuildCursor()
        {
            ringSprite = MakeRingSprite(128, 3.2f);
            var go = new GameObject("Cursor", typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            cursorRing = go.GetComponent<Image>();
            cursorRing.sprite = ringSprite;
            cursorRing.raycastTarget = false;
            cursorRing.preserveAspect = true;
            var rt = cursorRing.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(60f, 60f);

            var dotGo = new GameObject("CursorDot", typeof(Image));
            dotGo.transform.SetParent(canvas.transform, false);
            cursorDot = dotGo.GetComponent<Image>();
            cursorDot.color = new Color(0.98f, 0.95f, 0.88f, 0.95f);
            cursorDot.raycastTarget = false;
            var dotRt = cursorDot.rectTransform;
            dotRt.anchorMin = new Vector2(0.5f, 0.5f);
            dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = new Vector2(3f, 3f);
        }

        private void BuildVignettes()
        {
            radialSprite = MakeRadialSprite(256);

            var flash = new GameObject("DamageFlash", typeof(Image));
            flash.transform.SetParent(canvas.transform, false);
            damageVignette = flash.GetComponent<Image>();
            damageVignette.sprite = radialSprite;
            damageVignette.raycastTarget = false;
            damageVignette.color = new Color(0.55f, 0.08f, 0.05f, 0f);
            Stretch(damageVignette.rectTransform);
            damageVignette.transform.SetAsFirstSibling();

            var mag = new GameObject("MagnifierVignette", typeof(Image));
            mag.transform.SetParent(canvas.transform, false);
            magnifierVignette = mag.GetComponent<Image>();
            magnifierVignette.sprite = radialSprite;
            magnifierVignette.raycastTarget = false;
            magnifierVignette.color = new Color(0.02f, 0.018f, 0.014f, 0f);
            Stretch(magnifierVignette.rectTransform);
            magnifierVignette.transform.SetAsFirstSibling();

            cursorRing.transform.SetAsLastSibling();
        }

        private GameObject MakePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        private Image MakeImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text MakeText(Transform parent, string name, string content, int fontSize, FontStyle style, Color color,
            TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Font LoadFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Segoe UI", 16);
            return f;
        }

        private static Sprite MakeRingSprite(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float outer = size * 0.47f;
            float inner = outer - thickness * 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01((outer - d) / thickness) * Mathf.Clamp01((d - inner) / thickness);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite MakeRadialSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float maxD = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / maxD;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.45f));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static string StageName(DamageStage stage)
        {
            switch (stage)
            {
                case DamageStage.Scratch: return "царапина";
                case DamageStage.Chip: return "скол";
                case DamageStage.Crack: return "трещина";
                case DamageStage.Destroyed: return "разрушена";
                default: return "без повреждений";
            }
        }
    }
}
