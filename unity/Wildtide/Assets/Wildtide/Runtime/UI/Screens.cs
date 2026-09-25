using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wildtide.Core;

namespace Wildtide.UI
{
    /// <summary>A button you can hold: reports presses as they happen and whether it's still held down.</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool Held => pointer != int.MinValue;
        int pointer = int.MinValue;
        bool pressed;
        Image image;
        Color idle;

        public static HoldButton Create(RectTransform parent, string text, Vector2 size, Vector2 offset)
        {
            var img = Ui.Panel(parent, new Color(1f, 1f, 1f, 0.3f), "Button " + text);
            Ui.Place(img.rectTransform, new Vector2(1f, 0f), size, offset);
            var label = Ui.Label(img.transform, text, 52, Ui.Cream, TextAnchor.MiddleCenter);
            Ui.Stretch(label.rectTransform);
            label.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            var b = img.gameObject.AddComponent<HoldButton>();
            b.image = img;
            b.idle = img.color;
            return b;
        }

        /// <summary>True once per press.</summary>
        public bool TakePress()
        {
            bool p = pressed;
            pressed = false;
            return p;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Held) return;
            pointer = e.pointerId;
            pressed = true;
            image.color = new Color(1f, 1f, 1f, 0.55f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointer) Release();
        }

        void Release()
        {
            pointer = int.MinValue;
            if (image != null) image.color = idle;
        }

        void OnDisable()
        {
            Release();
            pressed = false;
        }
    }

    /// <summary>In-level HUD: stick on the left, jump on the right, pearls and time at the top.</summary>
    public sealed class Hud : MonoBehaviour
    {
        public VirtualJoystick Joystick { get; private set; }
        public HoldButton Jump { get; private set; }
        public Action MenuPressed;

        Canvas canvas;
        Text title, pearls, timer, toast;
        Image flash;
        float toastUntil;

        public static Hud Create()
        {
            var canvas = Ui.MakeCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<Hud>();
            hud.canvas = canvas;

            hud.flash = Ui.Solid(canvas.transform, new Color(1f, 1f, 1f, 0f), "Flash");
            Ui.Stretch(hud.flash.rectTransform);
            hud.flash.raycastTarget = false;

            var safe = Ui.SafeRoot(canvas);
            hud.Joystick = VirtualJoystick.Create(safe);
            hud.Jump = HoldButton.Create(safe, "JUMP", new Vector2(300f, 300f), new Vector2(-70f, 70f));

            var info = Ui.Panel(safe, Ui.Navy, "Info");
            Ui.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(560f, 150f), new Vector2(36f, -30f));
            info.raycastTarget = false;
            hud.title = Ui.Label(info.transform, "", 40, Ui.Cream);
            Ui.Place(hud.title.rectTransform, new Vector2(0f, 1f), new Vector2(500f, 60f), new Vector2(30f, -14f));
            hud.pearls = Ui.Label(info.transform, "", 40, Ui.SeaFoam);
            Ui.Place(hud.pearls.rectTransform, new Vector2(0f, 0f), new Vector2(260f, 60f), new Vector2(30f, 14f));
            hud.timer = Ui.Label(info.transform, "", 40, Ui.Gold, TextAnchor.MiddleRight);
            Ui.Place(hud.timer.rectTransform, new Vector2(1f, 0f), new Vector2(240f, 60f), new Vector2(-30f, 14f));

            var menu = Ui.MakeButton(safe, "Islands", Ui.SeaFoam, () => hud.MenuPressed?.Invoke(), 40);
            Ui.Place((RectTransform)menu.transform, new Vector2(1f, 1f), new Vector2(240f, 100f), new Vector2(-36f, -30f));

            hud.toast = Ui.Label(safe, "", 42, Ui.Cream, TextAnchor.MiddleCenter);
            Ui.Place(hud.toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000f, 140f), new Vector2(0f, -190f));
            hud.toast.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
            return hud;
        }

        public void SetVisible(bool visible)
        {
            canvas.gameObject.SetActive(visible);
            if (!visible) Joystick.Release();
        }

        public void Show(Stage stage)
        {
            title.text = stage.Level.Def.Name;
            pearls.text = $"Pearls {stage.PearlsCollected}/{stage.PearlsTotal}";
            timer.text = Progress.FormatTime(stage.Seconds);
        }

        public void Toast(string text, float seconds)
        {
            toast.text = text;
            toastUntil = Time.time + seconds;
        }

        /// <summary>A quick white flash when you fall or get hurt.</summary>
        public void Flash() => flash.color = new Color(1f, 1f, 1f, 0.6f);

        void Update()
        {
            if (toast.text.Length > 0 && Time.time > toastUntil) toast.text = "";
            if (flash.color.a > 0f) flash.color = new Color(1f, 1f, 1f, Mathf.MoveTowards(flash.color.a, 0f, Time.deltaTime * 1.5f));
        }
    }

    /// <summary>Island select: every level with its best pearls and time. Later islands open as you clear earlier ones.</summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        public Action<int> Chosen;
        RectTransform list;
        Text total;

        public static MenuScreen Create()
        {
            var canvas = Ui.MakeCanvas("Menu", 20);
            var m = canvas.gameObject.AddComponent<MenuScreen>();
            var dim = Ui.Solid(canvas.transform, new Color(0.04f, 0.1f, 0.2f, 0.45f), "Dim");
            Ui.Stretch(dim.rectTransform);
            var safe = Ui.SafeRoot(canvas);

            var logo = Ui.Label(safe, "WILDTIDE", 130, Ui.Cream, TextAnchor.MiddleLeft);
            Ui.Place(logo.rectTransform, new Vector2(0f, 1f), new Vector2(900f, 170f), new Vector2(90f, -90f));
            logo.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(6f, -6f);
            var tag = Ui.Label(safe, "Hop the islands. Gather the pearls. Find the golden shell.", 40, Ui.SeaFoam, TextAnchor.MiddleLeft);
            Ui.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(760f, 120f), new Vector2(96f, -270f));
            tag.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
            m.total = Ui.Label(safe, "", 40, Ui.Gold, TextAnchor.MiddleLeft);
            Ui.Place(m.total.rectTransform, new Vector2(0f, 0f), new Vector2(700f, 80f), new Vector2(96f, 70f));
            m.total.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);

            var panel = Ui.Panel(safe, Ui.Navy, "Islands");
            panel.rectTransform.anchorMin = new Vector2(0.52f, 0.06f);
            panel.rectTransform.anchorMax = new Vector2(0.97f, 0.94f);
            panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;
            m.list = Ui.Node("List", panel.transform);
            Ui.Stretch(m.list);
            Ui.Column(m.list, 18f, 30);
            return m;
        }

        public bool Open => gameObject.activeSelf;

        public void Show(Progress progress)
        {
            gameObject.SetActive(true);
            Ui.Clear(list);
            for (int i = 0; i < Levels.All.Count; i++)
            {
                int index = i;
                var def = Levels.All[i];
                var record = progress.Get(def.Id);
                bool open = progress.IsUnlocked(i);
                string detail = !open ? "Locked"
                    : record != null && record.Cleared ? $"Pearls {record.BestPearls}   {Progress.FormatTime(record.BestTime)}"
                    : "New!";
                var b = Ui.MakeButton(list, $"{i + 1}. {def.Name}", open ? Ui.SeaFoam : Ui.NavyLight, () => Chosen?.Invoke(index), 42);
                b.interactable = open;
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(30f, 0f);
                var right = Ui.Label(b.transform, detail, 34, open ? Ui.Navy : Ui.Cream, TextAnchor.MiddleRight, FontStyle.Normal);
                Ui.Stretch(right.rectTransform, 12f);
                right.rectTransform.offsetMax = new Vector2(-30f, -12f);
            }
            total.text = $"Pearls found: {progress.TotalPearls()}";
        }

        public void Hide() => gameObject.SetActive(false);
    }

    /// <summary>Shown on reaching the golden shell.</summary>
    public sealed class ClearedPanel : MonoBehaviour
    {
        public Action Next, Menu;
        Text heading, body;
        Button next;

        public static ClearedPanel Create()
        {
            var canvas = Ui.MakeCanvas("Cleared", 30);
            var c = canvas.gameObject.AddComponent<ClearedPanel>();
            var dim = Ui.Solid(canvas.transform, new Color(0f, 0f, 0f, 0.4f), "Dim");
            Ui.Stretch(dim.rectTransform);
            var safe = Ui.SafeRoot(canvas);
            var panel = Ui.Panel(safe, Ui.Navy, "Panel");
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000f, 640f), Vector2.zero);
            c.heading = Ui.Label(panel.transform, "", 72, Ui.Gold, TextAnchor.MiddleCenter);
            Ui.Place(c.heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(940f, 120f), new Vector2(0f, -40f));
            c.body = Ui.Label(panel.transform, "", 46, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Normal);
            Ui.Place(c.body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900f, 240f), new Vector2(0f, 30f));
            var menu = Ui.MakeButton(panel.transform, "Islands", Ui.NavyLight, () => c.Menu?.Invoke(), 44, Ui.Cream);
            Ui.Place((RectTransform)menu.transform, new Vector2(0f, 0f), new Vector2(400f, 120f), new Vector2(60f, 50f));
            c.next = Ui.MakeButton(panel.transform, "Next island", Ui.SeaFoam, () => c.Next?.Invoke(), 44);
            Ui.Place((RectTransform)c.next.transform, new Vector2(1f, 0f), new Vector2(400f, 120f), new Vector2(-60f, 50f));
            canvas.gameObject.SetActive(false);
            return c;
        }

        public void Show(StageResult r, bool bestTime, bool hasNext)
        {
            heading.text = r.Level.Name + " cleared!";
            body.text = $"Pearls  {r.Pearls} / {r.PearlsTotal}\n"
                        + $"Time  {Progress.FormatTime(r.Seconds)}{(bestTime ? "  (best!)" : "")}\n"
                        + $"Splashes  {r.Falls}";
            next.gameObject.SetActive(hasNext);
            Ui.SetText(next, "Next island");
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
