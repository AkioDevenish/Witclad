using System;
using UnityEngine;
using Wildtide.UI;

namespace Wildtide
{
    /// <summary>
    /// Runs and jumps with the on-screen stick and jump button (phones) or WASD / arrows + Space (editor).
    /// Jumps are forgiving: a short "coyote" window after walking off a ledge, a buffered press just before landing,
    /// and letting go early cuts the jump short. Tuned to clear what Core's <c>Moves.Rise</c> promises.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public const float Gravity = 32f;
        public const float RunSpeed = 7f;
        /// <summary>Apex of a full jump, in world units: two steps plus clearance.</summary>
        public const float JumpHeight = 2.6f;
        /// <summary>Apex of a bounce-pad launch: six steps plus clearance.</summary>
        public const float BounceHeight = 7.2f;
        const float CoyoteTime = 0.1f;
        const float JumpBuffer = 0.14f;

        public VirtualJoystick Joystick;
        public HoldButton JumpButton;
        public bool InputEnabled;
        /// <summary>Yaw of the isometric camera, so pushing the stick up runs up the screen.</summary>
        public float CameraYaw = 45f;

        /// <summary>Raised when the player lands on something harmful (urchin spikes).</summary>
        public event Action Hurt;

        public float VerticalSpeed => verticalSpeed;
        public bool Grounded { get; private set; }

        CharacterController controller;
        Transform visual, shadow;
        Vector3 planar;
        float verticalSpeed;
        float lastGrounded = -1f, lastJumpPress = -1f;
        bool jumpHeld, rising;
        Collider ground, groundThisMove;
        float squash;

