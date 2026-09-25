using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wildtide.Core;
using Wildtide.UI;

namespace Wildtide
{
    /// <summary>
    /// Runs one wild battle: a small 3D stage far from the overworld with its own camera (lower, behind the
    /// player's creature, per the battle-stage prompts), driven by the engine-free <see cref="Battle"/> rules.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        static readonly Vector3 StageOrigin = new Vector3(0f, 0f, 1000f);
        static readonly Vector3 PlayerSpot = new Vector3(-1.6f, 0f, 0.8f);
        static readonly Vector3 WildSpot = new Vector3(1.8f, 0f, 5.2f);

        public event Action<BattleOutcome> Finished;
        public bool Running { get; private set; }

        Camera cam;
        Transform stage;
        BattleScreen ui;
        Battle battle;
        SaveData save;
        IRng rng;
        GameObject playerView, wildView, lanternView;

        public static BattleController Create(BattleScreen ui)
        {
            var go = new GameObject("Battle");
            var bc = go.AddComponent<BattleController>();
            bc.ui = ui;
            bc.BuildStage();
            ui.MovePicked = slot => bc.Run(bc.battle.ChooseMove(slot));
            ui.LanternPicked = id => bc.Run(bc.battle.ChooseLantern(id), throwLantern: true);
            ui.TeamPicked = index => bc.Run(bc.battle.ChooseSwitch(index));
            ui.RunPicked = () => bc.Run(bc.battle.ChooseRun());
            return bc;
        }

