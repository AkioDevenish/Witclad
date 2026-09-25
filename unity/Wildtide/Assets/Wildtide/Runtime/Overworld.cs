using UnityEngine;
using Wildtide.Core;
using Wildtide.UI;

namespace Wildtide
{
    /// <summary>Walks the player with the on-screen joystick (phones) or WASD / arrow keys (editor).</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public float Speed = 6.5f;
        public VirtualJoystick Joystick;
        public bool InputEnabled = true;

        /// <summary>Metres walked this frame, used for encounter rolls.</summary>
        public float MovedThisFrame { get; private set; }

        CharacterController controller;
        Transform visual;
        float verticalVelocity;

        public static PlayerController Create(Vector3 position)
        {
            var go = new GameObject("Player");
            go.transform.position = position;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            var p = go.AddComponent<PlayerController>();
            p.visual = BuildVisual(go.transform);
            return p;
        }

        /// <summary>Stand-in for Player A: teal vest, explorer cap, satchel with a lantern.</summary>
        static Transform BuildVisual(Transform parent)
        {
            var v = new GameObject("Visual").transform;
            v.SetParent(parent, false);
            var skin = Materials.Hex(0xC98B63FF);
            Shapes.Make(PrimitiveType.Capsule, v, new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 0.35f, 0.45f), Materials.Hex(0x5A6B4AFF), name: "Legs");
            Shapes.Make(PrimitiveType.Capsule, v, new Vector3(0f, 1.05f, 0f), new Vector3(0.62f, 0.4f, 0.5f), Materials.Hex(0x2E9C94FF), name: "Vest");
            Shapes.Make(PrimitiveType.Sphere, v, new Vector3(0f, 1.62f, 0f), Vector3.one * 0.52f, skin, name: "Head");
            Shapes.Make(PrimitiveType.Cylinder, v, new Vector3(0f, 1.86f, 0f), new Vector3(0.55f, 0.08f, 0.55f), Materials.Hex(0xD8C8A0FF), name: "Cap");
            Shapes.Make(PrimitiveType.Cube, v, new Vector3(0f, 1.82f, 0.28f), new Vector3(0.4f, 0.04f, 0.3f), Materials.Hex(0xD8C8A0FF), name: "Brim");
            Shapes.Make(PrimitiveType.Cube, v, new Vector3(0.36f, 0.95f, -0.05f), new Vector3(0.18f, 0.3f, 0.3f), Materials.Hex(0x8A6A48FF), name: "Satchel");
            Shapes.Make(PrimitiveType.Sphere, v, new Vector3(0.4f, 0.75f, 0.05f), Vector3.one * 0.16f, Materials.Hex(0xFFD36BFF), name: "Lantern");
            return v;
        }

        void Awake() => controller = GetComponent<CharacterController>();

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }

        void Update()
        {
            Vector2 input = Vector2.zero;
            if (InputEnabled)
            {
                if (Joystick != null) input = Joystick.Value;
                // Keyboard for testing in the editor.
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
                input = Vector2.ClampMagnitude(input, 1f);
            }

            // The camera never rotates, so screen-up is world-forward.
            var move = new Vector3(input.x, 0f, input.y) * Speed;
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity - 20f * Time.deltaTime;
            var before = transform.position;
            controller.Move((move + Vector3.up * verticalVelocity) * Time.deltaTime);
            var delta = transform.position - before;
            delta.y = 0f;
            MovedThisFrame = delta.magnitude;

            if (move.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(new Vector3(move.x, 0f, move.z));
                visual.rotation = Quaternion.Slerp(visual.rotation, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
                // A little walk bounce.
                visual.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.08f;
            }
            else visual.localPosition = Vector3.zero;
        }
    }

    /// <summary>The lead creature trots after the player, like in the key art.</summary>
    public sealed class CreatureFollower : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 1.6f;
        public float Speed = 7f;

        void Update()
        {
            if (Target == null) return;
            var behind = Target.position - Target.GetChild(0).forward * Distance + Target.GetChild(0).right * 0.9f;
            var to = behind - transform.position;
            to.y = 0f;
            if (to.magnitude > 0.15f)
            {
                transform.position += Vector3.ClampMagnitude(to, Speed * Time.deltaTime * Mathf.Clamp(to.magnitude, 0.5f, 3f));
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
            if ((transform.position - Target.position).sqrMagnitude > 100f) transform.position = behind; // caught in scenery
        }
    }

    /// <summary>High-angle three-quarter view about 50 degrees above the ground, per the art direction.</summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;
        public float Pitch = 50f;
        public float Distance = 17f;

        public static FollowCamera Create()
        {
            var go = new GameObject("Overworld Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Materials.Hex(0x8FD3F2FF);
            go.AddComponent<AudioListener>();
            return go.AddComponent<FollowCamera>();
        }

        Vector3 Offset => Quaternion.Euler(Pitch, 0f, 0f) * new Vector3(0f, 0f, -Distance);

        public void Snap()
        {
            if (Target == null) return;
            transform.position = Target.position + Vector3.up + Offset;
            transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
        }

        void LateUpdate()
        {
            if (Target == null) return;
            var goal = Target.position + Vector3.up + Offset;
            transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-8f * Time.deltaTime));
            transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
        }
    }

    /// <summary>Rolls wild encounters while the player walks through tall grass.</summary>
    public sealed class EncounterRoller
    {
        /// <summary>Chance per metre walked in tall grass.</summary>
        public float ChancePerMetre = 0.07f;
        /// <summary>Metres of free walking after a battle so you can get out of the grass.</summary>
        public float GraceMetres = 4f;

        float grace;

        public void StartGrace() => grace = GraceMetres;

        public bool Step(World world, Vector3 position, float metres, IRng rng)
        {
            if (metres <= 0f) return false;
            bool inGrass = world.TallGrass.Exists(z => z.Contains(position));
            if (!inGrass) return false;
            if (grace > 0f)
            {
                grace -= metres;
                return false;
            }
            return rng.Value() < 1f - Mathf.Pow(1f - ChancePerMetre, metres);
        }
    }
}
