using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wildtide.Core;

namespace Wildtide
{
    public struct StageResult
    {
        public LevelDef Level;
        public int Pearls, PearlsTotal, Falls;
        public float Seconds;
    }

    /// <summary>
    /// One island, built from a Core <see cref="Level"/>: columns rising out of the sea, pearls, checkpoints, crabs,
    /// rafts and the goal shell. Watches the player for pickups, hits, falls and the finish.
    /// </summary>
    public sealed class Stage : MonoBehaviour
    {
        public Level Level { get; private set; }
        public bool Running;
        public event Action<StageResult> Cleared;
        public event Action Fell;

        public int PearlsCollected { get; private set; }
        public int PearlsTotal => pearls.Count;
        public float Seconds { get; private set; }
        public int Falls { get; private set; }

        /// <summary>Surface of the sea; falling this far below it counts as a fall.</summary>
        const float SeaLevel = 0f;
        const float ColumnBottom = -4f;

        PlayerController player;
        IsoCamera cam;
        readonly List<Transform> pearls = new List<Transform>();
        readonly List<(Transform post, Renderer flag, Vector3 spawn)> checkpoints = new List<(Transform, Renderer, Vector3)>();
        readonly List<Crab> crabs = new List<Crab>();
        Transform goal;
        Vector3 respawn;
        bool dying, finished;

        public static Vector3 CellTop(Cell c, float steps) => new Vector3(c.X * Moves.Tile, steps * Moves.Step, c.Z * Moves.Tile);

        public Vector3 StartPosition => CellTop(Level.Start, Level.Column(Level.Start)) + Vector3.up * 0.05f;

        public static Stage Create(Level level, PlayerController player, IsoCamera cam)
        {
            var go = new GameObject("Stage " + level.Def.Name);
            var s = go.AddComponent<Stage>();
            s.Level = level;
            s.player = player;
            s.cam = cam;
            s.Build();
            s.respawn = s.StartPosition;
            player.Hurt += s.OnHurt;
            return s;
        }

        void OnDestroy()
        {
            if (player != null) player.Hurt -= OnHurt;
        }

        // ---- building -----------------------------------------------------------------------------

        static readonly Color Sand = Materials.Hex(0xF2DFA7FF);
        static readonly Color SandDark = Materials.Hex(0xE8D08FFF);
        static readonly Color Grass = Materials.Hex(0x7CC76BFF);
        static readonly Color GrassDark = Materials.Hex(0x6BB85CFF);
        static readonly Color Rock = Materials.Hex(0xC9A27AFF);
        static readonly Color RockHigh = Materials.Hex(0xB38E6CFF);
        static readonly Color Foam = Materials.Hex(0xD9F3FAFF);

        void Build()
        {
            var t = transform;
            for (int x = 0; x < Level.Width; x++)
            for (int z = 0; z < Level.Depth; z++)
            {
                var cell = new Cell(x, z);
                int h = Level.Column(cell);
                if (h >= 0) BuildColumn(cell, h, t);
            }

            foreach (var c in Level.Pearls) pearls.Add(BuildPearl(CellTop(c, Level.Column(c)), t));
            foreach (var c in Level.Checkpoints) BuildCheckpoint(c, t);
            foreach (var c in Level.Crumbles) Crumble.Create(CellTop(c, Level.Platform(c)), t);
            foreach (var m in Level.Movers) MovingPlatform.Create(m, t);
            foreach (var c in Level.Crabs) crabs.Add(Crab.Create(c, t));
            goal = BuildGoal(CellTop(Level.Goal, Level.Column(Level.Goal)), t);
        }

