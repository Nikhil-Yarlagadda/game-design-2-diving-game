using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DivingPrototype
{
    public static class SwimmingCheck
    {
        [MenuItem("Diving Prototype/Check Swimming")]
        public static void Run()
        {
            foreach (string path in new[] { "Assets/Scenes/Ocean Interactables.unity", "Assets/Scenes/Ocean Floor.unity" })
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    SwimmerController swimmer = null;
                    foreach (var root in scene.GetRootGameObjects())
                        if (root.GetComponentInChildren<SwimmerController>(true) is SwimmerController found) swimmer = found;
                    Require(swimmer != null, "Ocean scene must have a swimmer: " + path);
                    var settings = new SerializedObject(swimmer);
                    float speed = settings.FindProperty("maxSpeed").floatValue;
                    float acceleration = settings.FindProperty("acceleration").floatValue;
                    float deceleration = settings.FindProperty("deceleration").floatValue;
                    Require(Mathf.Abs(speed - 2.5f) < .001f, "Keep the existing maximum swim speed");
                    Vector2 velocity = Vector2.zero;
                    for (int i = 0; i < 25; i++)
                        velocity = SwimmerController.StepVelocity(velocity, Vector2.one, speed, acceleration, deceleration, .02f);
                    Require(Mathf.Abs(velocity.magnitude - speed) < .001f, "Reach capped diagonal speed in half a second");
                    var braking = velocity;
                    float stoppingDistance = 0f;
                    for (int i = 0; i < 10; i++)
                    {
                        braking = SwimmerController.StepVelocity(braking, Vector2.zero, speed, acceleration, deceleration, .02f);
                        stoppingDistance += braking.magnitude * .02f;
                        if (i == 0) Require(braking.magnitude > 0f && braking.magnitude < velocity.magnitude, "Retain slight braking inertia");
                    }
                    Require(braking.magnitude < .001f, "Release stops within 0.2 seconds");
                    Require(stoppingDistance > .2f && stoppingDistance < .26f, "Release retains about a quarter-unit glide");
                    Require(SwimmerController.StepVelocity(Vector2.zero, Vector2.zero, speed, acceleration, deceleration, .02f) == Vector2.zero, "Stopped player stays stopped");
                    var firstStep = SwimmerController.StepVelocity(Vector2.zero, Vector2.right, speed, acceleration, deceleration, .02f);
                    Require(Mathf.Abs(firstStep.x - .1f) < .001f, "First physics step responds without jumping to full speed");
                    var reversing = SwimmerController.StepVelocity(Vector2.right * speed, Vector2.left, speed, acceleration, deceleration, .02f);
                    Require(reversing.x > 0f && reversing.x < speed, "Reversal preserves initial momentum");
                    Require(SwimmerController.StepVelocity(Vector2.zero, Vector2.right, speed * .5f, acceleration, deceleration, 1f).x == speed * .5f, "Kelp still caps speed at fifty percent");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            Debug.Log("Swimming checks passed for both ocean scenes: startup, diagonal cap, braking, stopping distance, reversal, kelp speed cap.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }
    }
}
