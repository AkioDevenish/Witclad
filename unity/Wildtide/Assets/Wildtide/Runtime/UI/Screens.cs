using System;
using UnityEngine;
using UnityEngine.UI;
using Wildtide.Core;

namespace Wildtide.UI
{
    /// <summary>Overworld HUD: joystick, Team button and the dialog box.</summary>
    public sealed class Hud : MonoBehaviour
    {
        public VirtualJoystick Joystick { get; private set; }
        public Dialog Dialog { get; private set; }
        public PartyPanel Party { get; private set; }
        Canvas canvas;
        Text toast;
        float toastUntil;

        public static Hud Create()
        {
            var canvas = Ui.MakeCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<Hud>();
            hud.canvas = canvas;
            var safe = Ui.SafeRoot(canvas);
            hud.Joystick = VirtualJoystick.Create(safe);

            var team = Ui.MakeButton(safe, "Team", Ui.SeaFoam, () => hud.Party.Show(), 44);
            Ui.Place((RectTransform)team.transform, new Vector2(1f, 1f), new Vector2(240f, 110f), new Vector2(-36f, -30f));

            hud.toast = Ui.Label(safe, "", 40, Ui.Cream, TextAnchor.MiddleCenter);
            Ui.Place(hud.toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 80f), new Vector2(0f, -40f));
            hud.toast.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

            hud.Party = PartyPanel.Create(safe);
            hud.Dialog = Dialog.Create(safe);
            return hud;
        }

        public bool Blocking => Dialog.Open || Party.Open;

        public void SetVisible(bool visible)
        {
            canvas.gameObject.SetActive(visible);
            if (!visible) Joystick.Release();
        }

        public void Toast(string text, float seconds = 2f)
        {
            toast.text = text;
            toastUntil = Time.time + seconds;
        }