        void BuildColumn(Cell cell, int h, Transform parent)
        {
            var top = CellTop(cell, h);
            float height = top.y - ColumnBottom;
            bool checker = (cell.X + cell.Z) % 2 == 0;
            var column = Shapes.Make(PrimitiveType.Cube, parent, new Vector3(top.x, ColumnBottom + height * 0.5f, top.z),
                new Vector3(Moves.Tile, height, Moves.Tile), h >= 5 ? RockHigh : Rock, collider: true, name: $"Column {cell}");
            // Grassy (or sandy, at sea level) cap in a gentle checkerboard so the tiles read clearly at an angle.
            var capColor = h <= 1 ? (checker ? Sand : SandDark) : (checker ? Grass : GrassDark);
            Shapes.Make(PrimitiveType.Cube, parent, top + Vector3.down * 0.14f,
                new Vector3(Moves.Tile + 0.04f, 0.3f, Moves.Tile + 0.04f), capColor, name: "Cap");
            Shapes.Make(PrimitiveType.Cylinder, parent, new Vector3(top.x, SeaLevel + 0.02f, top.z),
                new Vector3(Moves.Tile * 1.3f, 0.02f, Moves.Tile * 1.3f), Foam, name: "Foam");

            char thing = Level.Thing(cell);
            if (thing == '^') BuildUrchin(column, top);
            if (thing == 'b') BouncePad.Create(column, top);
        }

        static void BuildUrchin(GameObject column, Vector3 top)
        {
            column.AddComponent<Spikes>();
            var purple = Materials.Hex(0x5B3A7AFF);
            var tip = Materials.Hex(0xB58AD9FF);
            var root = new GameObject("Urchin").transform;
            root.SetParent(column.transform.parent, false);
            root.position = top;
            for (int i = 0; i < 4; i++)
            {
                var offset = new Vector3((i % 2 - 0.5f) * 0.9f, 0f, (i / 2 - 0.5f) * 0.9f);
                Shapes.Make(PrimitiveType.Sphere, root, offset + Vector3.up * 0.2f, new Vector3(0.7f, 0.45f, 0.7f), purple, name: "Body");
                for (int s = 0; s < 3; s++)
                {
                    var spine = Shapes.Make(PrimitiveType.Cube, root, offset + Vector3.up * 0.35f, new Vector3(0.07f, 0.9f, 0.07f), tip, name: "Spine");
                    spine.transform.localRotation = Quaternion.Euler(30f, s * 120f + i * 20f, 0f);
                }
            }
        }

        static Transform BuildPearl(Vector3 top, Transform parent)
        {
            var root = new GameObject("Pearl").transform;
            root.SetParent(parent, false);
            root.position = top + Vector3.up * 0.8f;
            var bob = Shapes.Make(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.5f, Materials.Hex(0xFFF1F4FF), name: "Pearl");
            bob.AddComponent<Bob>().Height = 0.25f;
            Shapes.Make(PrimitiveType.Sphere, bob.transform, new Vector3(-0.15f, 0.18f, -0.15f), Vector3.one * 0.3f, Color.white, name: "Shine");
            return root;
        }

        void BuildCheckpoint(Cell cell, Transform parent)
        {
            var top = CellTop(cell, Level.Column(cell));
            var root = new GameObject("Checkpoint").transform;
            root.SetParent(parent, false);
            root.position = top;
            root.rotation = Quaternion.Euler(0f, IsoCamera.Yaw, 0f); // flag faces the camera side-on
            Shapes.Make(PrimitiveType.Cylinder, root, Vector3.up * 1f, new Vector3(0.12f, 1f, 0.12f), Materials.Hex(0x8A6A48FF), name: "Post");
            var flag = Shapes.Make(PrimitiveType.Cube, root, new Vector3(0.36f, 1.7f, 0f), new Vector3(0.6f, 0.4f, 0.06f), Materials.Hex(0xB0B7C3FF), name: "Flag");
            checkpoints.Add((root, flag.GetComponent<Renderer>(), top + Vector3.up * 0.05f));
        }

