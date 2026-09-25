using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wildtide.Core;
using Wildtide.UI;

namespace Wildtide
{
    /// <summary>
    /// Boots the vertical slice in any scene: builds the world, player, cameras and UI from code, then runs the
    /// loop of starter pick → overworld → wild battle → back. No scene setup required; press Play.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        public SaveData Save { get; private set; }
        readonly IRng rng = new SystemRng();
        readonly EncounterRoller encounters = new EncounterRoller();

        World world;
        PlayerController player;
        FollowCamera overworldCamera;
        CreatureFollower follower;
        string followerSpecies;
        Hud hud;
        BattleController battle;
        Light sun;
        bool wasInHearth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null || FindAnyObjectByType<GameRoot>() != null) return;
            new GameObject("Wildtide").AddComponent<GameRoot>();
        }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = true;

            Save = SaveSystem.Load() ?? SaveData.NewGame();
            BuildScene();

            if (Save.HasStarter) EnterOverworld();
            else ShowStarterSelect();
        }

        void BuildScene()
        {
            // Warm key light; the toon shader turns it into two clean bands with a cool rim.
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.75f);

            world = World.Build();
            var spawn = Save.HasPosition ? new Vector3(Save.PosX, Save.PosY, Save.PosZ) : world.NewGameSpawn;
            player = PlayerController.Create(spawn);
            overworldCamera = FollowCamera.Create();
            overworldCamera.Target = player.transform;
            overworldCamera.Snap();

            hud = Hud.Create();
            hud.Party.Bind(Save);
            hud.Party.Changed = () => { RefreshFollower(); Persist(); };
            player.Joystick = hud.Joystick;

            battle = BattleController.Create(BattleScreen.Create());
            battle.Finished += OnBattleFinished;
        }

        // ---- flow ---------------------------------------------------------------------------------

        void ShowStarterSelect()
        {
            hud.SetVisible(false);
            player.InputEnabled = false;
            var screen = StarterScreen.Create();
            screen.Chosen = id =>
            {
                Destroy(screen.gameObject);
                var starter = Creature.Create(id, 5);
                Save.AddCaught(starter);
                Persist();
                EnterOverworld();
                hud.Dialog.Show($"Oma Rell: {starter.Name} has taken a shine to you! The tall grass north of town is full of wild creatures. Take these Tin Lanterns and go make some friends.");
            };
        }

        void EnterOverworld()
        {
            hud.SetVisible(true);
            player.InputEnabled = true;
            overworldCamera.GetComponent<Camera>().enabled = true;
            RefreshFollower();
            encounters.StartGrace();
            wasInHearth = world.HearthDoor.Contains(player.transform.position);
        }

        void Update()
        {
            if (battle.Running || !player.InputEnabled) return;
            bool blocked = hud.Blocking;
            player.enabled = !blocked;
            if (blocked) return;

            bool inHearth = world.HearthDoor.Contains(player.transform.position);
            if (inHearth && !wasInHearth)
            {
                Save.HealParty();
                Persist();
                hud.Dialog.Show("Welcome to the Hearth House! Your team is rested and ready. We hope to see you again!");
            }
            wasInHearth = inHearth;

            if (encounters.Step(world, player.transform.position, player.MovedThisFrame, rng)) StartWildBattle();
        }

        void StartWildBattle()
        {
            if (Save.Lead == null) return; // everyone fainted: walk to the Hearth House
            var slot = Database.Roll(Database.WindmillMeadows, rng);
            var wild = Creature.Create(slot.SpeciesId, rng.Range(slot.MinLevel, slot.MaxLevel + 1));
            hud.SetVisible(false);
            player.InputEnabled = false;
            overworldCamera.GetComponent<Camera>().enabled = false;
            if (follower != null) follower.gameObject.SetActive(false);
            battle.Begin(Save, wild, rng);
        }

        void OnBattleFinished(BattleOutcome outcome)
        {
            if (follower != null) follower.gameObject.SetActive(true);
            if (outcome == BattleOutcome.Lost)
            {
                Save.HealParty();
                player.Teleport(world.HearthSpawn);
                overworldCamera.Snap();
            }
            EnterOverworld();
            var lines = new List<string>();
            if (outcome == BattleOutcome.Lost) lines.Add("You rushed back to the Hearth House. Your team has been restored.");
            foreach (var c in Save.Party)
            {
                while (c.CanEvolve)
                {
                    string before = c.Evolve();
                    lines.Add($"What? {before} is evolving!\n...{before} became {c.Species.Name}!");
                }
            }
            RefreshFollower();
            Persist();
            ShowLines(lines, 0);
        }

        void ShowLines(List<string> lines, int index)
        {
            if (index >= lines.Count) return;
            hud.Dialog.Show(lines[index], () => ShowLines(lines, index + 1));
        }

        void RefreshFollower()
        {
            var lead = Save.Party.Count > 0 ? Save.Party[0] : null;
            if (lead == null)
            {
                if (follower != null) Destroy(follower.gameObject);
                follower = null;
                followerSpecies = null;
                return;
            }
            if (follower != null && followerSpecies == lead.SpeciesId) return;
            Vector3 at = follower != null ? follower.transform.position : player.transform.position - Vector3.forward * 1.5f;
            if (follower != null) Destroy(follower.gameObject);
            var view = CreatureView.Build(lead.Species, null, maxScale: 1.2f);
            view.transform.position = at;
            follower = view.AddComponent<CreatureFollower>();
            follower.Target = player.transform;
            followerSpecies = lead.SpeciesId;
        }

        // ---- saving -------------------------------------------------------------------------------

        void Persist()
        {
            if (Save == null || player == null) return;
            var p = player.transform.position;
            Save.PosX = p.x;
            Save.PosY = p.y;
            Save.PosZ = p.z;
            Save.HasPosition = Save.HasStarter;
            SaveSystem.Write(Save);
        }

        // Phones kill backgrounded apps without warning, so save whenever we lose focus.
        void OnApplicationPause(bool paused)
        {
            if (paused && !battle.Running) Persist();
        }

        void OnApplicationQuit()
        {
            if (!battle.Running) Persist();
        }
    }

    public static class SaveSystem
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "wildtide-save.json");

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                // Drop anything that no longer exists in the database (renamed or removed content).
                data.Party.RemoveAll(c => c == null || !Database.Species.ContainsKey(c.SpeciesId));
                data.Storage.RemoveAll(c => c == null || !Database.Species.ContainsKey(c.SpeciesId));
                foreach (var c in data.Party) c.Moves.RemoveAll(m => !Database.Moves.ContainsKey(m.MoveId));
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save could not be read, starting fresh (old file kept as .bad): {e.Message}");
                try { File.Copy(FilePath, FilePath + ".bad", true); } catch (Exception) { }
                return null;
            }
        }

        public static void Write(SaveData data)
        {
            try
            {
                // Write then swap, so a crash mid-write never corrupts the save.
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError("Saving failed: " + e.Message);
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Wildtide/Delete Save (start a new game)")]
        static void DeleteSave()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            Debug.Log("Wildtide save deleted: " + FilePath);
        }
#endif
    }
}
