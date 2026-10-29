using UnityEngine;
using UnityEngine.InputSystem;

namespace DivingPrototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SwimmerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float maxSpeed = 2.5f;
        [SerializeField, Min(0f)] private float acceleration = 5f;
        [SerializeField, Min(0f)] private float deceleration = 12.5f;

        private Rigidbody2D body;
        private Vector2 input;
        public Vector2 MovementInput => input;
        public bool MovementLocked { get; private set; }
        public void SetMovementLocked(bool locked)
        {
            MovementLocked = locked;
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (locked) body.linearVelocity = Vector2.zero;
        }
        [SerializeField] private PolygonCollider2D[] kelpZones = new PolygonCollider2D[0];
        public float SpeedMultiplier { get; private set; } = 1f;

        private void Awake() => body = GetComponent<Rigidbody2D>();

        public static bool HasMovementKeysPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.wKey.isPressed || keyboard.aKey.isPressed
                || keyboard.sKey.isPressed || keyboard.dKey.isPressed || keyboard.upArrowKey.isPressed
                || keyboard.leftArrowKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.rightArrowKey.isPressed);
        }
        public static Vector2 ReadMovementInput()
        {
            var keyboard = Keyboard.current;
            return keyboard == null ? Vector2.zero : Vector2.ClampMagnitude(new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0)), 1f);
        }
        private void Update() => input = MovementLocked ? Vector2.zero : ReadMovementInput();

        private void FixedUpdate()
        {
            if (MovementLocked) { body.linearVelocity = Vector2.zero; return; }
            SpeedMultiplier = 1f;
            foreach (var kelp in kelpZones)
                if (kelp != null && kelp.isActiveAndEnabled && kelp.OverlapPoint(body.position))
                { SpeedMultiplier = .5f; break; }
            body.linearVelocity = StepVelocity(body.linearVelocity, input, maxSpeed * SpeedMultiplier,
                acceleration, deceleration, Time.fixedDeltaTime);
            if (SpeedMultiplier < 1f)
                body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, maxSpeed * SpeedMultiplier);
        }

        public static Vector2 StepVelocity(Vector2 velocity, Vector2 direction,
            float speed, float accelerate, float decelerate, float deltaTime)
        {
            direction = Vector2.ClampMagnitude(direction, 1f);
            float rate = direction.sqrMagnitude > 0f ? accelerate : decelerate;
            return Vector2.MoveTowards(velocity, direction * speed, rate * deltaTime);
        }
    }
}
