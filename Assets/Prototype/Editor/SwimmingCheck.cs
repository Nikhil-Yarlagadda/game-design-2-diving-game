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
                    Require(speed > 0f && acceleration > 0f && deceleration > 0f, "Swimming checks require positive tuning values");
                    Vector2 velocity = Vector2.zero;
                    for (int i = 0; i < Mathf.CeilToInt(speed / acceleration / .02f); i++)
                        velocity = SwimmerController.StepVelocity(velocity, Vector2.one, speed, acceleration, deceleration, .02f);
                    Require(Mathf.Abs(velocity.magnitude - speed) < .001f, "Diagonal movement reaches capped speed");
                    var braking = velocity;
                    for (int i = 0; i < Mathf.CeilToInt(speed / deceleration / .02f); i++)
                    {
                        braking = SwimmerController.StepVelocity(braking, Vector2.zero, speed, acceleration, deceleration, .02f);
                        if (i == 0) Require(braking.magnitude > 0f && braking.magnitude < velocity.magnitude, "Retain slight braking inertia");
                    }
                    Require(braking.magnitude < .001f, "Release eventually stops the swimmer");
                    Require(SwimmerController.StepVelocity(Vector2.zero, Vector2.zero, speed, acceleration, deceleration, .02f) == Vector2.zero, "Stopped player stays stopped");
                    var firstStep = SwimmerController.StepVelocity(Vector2.zero, Vector2.right, speed, acceleration, deceleration, .02f);
                    Require(firstStep.x > 0f && firstStep.x < speed, "First physics step responds without jumping to full speed");
                    var reversing = SwimmerController.StepVelocity(Vector2.right * speed, Vector2.left, speed, acceleration, deceleration, .02f);
                    Require(reversing.x > 0f && reversing.x < speed, "Reversal preserves initial momentum");
                    var released = SwimmerController.StepVelocity(Vector2.right * speed, Vector2.zero, speed, acceleration, deceleration, .02f);
                    Require(reversing.x <= released.x + .001f, "Opposing input brakes at least as strongly as releasing input");
                    Require(SwimmerController.StepVelocity(Vector2.zero, Vector2.right, speed * .5f, acceleration, deceleration, speed / acceleration).x == speed * .5f, "Kelp still caps speed at fifty percent");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            Debug.Log("Swimming checks passed for both ocean scenes: startup, diagonal cap, braking, reversal, kelp speed cap.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }
    }
}