        static Transform BuildGoal(Vector3 top, Transform parent)
        {
            var root = new GameObject("Goal Shell").transform;
            root.SetParent(parent, false);
            root.position = top;
            Shapes.Make(PrimitiveType.Cylinder, root, Vector3.up * 0.06f, new Vector3(1.8f, 0.06f, 1.8f), Materials.Hex(0xFFE9A8FF), name: "Glow");
            var gem = Shapes.Make(PrimitiveType.Cube, root, Vector3.up * 1.4f, Vector3.one * 0.8f, Materials.Hex(0xF5C24CFF), name: "Shell");
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            gem.AddComponent<Bob>().Height = 0.35f;
            root.gameObject.AddComponent<Spin>().DegreesPerSecond = new Vector3(0f, 90f, 0f);
            return root;
        }

        // ---- play ---------------------------------------------------------------------------------

        void Update()
        {
            if (!Running || finished) return;
            Seconds += Time.deltaTime;
            if (dying) return;

            var p = player.transform.position;
            if (p.y < SeaLevel - 0.9f)
            {
                StartCoroutine(Respawn());
                return;
            }

            var chest = p + Vector3.up * 0.65f;
            for (int i = pearls.Count - 1; i >= 0; i--)
            {
                var pearl = pearls[i];
                if (!pearl.gameObject.activeSelf) continue;
                if ((pearl.GetChild(0).position - chest).sqrMagnitude < 1.1f)
                {
                    pearl.gameObject.SetActive(false);
                    PearlsCollected++;
                }
            }

            for (int i = 0; i < checkpoints.Count; i++)
            {
                var cp = checkpoints[i];
                var d = cp.spawn - p;
                if (Mathf.Abs(d.y) < 1.5f && new Vector2(d.x, d.z).sqrMagnitude < 2.2f && respawn != cp.spawn)
                {
                    respawn = cp.spawn;
                    cp.flag.sharedMaterial = Materials.Get(Materials.Hex(0xF5C24CFF));
                }
            }

            foreach (var crab in crabs)
            {
                if (!crab.Alive) continue;
                var d = crab.transform.position - p;
                if (new Vector2(d.x, d.z).sqrMagnitude > 0.95f * 0.95f) continue;
                if (p.y > crab.transform.position.y + 0.95f || p.y + 1.2f < crab.transform.position.y) continue;
                if (player.VerticalSpeed < 0f && p.y > crab.transform.position.y + 0.3f)
                {
                    crab.Bop();
                    player.Launch(1.8f);
                }
                else OnHurt();
            }

            var g = goal.position - p;
            if (Mathf.Abs(g.y) < 1.6f && new Vector2(g.x, g.z).sqrMagnitude < 1.6f)
            {
                finished = true;
                Running = false;
                player.InputEnabled = false;
                goal.localScale = Vector3.one * 1.3f;
                Cleared?.Invoke(new StageResult
                {
                    Level = Level.Def, Pearls = PearlsCollected, PearlsTotal = PearlsTotal, Falls = Falls, Seconds = Seconds,
                });
            }
        }

        void OnHurt()
        {
            if (!Running || dying || finished) return;
            StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            dying = true;
            Falls++;
            Fell?.Invoke();
            player.InputEnabled = false;
            yield return new WaitForSeconds(0.45f);
            player.Teleport(respawn);
            cam.Snap();
            dying = false;
            if (Running) player.InputEnabled = true;
        }
    }

    // ---- props ------------------------------------------------------------------------------------

    /// <summary>Marks a column whose top hurts to land on.</summary>
    public sealed class Spikes : MonoBehaviour { }

    /// <summary>A springy clam pad. The player launches when landing on its column.</summary>
    public sealed class BouncePad : MonoBehaviour
    {
        Transform pad;
        float boing;

        public static void Create(GameObject column, Vector3 top)
        {
            var b = column.AddComponent<BouncePad>();
            b.pad = Shapes.Make(PrimitiveType.Cylinder, column.transform.parent, top + Vector3.up * 0.1f, new Vector3(1.5f, 0.1f, 1.5f), Materials.Hex(0xF2705AFF), name: "Bounce Pad").transform;
            Shapes.Make(PrimitiveType.Cylinder, b.pad, Vector3.up * 1.2f, new Vector3(0.6f, 1f, 0.6f), Materials.Hex(0xFFD1C2FF), name: "Center");
        }

