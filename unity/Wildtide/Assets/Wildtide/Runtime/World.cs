using System.Collections.Generic;
using UnityEngine;

namespace Wildtide
{
    /// <summary>An axis-aligned area on the ground: tall grass, the Hearth House door, and so on.</summary>
    public sealed class Zone
    {
        public readonly string Name;
        public readonly Bounds Bounds;

        public Zone(string name, Vector3 center, Vector3 size)
        {
            Name = name;
            Bounds = new Bounds(center, size);
        }

        public bool Contains(Vector3 p) => Bounds.Contains(new Vector3(p.x, Bounds.center.y, p.z));
    }

    /// <summary>
    /// Greybox of Brightcove (starter town, south) and Windmill Meadows (route 1, north), built from primitives
    /// with the layout from the environment prompts. Swap pieces for real meshes as they come out of the pipeline.
    /// </summary>
    public sealed class World
    {
        public Transform Root;
        public Vector3 NewGameSpawn = new Vector3(0f, 0f, -18f);
        public Vector3 HearthSpawn = new Vector3(-8f, 0f, -21f);
        public readonly List<Zone> TallGrass = new List<Zone>();
        public Zone HearthDoor;

        static readonly Color Grass = Materials.Hex(0x7CC35AFF);
        static readonly Color GrassDark = Materials.Hex(0x4E9A3AFF);
        static readonly Color Dirt = Materials.Hex(0xD9B98AFF);
        static readonly Color Cobble = Materials.Hex(0xC9C3B8FF);
        static readonly Color Wall = Materials.Hex(0xF4F1EAFF);
        static readonly Color RoofBlue = Materials.Hex(0x3C6FB5FF);
        static readonly Color RoofTerracotta = Materials.Hex(0xC8643BFF);
        static readonly Color Wood = Materials.Hex(0x8A5A3CFF);
        static readonly Color Sea = Materials.Hex(0x3FB4D6FF);
        static readonly Color Sand = Materials.Hex(0xF1DFAFFF);
        static readonly Color Leaves = Materials.Hex(0x3F9E4DFF);
        static readonly Color Lantern = Materials.Hex(0xFFD36BFF);

        public static World Build()
        {
            var w = new World { Root = new GameObject("World").transform };
            var rng = new System.Random(42); // same layout every run
            w.BuildGround();
            w.BuildTown(rng);
            w.BuildMeadows(rng);
            w.BuildBounds();
            return w;
        }

        void BuildGround()
        {
            // The ground is the only thing the player walks on; a big box keeps it simple and solid.
            Shapes.Make(PrimitiveType.Cube, Root, new Vector3(0f, -0.5f, 10f), new Vector3(60f, 1f, 100f), Grass, collider: true, name: "Ground");
            Shapes.Make(PrimitiveType.Cube, Root, new Vector3(-36f, -0.6f, -22f), new Vector3(20f, 1f, 30f), Sea, name: "Sea");
            Shapes.Make(PrimitiveType.Cube, Root, new Vector3(-28f, -0.45f, -22f), new Vector3(4f, 1f, 30f), Sand, name: "Beach");
        }

