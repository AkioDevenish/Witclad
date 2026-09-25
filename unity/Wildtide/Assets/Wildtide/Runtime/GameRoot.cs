using System;
using System.IO;
using UnityEngine;
using Wildtide.Core;
using Wildtide.UI;

namespace Wildtide
{
    /// <summary>
    /// Boots the game in any scene: builds the sea, light, camera, player and UI from code, then runs the loop of
    /// island select → play → cleared → next island. No scene setup required; press Play.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        Progress progress;
        PlayerController player;
        IsoCamera cam;
        Hud hud;
        MenuScreen menu;
        ClearedPanel cleared;
        Stage stage;
        int levelIndex;

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
            // Rafts move their colliders in Update; keep physics in step so the player never sinks into one.
            Physics.autoSyncTransforms = true;

            progress = SaveSystem.Load() ?? new Progress();
            BuildScene();

            // Open on the first island not yet cleared, behind the menu.
            levelIndex = 0;
            while (levelIndex + 1 < Levels.All.Count && progress.IsUnlocked(levelIndex + 1)) levelIndex++;
            LoadLevel(levelIndex);
            ShowMenu();
        }

        void BuildScene()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(58f, 20f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.75f);

            var sea = Shapes.Make(PrimitiveType.Plane, null, Vector3.zero, new Vector3(60f, 1f, 60f), Materials.Hex(0x3FA7D6FF), name: "Sea");
            sea.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            player = PlayerController.Create();
            cam = IsoCamera.Create();
            cam.Target = player.transform;
            player.CameraYaw = IsoCamera.Yaw;

            hud = Hud.Create();
            hud.MenuPressed = ShowMenu;
            player.Joystick = hud.Joystick;
            player.JumpButton = hud.Jump;

            menu = MenuScreen.Create();
            menu.Chosen = Play;

            cleared = ClearedPanel.Create();
            cleared.Menu = () => { cleared.Hide(); ShowMenu(); };
            cleared.Next = () => { cleared.Hide(); Play(levelIndex + 1); };
        }

        void LoadLevel(int index)
        {
            if (stage != null) Destroy(stage.gameObject);
            levelIndex = Mathf.Clamp(index, 0, Levels.All.Count - 1);
            stage = Stage.Create(Level.Parse(Levels.All[levelIndex]), player, cam);
            stage.Cleared += OnCleared;
            stage.Fell += hud.Flash;
            player.Teleport(stage.StartPosition);
            cam.Snap();
        }

        void Play(int index)
        {
            if (!progress.IsUnlocked(index)) return;
            LoadLevel(index);
            menu.Hide();
            cleared.Hide();
            hud.SetVisible(true);
            hud.Toast(stage.Level.Def.Hint, 5f);
            stage.Running = true;
            player.InputEnabled = true;
        }

        void ShowMenu()
        {
            if (stage != null) stage.Running = false;
            player.InputEnabled = false;
            hud.SetVisible(false);
            cleared.Hide();
            menu.Show(progress);
        }

        void OnCleared(StageResult r)
        {
            bool best = progress.RecordClear(r.Level.Id, r.Pearls, r.Seconds);
            SaveSystem.Write(progress);
            hud.SetVisible(false);
            cleared.Show(r, best, levelIndex + 1 < Levels.All.Count);
        }

        void Update()
        {
            if (stage != null && hud.isActiveAndEnabled) hud.Show(stage);
            // Android back button (and Esc in the editor) goes back to the island list.
            if (Input.GetKeyDown(KeyCode.Escape) && !menu.Open) ShowMenu();
        }
    }

    public static class SaveSystem
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "wildtide-islands.json");

        public static Progress Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var data = JsonUtility.FromJson<Progress>(File.ReadAllText(FilePath));
                if (data == null) return null;
                if (data.Records == null) data.Records = new System.Collections.Generic.List<LevelRecord>();
                data.Records.RemoveAll(r => r == null || Levels.IndexOf(r.Id) < 0); // islands renamed or removed
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save could not be read, starting fresh (old file kept as .bad): {e.Message}");
                try { File.Copy(FilePath, FilePath + ".bad", true); } catch (Exception) { }
                return null;
            }
        }

        public static void Write(Progress data)
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
        [UnityEditor.MenuItem("Wildtide/Delete Save (start over)")]
        static void DeleteSave()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            Debug.Log("Wildtide save deleted: " + FilePath);
        }
#endif
    }
}