        void BuildStage()
        {
            stage = new GameObject("Battle Stage").transform;
            stage.SetParent(transform, false);
            stage.position = StageOrigin;
            // Meadow arena: a grassy disc, two worn circles for the creatures, a ring of trees behind.
            Shapes.Make(PrimitiveType.Cylinder, stage, new Vector3(0f, -0.05f, 4f), new Vector3(40f, 0.05f, 40f), Materials.Hex(0x7CC35AFF), name: "Ground");
            Shapes.Make(PrimitiveType.Cylinder, stage, PlayerSpot + Vector3.up * 0.01f, new Vector3(3.2f, 0.01f, 3.2f), Materials.Hex(0xB9D98AFF), name: "Player Spot");
            Shapes.Make(PrimitiveType.Cylinder, stage, WildSpot + Vector3.up * 0.01f, new Vector3(3.6f, 0.01f, 3.6f), Materials.Hex(0xB9D98AFF), name: "Wild Spot");
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-70f, 70f, i / 8f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 16f, 0f, 4f + Mathf.Cos(a) * 14f);
                Shapes.Make(PrimitiveType.Cylinder, stage, p + Vector3.up * 1.2f, new Vector3(0.6f, 1.2f, 0.6f), Materials.Hex(0x8A5A3CFF), name: "Trunk");
                Shapes.Make(PrimitiveType.Sphere, stage, p + Vector3.up * 3.4f, new Vector3(3.6f, 3f, 3.6f), Materials.Hex(0x3F9E4DFF), name: "Canopy");
            }
            for (int i = 0; i < 30; i++)
            {
                var p = new Vector3(UnityEngine.Random.Range(-9f, 9f), 0.35f, UnityEngine.Random.Range(8f, 12f));
                Shapes.Make(PrimitiveType.Cube, stage, p, new Vector3(0.3f, 0.7f, 0.3f), Materials.Hex(0x4E9A3AFF), name: "Tuft")
                    .transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 90f), UnityEngine.Random.Range(-10f, 10f));
            }

            var camGo = new GameObject("Battle Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(stage, false);
            camGo.transform.localPosition = new Vector3(-3.2f, 2.4f, -4.2f);
            camGo.transform.LookAt(stage.TransformPoint(new Vector3(0.9f, 0.9f, 3.4f)));
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Materials.Hex(0xA8DDF5FF);
            cam.enabled = false;
        }

        public void Begin(SaveData data, Creature wild, IRng random)
        {
            save = data;
            rng = random;
            battle = new Battle(save, wild, rng);
            Running = true;
            cam.enabled = true;

            wildView = SpawnView(wild, WildSpot, faceCamera: true);
            playerView = null;
            ui.Open(battle.Player.Creature, wild);
            ui.SetContext(battle.Player.Creature, save);
            Run(battle.Start());
        }

        GameObject SpawnView(Creature c, Vector3 spot, bool faceCamera)
        {
            var v = CreatureView.Build(c.Species, stage, maxScale: 2f);
            v.transform.localPosition = spot;
            var look = faceCamera ? cam.transform.position - v.transform.position : stage.TransformPoint(WildSpot) - v.transform.position;
            look.y = 0f;
            v.transform.rotation = Quaternion.LookRotation(look);
            v.AddComponent<Bob>();
            return v;
        }

        void Run(List<BattleEvent> events, bool throwLantern = false)
        {
            StartCoroutine(Play(events, throwLantern));
        }

        IEnumerator Play(List<BattleEvent> events, bool throwLantern)
        {
            bool ended = false;
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case BattleEventKind.Message:
                    case BattleEventKind.StatChange:
                        yield return ui.Say(e.Text);
                        // Level-ups and new moves show up on the plate as they're announced.
                        ui.Bind(true, battle.Player.Creature, instantHp: false);
                        if (e.Text.StartsWith("You threw") && throwLantern) yield return ThrowLantern();
                        break;

                    case BattleEventKind.SwitchIn:
                        if (playerView != null) Destroy(playerView);
                        playerView = SpawnView(battle.Player.Creature, PlayerSpot, faceCamera: false);
                        ui.BindPlayer(battle.Player.Creature);
                        ui.SetContext(battle.Player.Creature, save);
                        yield return ui.Say(e.Text, 1.4f);
                        break;

                    case BattleEventKind.Damage:
                        var target = e.PlayerSide ? playerView : wildView;
                        var creature = e.PlayerSide ? battle.Player.Creature : battle.Wild.Creature;
                        StartCoroutine(Flash(target));
                        yield return ui.AnimateHp(e.PlayerSide, e.HpAfter, creature.MaxHp);
                        break;

                    case BattleEventKind.Faint:
                        yield return Faint(e.PlayerSide ? playerView : wildView);
                        yield return ui.Say(e.Text);
                        break;

                    case BattleEventKind.CaptureShake:
                        yield return Wobble();
                        break;

                    case BattleEventKind.CaptureResult:
                        if (e.Caught)
                        {
                            if (lanternView != null) lanternView.transform.localScale *= 1.25f;
                        }
                        else
                        {
                            if (lanternView != null) Destroy(lanternView);
                            if (wildView != null) wildView.SetActive(true);
                        }
                        yield return ui.Say(e.Text);
                        break;

                    case BattleEventKind.End:
                        ended = true;
                        break;
                }
            }

            if (ended) End();
            else if (battle.MustSwitch) ui.ShowTeam(forced: true);
            else ui.ShowCommands();
        }

        IEnumerator ThrowLantern()
        {
            lanternView = Shapes.Make(PrimitiveType.Sphere, stage, PlayerSpot + new Vector3(0f, 1f, 0f), Vector3.one * 0.45f, Materials.Hex(0xFFD36BFF), name: "Lantern");
            Vector3 from = lanternView.transform.localPosition, to = WildSpot + new Vector3(0f, 0.25f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
            {
                lanternView.transform.localPosition = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2.2f;
                yield return null;
            }
            lanternView.transform.localPosition = to;
            if (wildView != null) wildView.SetActive(false); // pulled inside by ribbons of light
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator Wobble()
        {
            if (lanternView == null) yield break;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                lanternView.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t / 0.6f * Mathf.PI * 2f) * 25f);
                yield return null;
            }
            lanternView.transform.localRotation = Quaternion.identity;
            yield return new WaitForSeconds(0.35f);
        }

        static IEnumerator Flash(GameObject target)
        {
            if (target == null) yield break;
            // Blink a few times, like a hit.
            for (int i = 0; i < 3; i++)
            {
                target.SetActive(false);
                yield return new WaitForSeconds(0.07f);
                if (target == null) yield break;
                target.SetActive(true);
                yield return new WaitForSeconds(0.07f);
            }
        }

        static IEnumerator Faint(GameObject view)
        {
            if (view == null) yield break;
            var start = view.transform.localPosition;
            var bob = view.GetComponent<Bob>();
            if (bob != null) Destroy(bob);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                view.transform.localPosition = start + Vector3.down * t * 1.5f;
                yield return null;
            }
            view.SetActive(false);
        }

        void End()
        {
            if (playerView != null) Destroy(playerView);
            if (wildView != null) Destroy(wildView);
            if (lanternView != null) Destroy(lanternView);
            cam.enabled = false;
            ui.Close();
            Running = false;
            Finished?.Invoke(battle.Outcome);
        }
    }
}