        void BuildTown(System.Random rng)
        {
            var town = new GameObject("Brightcove").transform;
            town.SetParent(Root, false);
            // Cobblestone lanes
            Shapes.Make(PrimitiveType.Cube, town, new Vector3(0f, 0.01f, -20f), new Vector3(4f, 0.04f, 30f), Cobble, name: "Main Lane");
            Shapes.Make(PrimitiveType.Cube, town, new Vector3(-6f, 0.01f, -21f), new Vector3(16f, 0.04f, 3.5f), Cobble, name: "Cross Lane");

            House(town, new Vector3(7f, 0f, -24f), RoofBlue, "Player's Home");
            House(town, new Vector3(7f, 0f, -14f), RoofTerracotta, "Cottage");
            House(town, new Vector3(-13f, 0f, -14f), RoofBlue, "Cottage");

            // Hearth House: the healing center, marked by a big lantern sign.
            var hearth = House(town, new Vector3(-8f, 0f, -27f), RoofTerracotta, "Hearth House", 7f);
            Shapes.Make(PrimitiveType.Sphere, hearth, new Vector3(0f, 6.3f, 3.4f), Vector3.one * 1.1f, Lantern, name: "Lantern Sign")
                .AddComponent<Bob>().Height = 0.15f;
            HearthDoor = new Zone("Hearth House", new Vector3(-8f, 0f, -22.3f), new Vector3(3f, 4f, 2f));
            HearthSpawn = new Vector3(-8f, 0f, -20.5f);

            // Archivist's lab with a glass dome.
            var lab = new GameObject("Archivist's Lab").transform;
            lab.SetParent(town, false);
            lab.localPosition = new Vector3(-12f, 0f, -4f);
            Shapes.Make(PrimitiveType.Cylinder, lab, new Vector3(0f, 1.5f, 0f), new Vector3(8f, 1.5f, 8f), Wall, collider: true, name: "Base");
            Shapes.Make(PrimitiveType.Sphere, lab, new Vector3(0f, 3f, 0f), new Vector3(7f, 5f, 7f), Materials.Hex(0xA9E4F2FF), name: "Dome");

            // Harbor
            Shapes.Make(PrimitiveType.Cube, town, new Vector3(-30f, 0.1f, -30f), new Vector3(10f, 0.3f, 2.5f), Wood, collider: true, name: "Pier");
            Boat(town, new Vector3(-33f, 0f, -26f));
            Boat(town, new Vector3(-36f, 0f, -33f));

            for (int i = 0; i < 6; i++)
                Flowers(town, new Vector3(Range(rng, -4f, 4f) + (i % 2 == 0 ? -4f : 4f), 0f, Range(rng, -30f, -8f)), rng);
            Sign(town, new Vector3(2.8f, 0f, -6f));
        }

        void BuildMeadows(System.Random rng)
        {
            var route = new GameObject("Windmill Meadows").transform;
            route.SetParent(Root, false);
            // Dirt path winding north
            Shapes.Make(PrimitiveType.Cube, route, new Vector3(0f, 0.01f, 5f), new Vector3(3.5f, 0.04f, 12f), Dirt, name: "Path");
            Shapes.Make(PrimitiveType.Cube, route, new Vector3(4f, 0.01f, 12f), new Vector3(11.5f, 0.04f, 3.5f), Dirt, name: "Path");
            Shapes.Make(PrimitiveType.Cube, route, new Vector3(8f, 0.01f, 25f), new Vector3(3.5f, 0.04f, 30f), Dirt, name: "Path");
            Shapes.Make(PrimitiveType.Cube, route, new Vector3(0f, 0.01f, 40f), new Vector3(19.5f, 0.04f, 3.5f), Dirt, name: "Path");

            TallGrassPatch(route, new Vector3(-8f, 0f, 8f), new Vector2(9f, 7f), rng);
            TallGrassPatch(route, new Vector3(-6f, 0f, 24f), new Vector2(10f, 12f), rng);
            TallGrassPatch(route, new Vector3(16f, 0f, 20f), new Vector2(8f, 10f), rng);
            TallGrassPatch(route, new Vector3(18f, 0f, 48f), new Vector2(12f, 8f), rng);
            TallGrassPatch(route, new Vector3(-10f, 0f, 50f), new Vector2(10f, 9f), rng);

            Windmill(route, new Vector3(20f, 0f, 34f));
            Pond(route, new Vector3(-14f, 0f, 38f));
            BerryBush(route, new Vector3(3f, 0f, 20f));
            Sign(route, new Vector3(2.3f, 0f, 1f));

            for (int i = 0; i < 26; i++)
            {
                // Trees line the edges so the open middle stays walkable.
                float x = i % 2 == 0 ? Range(rng, -27f, -21f) : Range(rng, 23f, 28f);
                Tree(route, new Vector3(x, 0f, Range(rng, -2f, 58f)), rng);
            }
            for (int i = 0; i < 10; i++) Flowers(route, new Vector3(Range(rng, -18f, 18f), 0f, Range(rng, 0f, 56f)), rng);
        }