        public void Boing() => boing = 1f;

        void Update()
        {
            if (pad == null || boing <= 0f) return;
            boing = Mathf.MoveTowards(boing, 0f, Time.deltaTime * 3f);
            float s = 1f + Mathf.Sin(boing * Mathf.PI * 3f) * boing * 0.3f;
            pad.localScale = new Vector3(1.5f * s, 0.1f / s, 1.5f * s);
        }
    }

    /// <summary>A mossy stone that shakes when stood on, drops into the sea, and comes back a few seconds later.</summary>
    public sealed class Crumble : MonoBehaviour
    {
        const float ShakeTime = 0.55f, GoneTime = 3f;
        Vector3 home;
        Collider solid;
        Renderer look;
        float timer = -1f;
        bool fallen;

        public static void Create(Vector3 top, Transform parent)
        {
            var go = Shapes.Make(PrimitiveType.Cube, parent, top + Vector3.down * 0.35f, new Vector3(Moves.Tile * 0.94f, 0.7f, Moves.Tile * 0.94f),
                Materials.Hex(0x8FA67AFF), collider: true, name: "Crumbling Stone");
            Shapes.Make(PrimitiveType.Cube, go.transform, new Vector3(0.2f, 0.45f, -0.1f), new Vector3(0.5f, 0.2f, 0.45f), Materials.Hex(0x5E8A4EFF), name: "Moss");
            var c = go.AddComponent<Crumble>();
            c.home = go.transform.position;
            c.solid = go.GetComponent<Collider>();
            c.look = go.GetComponent<Renderer>();
        }

        public void Touch()
        {
            if (timer < 0f && !fallen) timer = 0f;
        }

        void Update()
        {
            if (timer < 0f) return;
            timer += Time.deltaTime;
            if (!fallen)
            {
                transform.position = home + new Vector3(Mathf.Sin(timer * 70f), 0f, Mathf.Cos(timer * 53f)) * 0.06f;
                if (timer >= ShakeTime)
                {
                    fallen = true;
                    solid.enabled = false;
                    timer = 0f;
                }
                return;
            }
            if (timer < 1f) transform.position = home + Vector3.down * (timer * timer * 12f);
            else look.enabled = false;
            if (timer >= GoneTime)
            {
                transform.position = home;
                solid.enabled = true;
                look.enabled = true;
                fallen = false;
                timer = -1f;
            }
        }
    }

    /// <summary>A driftwood raft gliding back and forth over open sea. Runs before the player so riders move with it.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MovingPlatform : MonoBehaviour
    {
        public const float Speed = 2.6f;
        public Vector3 Delta { get; private set; }
        Vector3 a, b;
        float period, phase;

        public static void Create(Track track, Transform parent)
        {
            Vector3 End(int along) => Stage.CellTop(track.Axis == Axis.X ? new Cell(along, track.Cell.Z) : new Cell(track.Cell.X, along), track.Height);
            var start = Stage.CellTop(track.Cell, track.Height);
            var go = Shapes.Make(PrimitiveType.Cube, parent, start + Vector3.down * 0.25f, new Vector3(Moves.Tile * 0.94f, 0.5f, Moves.Tile * 0.94f),
                Materials.Hex(0xA0714AFF), collider: true, name: "Raft");
            for (int i = -1; i <= 1; i++)
            {
                var plankScale = track.Axis == Axis.X ? new Vector3(0.28f, 1.1f, 1.02f) : new Vector3(1.02f, 1.1f, 0.28f);
                var plankPos = track.Axis == Axis.X ? new Vector3(i * 0.33f, 0f, 0f) : new Vector3(0f, 0f, i * 0.33f);
                Shapes.Make(PrimitiveType.Cube, go.transform, plankPos, plankScale, Materials.Hex(0x7E5634FF), name: "Plank");
            }
            var m = go.AddComponent<MovingPlatform>();
            m.a = End(track.From) + Vector3.down * 0.25f;
            m.b = End(track.To) + Vector3.down * 0.25f;
            float distance = Vector3.Distance(m.a, m.b);
            m.period = 2f * distance / Speed;
            // Start where the level author put it.
            float t = distance > 0f ? Vector3.Distance(m.a, go.transform.position) / distance : 0f;
            m.phase = Mathf.Acos(1f - 2f * Mathf.Clamp01(t)) / (2f * Mathf.PI) * m.period;
        }

        void Update()
        {
            phase += Time.deltaTime;
            // Eases in and out at each end so it's easy to step on and off.
            float t = 0.5f - 0.5f * Mathf.Cos(phase / period * 2f * Mathf.PI);
            var next = Vector3.Lerp(a, b, t);
            Delta = next - transform.position;
            transform.position = next;
        }
    }

