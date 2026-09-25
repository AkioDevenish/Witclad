using System.Collections.Generic;
using UnityEngine;

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

        static void SetColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }

        public static Color Hex(uint rgba)
        {
            return new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
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