        void BuildBounds()
        {
            // Invisible walls at the map edge.
            void WallAt(Vector3 center, Vector3 size)
            {
                var go = new GameObject("Bound");
                go.transform.SetParent(Root, false);
                go.transform.localPosition = center;
                go.AddComponent<BoxCollider>().size = size;
            }
            WallAt(new Vector3(0f, 2f, 60.5f), new Vector3(62f, 6f, 1f));
            WallAt(new Vector3(0f, 2f, -40.5f), new Vector3(62f, 6f, 1f));
            WallAt(new Vector3(30.5f, 2f, 10f), new Vector3(1f, 6f, 102f));
            WallAt(new Vector3(-26.5f, 2f, 10f), new Vector3(1f, 6f, 102f));
        }

        // ---- building blocks ------------------------------------------------------------------------

        static Transform House(Transform parent, Vector3 pos, Color roof, string name, float width = 5f)
        {
            var h = new GameObject(name).transform;
            h.SetParent(parent, false);
            h.localPosition = pos;
            Shapes.Make(PrimitiveType.Cube, h, new Vector3(0f, 1.6f, 0f), new Vector3(width, 3.2f, 5f), Wall, collider: true, name: "Walls");
            // A cube turned 45 degrees makes a gable; its lower half hides inside the walls.
            var r = Shapes.Make(PrimitiveType.Cube, h, new Vector3(0f, 3.2f, 0f), new Vector3(width + 0.6f, 3.96f, 3.96f), roof, name: "Roof");
            r.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            Shapes.Make(PrimitiveType.Cube, h, new Vector3(0f, 0.9f, 2.51f), new Vector3(1.1f, 1.8f, 0.05f), Wood, name: "Door");
            Shapes.Make(PrimitiveType.Cube, h, new Vector3(-width * 0.3f, 2f, 2.51f), new Vector3(0.9f, 0.8f, 0.05f), Materials.Hex(0x9FD3F0FF), name: "Window");
            Shapes.Make(PrimitiveType.Cube, h, new Vector3(width * 0.3f, 2f, 2.51f), new Vector3(0.9f, 0.8f, 0.05f), Materials.Hex(0x9FD3F0FF), name: "Window");
            return h;
        }

