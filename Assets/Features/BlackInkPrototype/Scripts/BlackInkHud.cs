using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace InkRunner.BlackInkPrototype
{
    /// <summary>Self-contained test HUD; it never changes any of the shared game UI.</summary>
    public sealed class BlackInkHud : MonoBehaviour
    {
        [Tooltip("Serialized by the test scene so the UI shader also remains available in a player build.")]
        public Shader gaugeShader;
        private static readonly Color Paper = new Color(0.97f, 0.95f, 0.90f, 0.97f);
        private static readonly Color Ink = new Color(0.13f, 0.12f, 0.11f, 1f);
        private static readonly Color Red = new Color(0.57f, 0.12f, 0.10f, 1f);
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(12);
        private readonly List<RectTransform> blockingRects = new List<RectTransform>(4);
        private Canvas hudCanvas;
        private GraphicRaycaster raycaster;
        private RectTransform fillMask;
        private TextMeshProUGUI inkReadout;
        private TextMeshProUGUI measurements;
        private TextMeshProUGUI stateLabel;
        private TextMeshProUGUI feedbackLabel;
        private Texture2D fillTexture;
        private Material brushMaterial;
        private Material frameMaterial;
        private GameObject ownedEventSystem;
        private Action resetAction;
        private Action refillAction;
        private int lastPoints;
        private int lastStrokes;
        private const float FillWidth = 438f;

        public void Build(Texture2D gaugeSource, Action reset, Action refill)
        {
            resetAction = reset;
            refillAction = refill;
            ClearVisuals();
            EnsureEventSystem();

            var canvasObject = new GameObject("BlackInkHudCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            hudCanvas = canvasObject.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            raycaster = canvasObject.GetComponent<GraphicRaycaster>();

            var panel = Panel("Ink and test measurements", canvasObject.transform, new Vector2(22f, 22f),
                new Vector2(546f, 246f));
            Label("BLACK INK / ENDLESS RUNNER", panel, new Vector2(20f, 12f), new Vector2(506f, 25f),
                18f, Red, FontStyles.Bold);
            BuildGauge(panel, gaugeSource);
            inkReadout = Label("", panel, new Vector2(22f, 100f), new Vector2(504f, 27f), 19f, Ink,
                FontStyles.Bold);
            measurements = Label("", panel, new Vector2(22f, 133f), new Vector2(504f, 64f), 16f, Ink);
            Button("Reset", panel, new Vector2(22f, 204f), () => resetAction?.Invoke());
            Button("Refill", panel, new Vector2(164f, 204f), () => refillAction?.Invoke());
            Label("Reset restarts the 3 s preparation.", panel, new Vector2(310f, 211f),
                new Vector2(215f, 20f), 13f, new Color(0.37f, 0.32f, 0.27f));

            var instructions = Panel("Drawing instructions", canvasObject.transform, new Vector2(-22f, 22f),
                new Vector2(462f, 111f), new Vector2(1f, 1f));
            Label("RUN AND DRAW INK", instructions, new Vector2(18f, 12f), new Vector2(426f, 25f),
                18f, Red, FontStyles.Bold);
            Label("Left drag: draw ink bridges across the gaps.\nObstacles are visual only. Refill restores ink.\nReset starts a fresh run.",
                instructions, new Vector2(18f, 43f), new Vector2(426f, 58f), 16f, Ink);

            var status = Panel("Run status", canvasObject.transform, new Vector2(0f, -22f),
                new Vector2(760f, 76f), new Vector2(0.5f, 0f));
            stateLabel = Label("", status, new Vector2(19f, 10f), new Vector2(722f, 27f),
                22f, Red, FontStyles.Bold);
            feedbackLabel = Label("", status, new Vector2(19f, 43f), new Vector2(722f, 23f),
                16f, Ink);
            Canvas.ForceUpdateCanvases();
        }

        public void Present(float current, float maximum, float bodyHeight, float thickness, float costPerUnit,
            bool replace, int points, int strokes, string state, string feedback)
        {
            if (hudCanvas == null)
                return;

            lastPoints = points;
            lastStrokes = strokes;
            float value = Mathf.Clamp(current, 0f, Mathf.Max(0f, maximum));
            float fraction = maximum > 0f ? Mathf.Clamp01(value / maximum) : 0f;
            fillMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, FillWidth * fraction);
            fillMask.gameObject.SetActive(fraction > 0f);
            float availableLength = costPerUnit > 0f ? value / costPerUnit : 0f;
            inkReadout.text = $"INK  {value:0.0} / {Mathf.Max(0f, maximum):0.0}     |     {availableLength:0.00} u available";
            measurements.text = $"BODY  {bodyHeight:0.000} u     |     THICKNESS  {thickness:0.000} u\n" +
                                $"COST  {costPerUnit:0.0} / u     |     PREVIOUS  {(replace ? "REPLACE" : "KEEP")}\n" +
                                $"POINTS  {points}     |     STROKES  {strokes}";
            stateLabel.text = state ?? string.Empty;
            feedbackLabel.text = feedback ?? string.Empty;
        }

        /// <summary>Extra runner metrics, using measured current Game view pixels.</summary>
        public void PresentRunner(float distance, float bodyPx, float inkPx, int platforms, int obstacles)
        {
            if (hudCanvas == null || measurements == null)
                return;

            measurements.text = $"BODY  {bodyPx:0.#} px     |     INK  {inkPx:0.#} px  (screen pixels)\n" +
                                $"DISTANCE  {Mathf.Max(0f, distance):0.0} u     |     PLATFORMS  {platforms}     |     DECOR  {obstacles}\n" +
                                $"POINTS  {lastPoints}     |     STROKES  {lastStrokes}";
        }

        /// <summary>Uses actual scaled uGUI geometry, including panel backgrounds and buttons.</summary>
        public bool IsScreenPointOverUI(Vector2 screenPoint)
        {
            if (hudCanvas == null || !hudCanvas.isActiveAndEnabled)
                return false;
            if (EventSystem.current != null && raycaster != null)
            {
                raycastResults.Clear();
                var pointer = new PointerEventData(EventSystem.current) { position = screenPoint };
                raycaster.Raycast(pointer, raycastResults);
                return raycastResults.Count > 0;
            }

            // Also works immediately during scene setup, before EventSystem's first Update.
            foreach (var rect in blockingRects)
                if (rect != null && rect.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null))
                    return true;
            return false;
        }

        private void BuildGauge(RectTransform panel, Texture2D source)
        {
            var gauge = Rect("Ink gauge (500 x 50)", panel, new Vector2(22f, 43f), new Vector2(500f, 50f));
            var empty = Rect("Empty interior", gauge, new Vector2(50f, 17f), new Vector2(FillWidth, 20f));
            var emptyImage = empty.gameObject.AddComponent<Image>();
            emptyImage.color = new Color(0.68f, 0.65f, 0.59f, 0.48f);
            emptyImage.raycastTarget = false;

            fillMask = Rect("Ink visibility mask", gauge, new Vector2(50f, 17f), new Vector2(FillWidth, 20f));
            fillMask.gameObject.AddComponent<RectMask2D>();
            var fill = Rect("Black ink texture (fixed width)", fillMask, Vector2.zero, new Vector2(FillWidth, 20f));
            var fillImage = fill.gameObject.AddComponent<RawImage>();
            fillTexture = CreateInkTexture();
            fillImage.texture = fillTexture;
            fillImage.raycastTarget = false;

            if (source == null)
            {
                // Missing art remains visible as an explicit diagnostic, while all controls still work.
                Label("INK GAUGE SOURCE MISSING", gauge, new Vector2(45f, 0f), new Vector2(455f, 50f),
                    18f, Red, FontStyles.Bold);
                return;
            }

            var shader = gaugeShader != null ? gaugeShader : Shader.Find("InkRunner/BlackInkPrototype/InkGaugeUI");
            if (shader != null)
            {
                brushMaterial = new Material(shader) { name = "InkGauge Brush Crop (runtime)" };
                brushMaterial.SetFloat("_SourceMode", 1f);
                frameMaterial = new Material(shader) { name = "InkGauge Red Frame (runtime)" };
                frameMaterial.SetFloat("_SourceMode", 2f);
            }

            // The 2146 x 733 original has transparent margins. All crops are UV-only;
            // the imported source bitmap is byte-identical to the supplied file.
            // Frame caps use equal 0.2 scaling in both axes. Body pieces preserve that
            // same scale, so border widths and painted grain are never stretched.
            SourcePart("Frame left cap", gauge, source, new Rect(204f, 280f, 100f, 200f),
                new Vector2(40f, 6f), new Vector2(20f, 40f), frameMaterial);
            float bodyX = 60f;
            float bodyRemaining = 420f;
            int piece = 0;
            while (bodyRemaining > 0.01f)
            {
                float width = Mathf.Min(80f, bodyRemaining);
                // Adjacent source regions vary the natural brush edge without rescaling it.
                float sourceX = 500f + (piece % 3) * 400f;
                SourcePart("Frame body " + piece, gauge, source, new Rect(sourceX, 280f, width * 5f, 200f),
                    new Vector2(bodyX, 6f), new Vector2(width, 40f), frameMaterial);
                bodyX += width;
                bodyRemaining -= width;
                piece++;
            }
            SourcePart("Frame right cap", gauge, source, new Rect(2020f, 280f, 100f, 200f),
                new Vector2(480f, 6f), new Vector2(20f, 40f), frameMaterial);

            // 388 : 325 preserves the brush's source aspect in its approximately 50 x 50 area.
            SourcePart("Brush illustration", gauge, source, new Rect(25f, 165f, 388f, 325f),
                new Vector2(0f, 4f), new Vector2(50f, 50f * 325f / 388f), brushMaterial);
        }

        private static void SourcePart(string name, Transform parent, Texture2D source, Rect pixelsFromTop,
            Vector2 position, Vector2 size, Material material)
        {
            var rect = Rect(name, parent, position, size);
            var raw = rect.gameObject.AddComponent<RawImage>();
            raw.texture = source;
            raw.material = material;
            raw.uvRect = new Rect(pixelsFromTop.x / source.width,
                1f - (pixelsFromTop.y + pixelsFromTop.height) / source.height,
                pixelsFromTop.width / source.width, pixelsFromTop.height / source.height);
            raw.raycastTarget = false;
        }

        private static Texture2D CreateInkTexture()
        {
            const int width = 512;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "BlackInk Gauge Grain (runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // Opaque black body, subtle horizontal grain; only the mask changes width.
                uint hash = (uint)(x * 374761393u + y * 668265263u);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                byte shade = (byte)(7 + ((hash >> 25) & 7) + (y % 5 == 0 ? 4 : 0));
                pixels[y * width + x] = new Color32(shade, (byte)(shade + 1), shade, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size,
            Vector2? anchor = null)
        {
            var rect = Rect(name, parent, position, size, anchor);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Paper;
            image.raycastTarget = true;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.39f, 0.27f, 0.21f, 0.22f);
            outline.effectDistance = new Vector2(1f, -1f);
            blockingRects.Add(rect);
            return rect;
        }

        private static TextMeshProUGUI Label(string value, Transform parent, Vector2 position, Vector2 size,
            float fontSize, Color color, FontStyles style = FontStyles.Normal)
        {
            var rect = Rect(value.Length > 0 ? value.Split('\n')[0] : "Readout", parent, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            return text;
        }

        private void Button(string caption, Transform parent, Vector2 position, Action callback)
        {
            var rect = Rect(caption + " button", parent, position, new Vector2(128f, 30f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Red;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.10f, 1.06f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => callback());
            var text = Label(caption, rect, new Vector2(0f, 1f), new Vector2(128f, 28f), 17f, Paper,
                FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            blockingRects.Add(rect);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size,
            Vector2? anchor = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var point = anchor ?? new Vector2(0f, 1f);
            rect.anchorMin = point;
            rect.anchorMax = point;
            rect.pivot = point;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            return rect;
        }

        private void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                ownedEventSystem = new GameObject("BlackInkPrototype EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform, false);
                eventSystem = ownedEventSystem.GetComponent<EventSystem>();
            }
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            module.enabled = true;
            foreach (var other in eventSystem.GetComponents<BaseInputModule>())
                if (other != module)
                    other.enabled = false;
            if (module.actionsAsset == null)
                module.AssignDefaultActions();
        }

        private void ClearVisuals()
        {
            if (hudCanvas != null)
            {
                hudCanvas.gameObject.SetActive(false);
                Release(hudCanvas.gameObject);
            }
            hudCanvas = null;
            raycaster = null;
            blockingRects.Clear();
            Release(fillTexture);
            Release(brushMaterial);
            Release(frameMaterial);
            fillTexture = null;
            brushMaterial = null;
            frameMaterial = null;
        }

        private static void Release(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            ClearVisuals();
            if (ownedEventSystem != null)
                Release(ownedEventSystem);
        }
    }
}
