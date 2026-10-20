using UnityEditor;
using UnityEngine;

namespace DivingPrototype
{
    public static class SwimmingCheck
    {
        [MenuItem("Diving Prototype/Check Swimming")]
        public static void Run()
        {
            Vector2 velocity = Vector2.zero;
            for (int i = 0; i < 50; i++)
                velocity = SwimmerController.StepVelocity(velocity, Vector2.one, 5f, 8f, 7f, .02f);
            Require(Mathf.Abs(velocity.magnitude - 5f) < .001f, "Diagonal speed exceeds or misses the cap");
            var braking = velocity;
            float stoppingDistance = 0f;
            for (int i = 0; i < 36; i++)
            {
                braking = SwimmerController.StepVelocity(braking, Vector2.zero, 5f, 8f, 7f, .02f);
                stoppingDistance += braking.magnitude * .02f;
                if (i == 0) Require(braking.magnitude > 0f && braking.magnitude < velocity.magnitude, "Braking must be gradual");
            }
            Require(braking.magnitude < .001f, "Release must stop within 0.72 seconds");
            Require(stoppingDistance > 1.6f && stoppingDistance <= 1.9f, "Braking must retain a short glide of 1.6 to 1.9 units");
            Require(SwimmerController.StepVelocity(Vector2.zero, Vector2.zero, 5f, 8f, 7f, .02f) == Vector2.zero, "Stopped player must stay stopped");
            var firstStep = SwimmerController.StepVelocity(Vector2.zero, Vector2.right, 5f, 8f, 3f, .02f);
            Require(Mathf.Abs(firstStep.x - .16f) < .001f, "Acceleration must be gradual");
            var reversing = SwimmerController.StepVelocity(Vector2.right * 5f, Vector2.left, 5f, 8f, 3f, .02f);
            Require(reversing.x > 0f && reversing.x < 5f, "Reversal must preserve initial momentum");
            Debug.Log("Swimming check passed: acceleration, diagonal cap, quick braking, stopping distance, reversal.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }
    }
}
