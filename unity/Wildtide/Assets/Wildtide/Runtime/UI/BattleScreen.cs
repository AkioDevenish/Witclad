using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wildtide.Core;

namespace Wildtide.UI
{
    /// <summary>
    /// Battle HUD from the battle-screen mockup: name plates with level and health, a message box, a command
    /// bar (Fight, Bag, Team, Run) and a four-move menu with element-coloured buttons.
    /// </summary>
    public sealed class BattleScreen : MonoBehaviour
    {
        public Action<int> MovePicked;
        public Action<string> LanternPicked;
        public Action<int> TeamPicked;
        public Action RunPicked;

        Canvas canvas;
        RectTransform safe, commands, submenu, submenuGrid;
        Plate wildPlate, playerPlate;
        Text message;
        Button backButton;
        bool advance;

        sealed class Plate
        {
            public Text Name, Level, HpText;
            public Bar Hp, Xp;
        }

        public static BattleScreen Create()
        {
            var canvas = Ui.MakeCanvas("Battle UI", 20);
            var s = canvas.gameObject.AddComponent<BattleScreen>();
            s.canvas = canvas;
            s.safe = Ui.SafeRoot(canvas);
            s.Build();
            canvas.gameObject.SetActive(false);
            return s;
        }

        void Build()
        {
            wildPlate = MakePlate(new Vector2(0f, 1f), new Vector2(40f, -40f), false);
            playerPlate = MakePlate(new Vector2(1f, 0.36f), new Vector2(-40f, 20f), true);

            var box = Ui.Panel(safe, Ui.Navy, "Message");
            box.rectTransform.anchorMin = new Vector2(0f, 0f);
            box.rectTransform.anchorMax = new Vector2(0.56f, 0.3f);
            box.rectTransform.offsetMin = new Vector2(30f, 30f);
            box.rectTransform.offsetMax = new Vector2(-10f, -10f);
            message = Ui.Label(box.transform, "", 50, Ui.Cream, TextAnchor.MiddleLeft, FontStyle.Normal);
            Ui.Stretch(message.rectTransform, 44f);
            // Tapping the message box skips ahead.
            box.gameObject.AddComponent<Button>().onClick.AddListener(() => advance = true);

            commands = Ui.Node("Commands", safe);
            commands.anchorMin = new Vector2(0.56f, 0f);
            commands.anchorMax = new Vector2(1f, 0.3f);
            commands.offsetMin = new Vector2(10f, 30f);
            commands.offsetMax = new Vector2(-30f, -10f);
            var grid = Ui.Node("Grid", commands);
            Ui.Stretch(grid);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            g.spacing = new Vector2(18f, 18f);
            g.cellSize = new Vector2(370f, 125f);
            g.childAlignment = TextAnchor.MiddleCenter;
            Ui.MakeButton(grid, "Fight", Materials.Hex(0xF07A5AFF), () => ShowMoves());
            Ui.MakeButton(grid, "Bag", Ui.Gold, () => ShowBag());
            Ui.MakeButton(grid, "Team", Ui.SeaFoam, () => ShowTeam(false));
            Ui.MakeButton(grid, "Run", Materials.Hex(0x9FB4D9FF), () => { HideAll(); RunPicked?.Invoke(); });

            submenu = Ui.Node("Submenu", safe);
            submenu.anchorMin = new Vector2(0.3f, 0f);
            submenu.anchorMax = new Vector2(1f, 0.62f);
            submenu.offsetMin = new Vector2(10f, 30f);
            submenu.offsetMax = new Vector2(-30f, -10f);
            var bg = Ui.Panel(submenu, Ui.Navy, "Background");
            Ui.Stretch(bg.rectTransform);
            submenuGrid = Ui.Node("Options", submenu);
            submenuGrid.anchorMin = Vector2.zero;
            submenuGrid.anchorMax = Vector2.one;
            submenuGrid.offsetMin = new Vector2(30f, 30f);
            submenuGrid.offsetMax = new Vector2(-30f, -120f);
            var sg = submenuGrid.gameObject.AddComponent<GridLayoutGroup>();
            sg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            sg.constraintCount = 2;
            sg.spacing = new Vector2(20f, 20f);
            sg.cellSize = new Vector2(560f, 130f);
            sg.childAlignment = TextAnchor.UpperCenter;
            backButton = Ui.MakeButton(submenu, "Back", Ui.Cream, () => ShowCommands(), 38);
            Ui.Place((RectTransform)backButton.transform, new Vector2(1f, 1f), new Vector2(220f, 80f), new Vector2(-24f, -20f));

            HideAll();
        }

        Plate MakePlate(Vector2 anchor, Vector2 offset, bool player)
        {
            var panel = Ui.Panel(safe, Ui.Navy, player ? "Player Plate" : "Wild Plate");
            Ui.Place(panel.rectTransform, anchor, new Vector2(700f, player ? 210f : 170f), offset);
            var p = new Plate();
            p.Name = Ui.Label(panel.transform, "", 50, Ui.Cream);
            Ui.Place(p.Name.rectTransform, new Vector2(0f, 1f), new Vector2(460f, 70f), new Vector2(34f, -16f));
            p.Level = Ui.Label(panel.transform, "", 44, Ui.Gold, TextAnchor.MiddleRight);
            Ui.Place(p.Level.rectTransform, new Vector2(1f, 1f), new Vector2(200f, 70f), new Vector2(-34f, -16f));
            p.Hp = Bar.Create(panel.transform, Ui.HpGreen);
            Ui.Place((RectTransform)p.Hp.transform, new Vector2(1f, 1f), new Vector2(560f, 36f), new Vector2(-34f, -96f));
            var hpTag = Ui.Label(panel.transform, "HP", 32, Ui.Gold);
            Ui.Place(hpTag.rectTransform, new Vector2(0f, 1f), new Vector2(80f, 36f), new Vector2(34f, -96f));
            if (player)
            {
                p.HpText = Ui.Label(panel.transform, "", 36, Ui.Cream, TextAnchor.MiddleRight);
                Ui.Place(p.HpText.rectTransform, new Vector2(1f, 1f), new Vector2(300f, 44f), new Vector2(-34f, -138f));
                p.Xp = Bar.Create(panel.transform, Materials.Hex(0x6FB8FFFF), false);
                Ui.Place((RectTransform)p.Xp.transform, new Vector2(0f, 0f), new Vector2(360f, 20f), new Vector2(34f, 24f));
            }
            return p;
        }