        public static PlayerController Create()
        {
            var go = new GameObject("Player");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.3f;
            cc.radius = 0.38f;
            cc.center = new Vector3(0f, 0.65f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            var p = go.AddComponent<PlayerController>();
            p.visual = BuildVisual(go.transform);
            p.shadow = Shapes.Make(PrimitiveType.Cylinder, null, Vector3.zero, new Vector3(0.8f, 0.01f, 0.8f), Materials.Hex(0x1F3C5AFF), name: "Player Shadow").transform;
            return p;
        }

        /// <summary>Stand-in hero: a little islander in a sea-foam tunic with a coral scarf and a sun hat.</summary>
        static Transform BuildVisual(Transform parent)
        {
            var v = new GameObject("Visual").transform;
            v.SetParent(parent, false);
            var skin = Materials.Hex(0xC98B63FF);
            Shapes.Make(PrimitiveType.Capsule, v, new Vector3(-0.14f, 0.25f, 0f), new Vector3(0.2f, 0.25f, 0.2f), Materials.Hex(0x3C4A6BFF), name: "Leg");
            Shapes.Make(PrimitiveType.Capsule, v, new Vector3(0.14f, 0.25f, 0f), new Vector3(0.2f, 0.25f, 0.2f), Materials.Hex(0x3C4A6BFF), name: "Leg");
            Shapes.Make(PrimitiveType.Capsule, v, new Vector3(0f, 0.68f, 0f), new Vector3(0.6f, 0.34f, 0.5f), Materials.Hex(0x4FC7B0FF), name: "Tunic");
            Shapes.Make(PrimitiveType.Cylinder, v, new Vector3(0f, 0.92f, 0f), new Vector3(0.5f, 0.07f, 0.44f), Materials.Hex(0xF2705AFF), name: "Scarf");
            Shapes.Make(PrimitiveType.Cube, v, new Vector3(0.12f, 0.8f, -0.26f), new Vector3(0.12f, 0.3f, 0.06f), Materials.Hex(0xF2705AFF), name: "Scarf Tail");
            Shapes.Make(PrimitiveType.Sphere, v, new Vector3(0f, 1.15f, 0f), Vector3.one * 0.46f, skin, name: "Head");
            Shapes.Make(PrimitiveType.Sphere, v, new Vector3(-0.1f, 1.18f, 0.2f), Vector3.one * 0.08f, Color.black, name: "Eye");
            Shapes.Make(PrimitiveType.Sphere, v, new Vector3(0.1f, 1.18f, 0.2f), Vector3.one * 0.08f, Color.black, name: "Eye");
            Shapes.Make(PrimitiveType.Cylinder, v, new Vector3(0f, 1.34f, 0f), new Vector3(0.8f, 0.03f, 0.8f), Materials.Hex(0xF5D98AFF), name: "Hat Brim");
            Shapes.Make(PrimitiveType.Sphere, v, new Vector3(0f, 1.38f, 0f), new Vector3(0.4f, 0.22f, 0.4f), Materials.Hex(0xF5D98AFF), name: "Hat");
            Shapes.Make(PrimitiveType.Cylinder, v, new Vector3(0f, 1.37f, 0f), new Vector3(0.42f, 0.03f, 0.42f), Materials.Hex(0xF2705AFF), name: "Hat Band");
            return v;
        }

        void Awake() => controller = GetComponent<CharacterController>();

        void OnDestroy()
        {
            if (shadow != null) Destroy(shadow.gameObject);
        }

        public void Teleport(Vector3 position, float yaw = 45f)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            planar = Vector3.zero;
            verticalSpeed = 0f;
            ground = null;
            Grounded = false;
            visual.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Throws the player upwards (bounce pads, bopping a crab).</summary>
        public void Launch(float apexHeight)
        {
            verticalSpeed = Mathf.Sqrt(2f * Gravity * apexHeight);
            rising = true;
            jumpHeld = true; // a launch always goes full height
            Grounded = false;
            lastGrounded = -1f;
            squash = -0.25f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 input = Vector2.zero;
            bool pressed = false, held = false;
            if (InputEnabled)
            {
                if (Joystick != null) input = Joystick.Value;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
                input = Vector2.ClampMagnitude(input, 1f);
                pressed = Input.GetKeyDown(KeyCode.Space) || (JumpButton != null && JumpButton.TakePress());
                held = Input.GetKey(KeyCode.Space) || (JumpButton != null && JumpButton.Held);
            }
            else if (JumpButton != null) JumpButton.TakePress(); // don't carry a press into the next level

            // Stick up runs up the screen, which on an isometric camera is diagonal across the grid.
            var yaw = Quaternion.Euler(0f, CameraYaw, 0f);
            var wish = (yaw * Vector3.right * input.x + yaw * Vector3.forward * input.y) * RunSpeed;
            float accel = Grounded ? 70f : 38f;
            planar = Vector3.MoveTowards(planar, wish, accel * dt);

            if (Grounded) lastGrounded = Time.time;
            if (pressed) lastJumpPress = Time.time;
            if (Time.time - lastJumpPress <= JumpBuffer && Time.time - lastGrounded <= CoyoteTime)
            {
                verticalSpeed = Mathf.Sqrt(2f * Gravity * JumpHeight);
                rising = true;
                jumpHeld = true;
                lastJumpPress = -1f;
                lastGrounded = -1f;
                Grounded = false;
                squash = -0.2f;
            }
            if (!held) jumpHeld = false;

            // Letting go early falls faster, so a tap is a hop and a hold is a full jump.
            float g = Gravity * (rising && !jumpHeld && verticalSpeed > 0f ? 2.6f : 1f);
            verticalSpeed -= g * dt;
            if (verticalSpeed <= 0f) rising = false;
            if (Grounded && verticalSpeed < 0f) verticalSpeed = -3f; // keeps the controller hugging the ground

            // Ride along with a drifting raft.
            var carry = Vector3.zero;
            if (Grounded && ground != null)
            {
                var raft = ground.GetComponent<MovingPlatform>();
                if (raft != null) carry = raft.Delta;
            }

            groundThisMove = null;
            bool wasGrounded = Grounded;
            controller.Move((planar + Vector3.up * verticalSpeed) * dt + carry);
            Grounded = controller.isGrounded;
            if ((controller.collisionFlags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            ground = Grounded ? groundThisMove : null;

            if (Grounded && !wasGrounded) squash = 0.22f;
            if (Grounded && ground != null) TouchGround(ground);

            Animate(dt);
            PlaceShadow();
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.6f) groundThisMove = hit.collider;
        }

        void TouchGround(Collider c)
        {
            if (c.GetComponent<Spikes>() != null)
            {
                Hurt?.Invoke();
                return;
            }
            var crumble = c.GetComponent<Crumble>();
            if (crumble != null) crumble.Touch();
            if (c.GetComponent<BouncePad>() != null)
            {
                Launch(BounceHeight);
                c.GetComponent<BouncePad>().Boing();
            }
        }

        void Animate(float dt)
        {
            var flat = new Vector3(planar.x, 0f, planar.z);
            if (flat.sqrMagnitude > 0.2f)
            {
                var target = Quaternion.LookRotation(flat);
                visual.rotation = Quaternion.Slerp(visual.rotation, target, 1f - Mathf.Exp(-16f * dt));
            }
            // Squash on landing, stretch on take-off, a little bounce while running.
            squash = Mathf.MoveTowards(squash, 0f, dt * 1.6f);
            float run = Grounded ? Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.07f * Mathf.Clamp01(flat.magnitude / RunSpeed) : 0f;
            visual.localScale = new Vector3(1f + squash * 0.6f, 1f - squash, 1f + squash * 0.6f);
            visual.localPosition = Vector3.up * run;
        }

        /// <summary>A soft disc straight under the player: the main depth cue on an isometric screen.</summary>
        void PlaceShadow()
        {
            if (shadow == null) return;
            var from = transform.position + Vector3.up * 0.3f;
            if (Physics.Raycast(from, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                shadow.gameObject.SetActive(true);
                shadow.position = hit.point + Vector3.up * 0.02f;
                float s = Mathf.Lerp(0.85f, 0.4f, Mathf.Clamp01((transform.position.y - hit.point.y) / 6f));
                shadow.localScale = new Vector3(s, 0.01f, s);
            }
            else shadow.gameObject.SetActive(false);
        }
    }

    /// <summary>Isometric camera: fixed 30-degree pitch and 45-degree yaw, orthographic, smoothly following a target.</summary>
    public sealed class IsoCamera : MonoBehaviour
    {
        public Transform Target;
        public const float Pitch = 30f;
        public const float Yaw = 45f;
        const float Distance = 40f;

        Camera cam;

        public static IsoCamera Create()
        {
            var go = new GameObject("Iso Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8.5f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 160f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Materials.Hex(0x3FA7D6FF);
            go.AddComponent<AudioListener>();
            var iso = go.AddComponent<IsoCamera>();
            iso.cam = cam;
            go.transform.rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            return iso;
        }

        Vector3 Goal => Target.position + Vector3.up * 1f - transform.forward * Distance;

        public void Snap()
        {
            if (Target != null) transform.position = Goal;
        }

        void LateUpdate()
        {
            if (Target == null) return;
            // Phones in landscape show more sideways than up; zoom out a little on narrow (tall) screens.
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7f;
            cam.orthographicSize = aspect < 1.5f ? 10f : 8.5f;
            transform.position = Vector3.Lerp(transform.position, Goal, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }
    }
}