    /// <summary>A beach crab scuttling back and forth. Land on it to bop it; touch it any other way and you're sent back.</summary>
    public sealed class Crab : MonoBehaviour
    {
        const float Speed = 2.2f;
        public bool Alive { get; private set; } = true;
        Vector3 a, b;
        int direction = 1;
        float squash;
        Transform body;

        public static Crab Create(Track track, Transform parent)
        {
            Vector3 End(int along) => Stage.CellTop(track.Axis == Axis.X ? new Cell(along, track.Cell.Z) : new Cell(track.Cell.X, along), track.Height);
            var root = new GameObject("Crab").transform;
            root.SetParent(parent, false);
            root.position = Stage.CellTop(track.Cell, track.Height);
            var red = Materials.Hex(0xE8583CFF);
            var dark = Materials.Hex(0xB23A26FF);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            Shapes.Make(PrimitiveType.Sphere, body, Vector3.up * 0.35f, new Vector3(1f, 0.5f, 0.75f), red, name: "Shell");
            Shapes.Make(PrimitiveType.Sphere, body, new Vector3(-0.55f, 0.35f, 0.35f), new Vector3(0.38f, 0.3f, 0.3f), dark, name: "Claw");
            Shapes.Make(PrimitiveType.Sphere, body, new Vector3(0.55f, 0.35f, 0.35f), new Vector3(0.38f, 0.3f, 0.3f), dark, name: "Claw");
            Shapes.Make(PrimitiveType.Sphere, body, new Vector3(-0.18f, 0.68f, 0.2f), Vector3.one * 0.18f, Color.white, name: "Eye");
            Shapes.Make(PrimitiveType.Sphere, body, new Vector3(0.18f, 0.68f, 0.2f), Vector3.one * 0.18f, Color.white, name: "Eye");
            Shapes.Make(PrimitiveType.Sphere, body, new Vector3(-0.18f, 0.7f, 0.28f), Vector3.one * 0.09f, Color.black, name: "Pupil");
            Shapes.Make(PrimitiveType.Sphere, body, new Vector3(0.18f, 0.7f, 0.28f), Vector3.one * 0.09f, Color.black, name: "Pupil");
            var crab = root.gameObject.AddComponent<Crab>();
            crab.body = body;
            // Walk from the middle of the first cell to the middle of the last, facing sideways like a crab.
            crab.a = End(track.From);
            crab.b = End(track.To);
            var along = (crab.b - crab.a).normalized;
            if (along.sqrMagnitude > 0f) root.rotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up));
            return crab;
        }

        public void Bop()
        {
            Alive = false;
            squash = 0f;
        }

        void Update()
        {
            if (!Alive)
            {
                squash += Time.deltaTime;
                body.localScale = new Vector3(1f + squash * 2f, Mathf.Max(0.05f, 1f - squash * 4f), 1f + squash * 2f);
                if (squash > 0.4f) gameObject.SetActive(false);
                return;
            }
            var target = direction > 0 ? b : a;
            transform.position = Vector3.MoveTowards(transform.position, target, Speed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude < 0.0001f) direction = -direction;
            body.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 18f)) * 0.05f;
        }
    }
}