        // ---- binding -----------------------------------------------------------------------------

        public void Open(Creature player, Creature wild)
        {
            canvas.gameObject.SetActive(true);
            message.text = "";
            Bind(playerPlate, player, true);
            Bind(wildPlate, wild, true);
            HideAll();
        }

        public void Close() => canvas.gameObject.SetActive(false);

        public void BindPlayer(Creature c) => Bind(playerPlate, c, true);

        /// <summary>Refresh names and levels; the HP bar only jumps if instant.</summary>
        public void Bind(bool playerSide, Creature c, bool instantHp) => Bind(playerSide ? playerPlate : wildPlate, c, instantHp);

        void Bind(Plate p, Creature c, bool instantHp)
        {
            p.Name.text = c.Name;
            p.Level.text = "Lv " + c.Level;
            p.Hp.Set((float)c.Hp / c.MaxHp, instantHp);
            if (p.HpText != null && instantHp) p.HpText.text = $"{c.Hp} / {c.MaxHp}";
            if (p.Xp != null)
            {
                int from = Creature.XpForLevel(c.Level), to = Creature.XpForLevel(c.Level + 1);
                p.Xp.Set(to > from ? (float)(c.Xp - from) / (to - from) : 1f, false);
            }
        }

        public IEnumerator AnimateHp(bool playerSide, int hpAfter, int maxHp)
        {
            var p = playerSide ? playerPlate : wildPlate;
            p.Hp.Set((float)hpAfter / Mathf.Max(1, maxHp), false);
            if (p.HpText != null) p.HpText.text = $"{hpAfter} / {maxHp}";
            while (p.Hp.Animating) yield return null;
        }

        /// <summary>Shows a line, then waits for a tap (or moves on by itself after a few seconds).</summary>
        public IEnumerator Say(string text, float autoAdvance = 2.6f)
        {
            message.text = text;
            advance = false;
            yield return null; // don't let the tap that picked a command also skip this line
            float t = 0f;
            while (!advance && t < autoAdvance)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) advance = true;
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ---- menus -------------------------------------------------------------------------------

        Creature player;
        SaveData save;
        bool forcedSwitch;

        public void SetContext(Creature activePlayer, SaveData data)
        {
            player = activePlayer;
            save = data;
        }

        public void ShowCommands()
        {
            HideAll();
            message.text = $"What will {player.Name} do?";
            commands.gameObject.SetActive(true);
        }

        void ShowMoves()
        {
            OpenSubmenu(true);
            for (int i = 0; i < player.Moves.Count; i++)
            {
                int slot = i;
                var m = player.Moves[i];
                var b = Ui.MakeButton(submenuGrid, $"{m.Data.Name}\n<size=30>{m.Data.Element}  ·  {m.Pp}/{m.Data.MaxPp}</size>",
                    Materials.ForElement(m.Data.Element), () => { HideAll(); MovePicked?.Invoke(slot); }, 40);
                b.interactable = m.Pp > 0;
            }
        }

        void ShowBag()
        {
            OpenSubmenu(true);
            foreach (var lantern in Database.Lanterns.Values)
            {
                int count = save.LanternCount(lantern.Id);
                if (count <= 0) continue;
                string id = lantern.Id;
                Ui.MakeButton(submenuGrid, $"{lantern.Name}  x{count}", Ui.Gold, () => { HideAll(); LanternPicked?.Invoke(id); }, 40);
            }
            if (submenuGrid.childCount == 0)
                Ui.MakeButton(submenuGrid, "No lanterns left!", Ui.Cream, null, 40).interactable = false;
        }

        public void ShowTeam(bool forced)
        {
            forcedSwitch = forced;
            OpenSubmenu(!forced);
            if (forced) message.text = "Who will you send out next?";
            for (int i = 0; i < save.Party.Count; i++)
            {
                int index = i;
                var c = save.Party[i];
                string label = $"{c.Name}  Lv {c.Level}\n<size=30>HP {c.Hp}/{c.MaxHp}{(c == player ? "  ·  in battle" : "")}</size>";
                var b = Ui.MakeButton(submenuGrid, label, c.Fainted ? Materials.Hex(0x777777FF) : Ui.SeaFoam,
                    () => { HideAll(); TeamPicked?.Invoke(index); }, 40);
                b.interactable = !c.Fainted && c != player;
            }
        }

        void OpenSubmenu(bool canGoBack)
        {
            HideAll();
            Ui.Clear(submenuGrid);
            submenu.gameObject.SetActive(true);
            backButton.gameObject.SetActive(canGoBack);
        }

        void HideAll()
        {
            commands.gameObject.SetActive(false);
            submenu.gameObject.SetActive(false);
        }

        void Update()
        {
            // Android back button / Escape closes a submenu.
            if (Input.GetKeyDown(KeyCode.Escape) && submenu.gameObject.activeSelf && !forcedSwitch) ShowCommands();
        }
    }
}
