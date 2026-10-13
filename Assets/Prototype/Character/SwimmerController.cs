using UnityEngine;
using UnityEngine.InputSystem;

namespace DivingPrototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SwimmerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float maxSpeed = 5f;
        [SerializeField, Min(0f)] private float acceleration = 8f;
        [SerializeField, Min(0f)] private float deceleration = 7f;

        private Rigidbody2D body;
        private Vector2 input;
        public Vector2 MovementInput => input;
        [SerializeField] private PolygonCollider2D[] kelpZones = new PolygonCollider2D[0];
        public float SpeedMultiplier { get; private set; } = 1f;

        private void Awake() => body = GetComponent<Rigidbody2D>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            input = keyboard == null ? Vector2.zero : new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1f);
        }

        private void FixedUpdate()
        {
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