        static void Tree(Transform parent, Vector3 pos, System.Random rng)
        {
            var t = new GameObject("Tree").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            float s = Range(rng, 0.8f, 1.3f);
            Shapes.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 1f * s, 0f), new Vector3(0.5f, 1f, 0.5f) * s, Wood, collider: true, name: "Trunk");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(0f, 2.8f * s, 0f), new Vector3(2.8f, 2.4f, 2.8f) * s, Leaves, name: "Canopy");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(0.6f, 3.6f * s, 0.3f), new Vector3(1.6f, 1.4f, 1.6f) * s, Color.Lerp(Leaves, Color.white, 0.12f), name: "Canopy Top");
        }

        void TallGrassPatch(Transform parent, Vector3 center, Vector2 size, System.Random rng)
        {
            var patch = new GameObject("Tall Grass").transform;
            patch.SetParent(parent, false);
            patch.localPosition = center;
            // Chunky tufts rather than thousands of blades: cheap on phones and readable from the high camera.
            int count = Mathf.RoundToInt(size.x * size.y * 1.1f);
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3(Range(rng, -size.x / 2f, size.x / 2f), 0.45f, Range(rng, -size.y / 2f, size.y / 2f));
                var tuft = Shapes.Make(PrimitiveType.Cube, patch, p, new Vector3(0.35f, 0.9f, 0.35f), i % 3 == 0 ? Grass : GrassDark, name: "Tuft");
                tuft.transform.localRotation = Quaternion.Euler(Range(rng, -12f, 12f), Range(rng, 0f, 90f), Range(rng, -12f, 12f));
            }
            TallGrass.Add(new Zone("Tall Grass", center, new Vector3(size.x, 4f, size.y)));
        }

        static void Windmill(Transform parent, Vector3 pos)
        {
            var w = new GameObject("Windmill").transform;
            w.SetParent(parent, false);
            w.localPosition = pos;
            Shapes.Make(PrimitiveType.Cylinder, w, new Vector3(0f, 3f, 0f), new Vector3(3f, 3f, 3f), Wall, collider: true, name: "Tower");
            Shapes.Make(PrimitiveType.Sphere, w, new Vector3(0f, 6.2f, 0f), new Vector3(3.2f, 2f, 3.2f), RoofTerracotta, name: "Cap");
            var hub = new GameObject("Sails").transform;
            hub.SetParent(w, false);
            hub.localPosition = new Vector3(0f, 5.2f, -1.8f);
            for (int i = 0; i < 4; i++)
            {
                var arm = new GameObject("Arm").transform;
                arm.SetParent(hub, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                Shapes.Make(PrimitiveType.Cube, arm, new Vector3(0f, 2.3f, 0f), new Vector3(0.9f, 4.2f, 0.08f), Materials.Hex(0xEDE3CFFF), name: "Sail");
            }
            hub.gameObject.AddComponent<Spin>();
        }

        static void Pond(Transform parent, Vector3 pos)
        {
            Shapes.Make(PrimitiveType.Cylinder, parent, pos + new Vector3(0f, 0.02f, 0f), new Vector3(9f, 0.02f, 7f), Sea, name: "Pond");
            // Blocks walking into the water.
            Shapes.Make(PrimitiveType.Cylinder, parent, pos + new Vector3(0f, 0.5f, 0f), new Vector3(7.5f, 0.5f, 5.5f), Sea, collider: true, name: "Pond Edge")
                .GetComponent<Renderer>().enabled = false;
        }

        static void BerryBush(Transform parent, Vector3 pos)
        {
            var b = Shapes.Make(PrimitiveType.Sphere, parent, pos + new Vector3(0f, 0.7f, 0f), new Vector3(1.8f, 1.4f, 1.8f), Leaves, collider: true, name: "Berry Bush");
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Shapes.Make(PrimitiveType.Sphere, b.transform, new Vector3(Mathf.Cos(a) * 0.45f, 0.1f + (i % 2) * 0.2f, Mathf.Sin(a) * 0.45f),
                    Vector3.one * 0.14f, Materials.Hex(0xD9344AFF), name: "Berry");
            }
        }

        static void Boat(Transform parent, Vector3 pos)
        {
            var b = new GameObject("Boat").transform;
            b.SetParent(parent, false);
            b.localPosition = pos;
            Shapes.Make(PrimitiveType.Cube, b, new Vector3(0f, 0.1f, 0f), new Vector3(1.6f, 0.6f, 3.4f), Materials.Hex(0xE8E2D2FF), name: "Hull");
            Shapes.Make(PrimitiveType.Cylinder, b, new Vector3(0f, 1.4f, 0f), new Vector3(0.1f, 1.2f, 0.1f), Wood, name: "Mast");
            b.gameObject.AddComponent<Bob>().Height = 0.12f;
        }

        static void Sign(Transform parent, Vector3 pos)
        {
            Shapes.Make(PrimitiveType.Cube, parent, pos + new Vector3(0f, 0.6f, 0f), new Vector3(0.15f, 1.2f, 0.15f), Wood, collider: true, name: "Signpost");
            Shapes.Make(PrimitiveType.Cube, parent, pos + new Vector3(0f, 1.2f, 0f), new Vector3(1.2f, 0.6f, 0.1f), Materials.Hex(0xB07A4FFF), name: "Sign");
        }

        static void Flowers(Transform parent, Vector3 pos, System.Random rng)
        {
            Color[] colors = { Materials.Hex(0xF7E36BFF), Materials.Hex(0xF29BB8FF), Color.white, Materials.Hex(0xB59BF2FF) };
            for (int i = 0; i < 5; i++)
            {
                var p = pos + new Vector3(Range(rng, -0.8f, 0.8f), 0.12f, Range(rng, -0.8f, 0.8f));
                Shapes.Make(PrimitiveType.Sphere, parent, p, Vector3.one * 0.22f, colors[rng.Next(colors.Length)], name: "Flower");
            }
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
