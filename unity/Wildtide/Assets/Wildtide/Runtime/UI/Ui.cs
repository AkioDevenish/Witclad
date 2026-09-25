using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wildtide.UI
{
    /// <summary>
    /// Builds uGUI from code: chunky rounded panels in deep navy and sea-foam with gold highlights
    /// (the battle-screen prompt's palette). Everything scales from a 1920x1080 landscape reference.
    /// </summary>
    public static class Ui
    {
        public static readonly Color Navy = new Color32(0x1B, 0x2A, 0x4A, 0xF0);
        public static readonly Color NavyLight = new Color32(0x2C, 0x41, 0x6B, 0xFF);
        public static readonly Color SeaFoam = new Color32(0x7F, 0xE0, 0xC8, 0xFF);
        public static readonly Color Gold = new Color32(0xF5, 0xC2, 0x4C, 0xFF);
        public static readonly Color Cream = new Color32(0xFB, 0xF6, 0xE9, 0xFF);
        public static readonly Color HpGreen = new Color32(0x5C, 0xD6, 0x6E, 0xFF);
        public static readonly Color HpYellow = new Color32(0xF2, 0xC9, 0x3B, 0xFF);
        public static readonly Color HpRed = new Color32(0xE8, 0x4A, 0x4A, 0xFF);

        static Font font;
        static Sprite rounded;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>A white rounded rectangle, 9-sliced so any panel size keeps round corners.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64, radius = 22;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return rounded;
            }
        }

        public static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // phones vary in width far more than height in landscape
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>A child that stays inside the iPhone notch / home-indicator safe area.</summary>
        public static RectTransform SafeRoot(Canvas canvas)
        {
            var rt = Node("Safe Area", canvas.transform);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeArea>();
            return rt;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Anchor to a point of the parent (0..1 each axis) with a fixed size, offset in reference pixels.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        public static Image Panel(Transform parent, Color color, string name = "Panel")
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public static Image Solid(Transform parent, Color color, string name = "Fill")
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Bold)
        {
            var rt = Node("Label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string text, Color color, UnityAction onClick, int fontSize = 44,
            Color? textColor = null)
        {
            var img = Panel(parent, color, "Button " + text);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            var label = Label(img.transform, text, fontSize, textColor ?? Navy, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 12f);
            return button;
        }

        public static void SetText(Button b, string text) => b.GetComponentInChildren<Text>().text = text;

        public static Color HpColor(float fraction) => fraction > 0.5f ? HpGreen : fraction > 0.2f ? HpYellow : HpRed;

        /// <summary>Vertical stack of children, spaced evenly.</summary>
        public static VerticalLayoutGroup Column(RectTransform rt, float spacing, int padding = 0)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = true;
            return v;
        }

        public static GridLayoutGroup Grid(RectTransform rt, Vector2 cell, Vector2 spacing, int columns)
        {
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            g.childAlignment = TextAnchor.MiddleCenter;
            return g;
        }

        public static void Clear(Transform t)
        {
            // Detach first so childCount and layout update straight away (Destroy waits until end of frame).
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    /// <summary>A health (or XP) bar: a rounded track with a fill that eases to its target.</summary>
    public sealed class Bar : MonoBehaviour
    {
        RectTransform fill;
        Image fillImage;
        float shown = 1f, target = 1f;
        public bool ColorByValue = true;
        public float Speed = 1.2f;

        public static Bar Create(Transform parent, Color fillColor, bool colorByValue = true)
        {
            var track = Ui.Panel(parent, new Color(0f, 0f, 0f, 0.45f), "Bar");
            var bar = track.gameObject.AddComponent<Bar>();
            bar.fillImage = Ui.Panel(track.transform, fillColor, "Fill");
            bar.fill = bar.fillImage.rectTransform;
            bar.fill.anchorMin = Vector2.zero;
            bar.fill.anchorMax = Vector2.one;
            bar.fill.offsetMin = new Vector2(4f, 4f);
            bar.fill.offsetMax = new Vector2(-4f, -4f);
            bar.ColorByValue = colorByValue;
            return bar;
        }

        public bool Animating => !Mathf.Approximately(shown, target);

        public void Set(float fraction, bool instant)
        {
            target = Mathf.Clamp01(fraction);
            if (instant) shown = target;
            Apply();
        }

        void Update()
        {
            if (!Animating) return;
            shown = Mathf.MoveTowards(shown, target, Speed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            fill.anchorMax = new Vector2(Mathf.Max(0.0001f, shown), 1f);
            fill.gameObject.SetActive(shown > 0.001f);
            if (ColorByValue) fillImage.color = Ui.HpColor(shown);
        }
    }

    /// <summary>Keeps its RectTransform inside Screen.safeArea (notch, rounded corners, home indicator).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect applied;
        Vector2Int screen;

        void Update()
        {
            var safe = Screen.safeArea;
            if (safe == applied && screen.x == Screen.width && screen.y == Screen.height) return;
            applied = safe;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Floating thumbstick: touch anywhere on the left side of the screen and drag.
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Vector2 Value { get; private set; }
        public float Radius = 130f;

        RectTransform area, stick, knob;
        Vector2 home;
        int pointer = int.MinValue;

        public static VirtualJoystick Create(RectTransform parent)
        {
            var area = Ui.Node("Joystick Area", parent);
            area.anchorMin = Vector2.zero;
            area.anchorMax = new Vector2(0.45f, 0.75f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            var hit = area.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f); // invisible but still catches touches

            var j = area.gameObject.AddComponent<VirtualJoystick>();
            j.area = area;
            j.stick = Ui.Panel(area, new Color(1f, 1f, 1f, 0.18f), "Stick").rectTransform;
            j.stick.anchorMin = j.stick.anchorMax = Vector2.zero;
            j.stick.sizeDelta = Vector2.one * 300f;
            j.home = new Vector2(260f, 240f);
            j.stick.anchoredPosition = j.home;
            j.stick.GetComponent<Image>().raycastTarget = false;
            j.knob = Ui.Panel(j.stick, new Color(1f, 1f, 1f, 0.55f), "Knob").rectTransform;
            j.knob.sizeDelta = Vector2.one * 130f;
            j.knob.GetComponent<Image>().raycastTarget = false;
            return j;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var local))
                stick.anchoredPosition = local - area.rect.min; // anchors are bottom-left
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stick, e.position, e.pressEventCamera, out var local)) return;
            var offset = Vector2.ClampMagnitude(local, Radius);
            knob.anchoredPosition = offset;
            var v = offset / Radius;
            Value = v.magnitude < 0.12f ? Vector2.zero : v; // small dead zone
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            Release();
        }

        public void Release()
        {
            pointer = int.MinValue;
            Value = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (stick != null) stick.anchoredPosition = home;
        }

        void OnDisable() => Release();
    }

    /// <summary>A modal text box: tap to continue.</summary>
    public sealed class Dialog : MonoBehaviour
    {
        Text text;
        Action onClose;

        public static Dialog Create(RectTransform parent)
        {
            var panel = Ui.Panel(parent, Ui.Navy, "Dialog");
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0.08f, 0.03f);
            rt.anchorMax = new Vector2(0.92f, 0.26f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var d = panel.gameObject.AddComponent<Dialog>();
            d.text = Ui.Label(rt, "", 50, Ui.Cream, TextAnchor.MiddleLeft, FontStyle.Normal);
            Ui.Stretch(d.text.rectTransform, 48f);
            var hint = Ui.Label(rt, "▼", 36, Ui.Gold, TextAnchor.LowerRight);
            Ui.Stretch(hint.rectTransform, 22f);
            panel.gameObject.AddComponent<Button>().onClick.AddListener(d.Close);
            panel.gameObject.SetActive(false);
            return d;
        }

        public bool Open => gameObject.activeSelf;

        public void Show(string message, Action closed = null)
        {
            text.text = message;
            onClose = closed;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            var cb = onClose;
            onClose = null;
            cb?.Invoke();
        }
    }
}
