using System.Collections.Generic;
using UnityEngine;
using Wildtide.Core;

namespace Wildtide
{
    /// <summary>
    /// One toon material per colour, shared everywhere, so the whole game gets its cel-shaded look from a
    /// single shader (the library's rule: the look comes from the shader, not from baked light).
    /// </summary>
    public static class Materials
    {
        static readonly Dictionary<Color32, Material> cache = new Dictionary<Color32, Material>();
        static Shader toon;

        static Shader Toon
        {
            get
            {
                if (toon != null) return toon;
                toon = Shader.Find("Wildtide/Toon");
                if (toon == null) toon = Shader.Find("Universal Render Pipeline/Lit");
                if (toon == null) toon = Shader.Find("Standard");
                return toon;
            }
        }

        public static Material Get(Color color)
        {
            Color32 key = color;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Toon) { name = "Toon " + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(m, color);
            cache[key] = m;
            return m;
        }

        /// <summary>Unshaded, alpha-clipped material for concept-art billboards.</summary>
        public static Material Billboard(Texture texture)
        {
            var m = new Material(Toon) { name = "Billboard " + texture.name };
            SetColor(m, Color.white);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
            if (m.HasProperty("_ShadeColor")) m.SetColor("_ShadeColor", Color.white);
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            return m;
        }

        static void SetColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }

        public static Color Hex(uint rgba)
        {
            return new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
        }

        public static Color ForElement(Element e)
        {
            switch (e)
            {
                case Element.Flame: return Hex(0xF07A2AFF);
                case Element.Tide: return Hex(0x2F8FE0FF);
                case Element.Verdant: return Hex(0x5DB54AFF);
                case Element.Storm: return Hex(0xF2C72EFF);
                case Element.Stone: return Hex(0xA08560FF);
                case Element.Frost: return Hex(0x8FD8F0FF);
                case Element.Gale: return Hex(0x9EE3C0FF);
                case Element.Venom: return Hex(0x9A4FC9FF);
                case Element.Spirit: return Hex(0xD99AC8FF);
                case Element.Iron: return Hex(0x8C95A3FF);
                case Element.Lumen: return Hex(0xF5E08AFF);
                default: return Hex(0x4B3F8CFF);
            }
        }
    }

    public static class Shapes
    {
        /// <summary>A primitive with the shared toon material. Colliders are removed unless asked for.</summary>
        public static GameObject Make(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color,
            bool collider = false, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Materials.Get(color);
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
    }

    /// <summary>
    /// Builds how a creature looks. Uses the best art available for its prompt id:
    /// 1. Resources/Creatures/&lt;artId&gt; (a prefab or imported GLB from the mesh step),
    /// 2. Resources/Concepts/&lt;artId&gt; (a cut-out concept PNG, shown as a billboard),
    /// 3. a coloured placeholder built from primitives.
    /// </summary>
    public static class CreatureView
    {
        public static GameObject Build(Species species, Transform parent, float maxScale = 10f)
        {
            var root = new GameObject(species.Name);
            root.transform.SetParent(parent, false);
            float scale = Mathf.Min(species.PlaceholderScale, maxScale);

            var model = Resources.Load<GameObject>("Creatures/" + species.ArtId);
            if (model != null)
            {
                var m = Object.Instantiate(model, root.transform);
                m.transform.localPosition = Vector3.zero;
                m.transform.localScale = Vector3.one * scale;
                foreach (var c in m.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                return root;
            }

            var concept = Resources.Load<Texture2D>("Concepts/" + species.ArtId);
            if (concept != null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(quad.GetComponent<Collider>());
                quad.name = "Concept";
                quad.transform.SetParent(root.transform, false);
                float h = 1.6f * scale, w = h * concept.width / Mathf.Max(1, concept.height);
                quad.transform.localScale = new Vector3(w, h, 1f);
                quad.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
                quad.GetComponent<Renderer>().sharedMaterial = Materials.Billboard(concept);
                quad.AddComponent<Billboard>();
                return root;
            }

            BuildPlaceholder(species, root.transform, scale);
            return root;
        }

        static void BuildPlaceholder(Species s, Transform root, float scale)
        {
            var body = Materials.Hex(s.PlaceholderColor);
            var accent = Materials.ForElement(s.SecondaryElement ?? s.Element);
            var dark = Color.Lerp(body, Color.black, 0.35f);
            var t = new GameObject("Placeholder").transform;
            t.SetParent(root, false);
            t.localScale = Vector3.one * scale;
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.85f, 1.05f), body, name: "Body");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(0f, 0.42f, 0.18f), new Vector3(0.7f, 0.55f, 0.75f), Color.Lerp(body, Color.white, 0.45f), name: "Belly");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(-0.3f, 1f, -0.05f), new Vector3(0.28f, 0.34f, 0.2f), dark, name: "Ear");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(0.3f, 1f, -0.05f), new Vector3(0.28f, 0.34f, 0.2f), dark, name: "Ear");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(-0.2f, 0.66f, 0.46f), Vector3.one * 0.16f, Color.black, name: "Eye");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(0.2f, 0.66f, 0.46f), Vector3.one * 0.16f, Color.black, name: "Eye");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(-0.17f, 0.7f, 0.52f), Vector3.one * 0.05f, Color.white, name: "Shine");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(0.23f, 0.7f, 0.52f), Vector3.one * 0.05f, Color.white, name: "Shine");
            // The element "signature feature": a glowing tail tip.
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(0f, 0.55f, -0.62f), Vector3.one * 0.3f, accent, name: "Tail");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(-0.3f, 0.1f, 0.2f), new Vector3(0.25f, 0.2f, 0.3f), dark, name: "Foot");
            Shapes.Make(PrimitiveType.Sphere, t, new Vector3(0.3f, 0.1f, 0.2f), new Vector3(0.25f, 0.2f, 0.3f), dark, name: "Foot");
        }
    }

    /// <summary>Turns around the vertical axis to face whichever camera is rendering.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var forward = transform.position - cam.transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(forward);
        }
    }

    /// <summary>Idle bob so placeholders feel alive.</summary>
    public sealed class Bob : MonoBehaviour
    {
        public float Height = 0.06f;
        public float Speed = 2.4f;
        Vector3 origin;
        float phase;

        void Start()
        {
            origin = transform.localPosition;
            phase = Random.value * 10f;
        }

        void Update()
        {
            transform.localPosition = origin + Vector3.up * (Mathf.Sin((Time.time + phase) * Speed) * 0.5f + 0.5f) * Height;
        }
    }

    public sealed class Spin : MonoBehaviour
    {
        public Vector3 DegreesPerSecond = new Vector3(0f, 0f, 40f);
        void Update() => transform.Rotate(DegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