        void Update()
        {
            if (toast.text.Length > 0 && Time.time > toastUntil) toast.text = "";
            Joystick.gameObject.SetActive(!Blocking);
            if (Input.GetKeyDown(KeyCode.Escape) && Party.Open) Party.Hide();
        }
    }

    /// <summary>The team list. Tap a creature to make it your lead.</summary>
    public sealed class PartyPanel : MonoBehaviour
    {
        public Action Changed;
        SaveData save;
        RectTransform list;

        public static PartyPanel Create(RectTransform parent)
        {
            var bg = Ui.Solid(parent, new Color(0f, 0f, 0f, 0.55f), "Team");
            Ui.Stretch(bg.rectTransform);
            var p = bg.gameObject.AddComponent<PartyPanel>();
            var panel = Ui.Panel(bg.transform, Ui.Navy, "Panel");
            panel.rectTransform.anchorMin = new Vector2(0.12f, 0.06f);
            panel.rectTransform.anchorMax = new Vector2(0.88f, 0.94f);
            panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;
            var title = Ui.Label(panel.transform, "Your team  ·  tap to lead", 54, Ui.Gold);
            Ui.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1000f, 90f), new Vector2(50f, -30f));
            var close = Ui.MakeButton(panel.transform, "Close", Ui.Cream, p.Hide, 40);
            Ui.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(220f, 90f), new Vector2(-30f, -30f));
            p.list = Ui.Node("List", panel.transform);
            p.list.anchorMin = Vector2.zero;
            p.list.anchorMax = Vector2.one;
            p.list.offsetMin = new Vector2(40f, 40f);
            p.list.offsetMax = new Vector2(-40f, -140f);
            Ui.Grid(p.list, new Vector2(620f, 200f), new Vector2(24f, 20f), 2);
            bg.gameObject.SetActive(false);
            return p;
        }

        public bool Open => gameObject.activeSelf;

        public void Bind(SaveData data) => save = data;

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Rebuild();
        }

        public void Hide() => gameObject.SetActive(false);

        void Rebuild()
        {
            Ui.Clear(list);
            for (int i = 0; i < save.Party.Count; i++)
            {
                int index = i;
                var c = save.Party[i];
                var card = Ui.MakeButton(list, "", i == 0 ? Ui.Gold : Ui.NavyLight, () => MakeLead(index));
                var name = Ui.Label(card.transform, $"{c.Name}   Lv {c.Level}", 44, i == 0 ? Ui.Navy : Ui.Cream);
                Ui.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(560f, 60f), new Vector2(30f, -20f));
                var element = c.Species.SecondaryElement.HasValue ? $"{c.Species.Element} / {c.Species.SecondaryElement}" : c.Species.Element.ToString();
                var info = Ui.Label(card.transform, $"{element}   HP {c.Hp}/{c.MaxHp}", 32, i == 0 ? Ui.Navy : Ui.SeaFoam, TextAnchor.MiddleLeft, FontStyle.Normal);
                Ui.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(560f, 50f), new Vector2(30f, -82f));
                var bar = Bar.Create(card.transform, Ui.HpGreen);
                Ui.Place((RectTransform)bar.transform, new Vector2(0f, 0f), new Vector2(560f, 30f), new Vector2(30f, 24f));
                bar.Set((float)c.Hp / c.MaxHp, true);
            }
            if (save.Storage.Count > 0)
            {
                var more = Ui.Label(list, $"+{save.Storage.Count} in storage", 34, Ui.SeaFoam, TextAnchor.MiddleCenter);
                more.raycastTarget = false;
            }
        }

        void MakeLead(int index)
        {
            if (index == 0) return;
            var c = save.Party[index];
            save.Party.RemoveAt(index);
            save.Party.Insert(0, c);
            Rebuild();
            Changed?.Invoke();
        }
    }

    /// <summary>New game: Archivist Oma Rell offers the three starters.</summary>
    public sealed class StarterScreen : MonoBehaviour
    {
        public Action<string> Chosen;

        public static StarterScreen Create()
        {
            var canvas = Ui.MakeCanvas("Starter Select", 30);
            var s = canvas.gameObject.AddComponent<StarterScreen>();
            var bg = Ui.Solid(canvas.transform, Ui.Navy, "Background");
            Ui.Stretch(bg.rectTransform);
            var safe = Ui.SafeRoot(canvas);

            var title = Ui.Label(safe, "Choose your partner", 84, Ui.Gold, TextAnchor.MiddleCenter);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(1600f, 130f), new Vector2(0f, -40f));
            var sub = Ui.Label(safe, "\"Every great journey starts with one bond.\"  —  Archivist Oma Rell", 40, Ui.Cream,
                TextAnchor.MiddleCenter, FontStyle.Italic);
            Ui.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(1600f, 70f), new Vector2(0f, -170f));

            var row = Ui.Node("Starters", safe);
            row.anchorMin = new Vector2(0.04f, 0.06f);
            row.anchorMax = new Vector2(0.96f, 0.76f);
            row.offsetMin = row.offsetMax = Vector2.zero;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 36f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;

            foreach (var id in Database.Starters) s.Card(row, Database.GetSpecies(id));
            return s;
        }

        void Card(RectTransform parent, Species sp)
        {
            var color = Materials.ForElement(sp.Element);
            var card = Ui.MakeButton(parent, "", Ui.NavyLight, () => Chosen?.Invoke(sp.Id));
            var art = Resources.Load<Texture2D>("Concepts/" + sp.ArtId);
            if (art != null)
            {
                var img = Ui.Node("Art", card.transform).gameObject.AddComponent<RawImage>();
                img.texture = art;
                img.raycastTarget = false;
                Ui.Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(420f, 420f), new Vector2(0f, -30f));
            }
            else
            {
                var blob = Ui.Panel(card.transform, color, "Placeholder");
                blob.raycastTarget = false;
                Ui.Place(blob.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, 300f), new Vector2(0f, -80f));
            }
            var name = Ui.Label(card.transform, sp.Name, 64, Ui.Cream, TextAnchor.MiddleCenter);
            Ui.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(520f, 90f), new Vector2(0f, 230f));
            var el = Ui.Label(card.transform, sp.Element.ToString().ToUpperInvariant(), 36, color, TextAnchor.MiddleCenter);
            Ui.Place(el.rectTransform, new Vector2(0.5f, 0f), new Vector2(520f, 50f), new Vector2(0f, 180f));
            var desc = Ui.Label(card.transform, sp.Description, 32, Ui.SeaFoam, TextAnchor.UpperCenter, FontStyle.Normal);
            Ui.Place(desc.rectTransform, new Vector2(0.5f, 0f), new Vector2(500f, 150f), new Vector2(0f, 20f));
        }
    }
}
