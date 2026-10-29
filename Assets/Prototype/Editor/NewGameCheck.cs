using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DivingPrototype
{
    public static class NewGameCheck
    {
        [MenuItem("Diving Prototype/Check New Game Initialization")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Run this check outside Play mode.");

            using var state = new GameStateSnapshot();
            bool paused = PauseManager.isPaused;
            float timeScale = Time.timeScale;
            var resumeField = typeof(PauseManager).GetField("resumeScene", BindingFlags.NonPublic | BindingFlags.Static);
            var resumeScene = resumeField.GetValue(null);
            int checks = 0;
            Action<bool, string> check = (ok, label) => { FlashlightCheck.Require(ok, label); checks++; };
            try
            {
                GameState.reset();
                GameState.days = 6;
                GameState.credits = 0;
                GameState.tellurium = 20;
                GameState.deliveryTutorialShown = true;
                GameState.npcsTalks.Add("Crew");
                GameState.itemsBought[GameState.MiningMachine] = 2;
                GameState.miningSpots["Test"] = new GameState.MiningSpotState { IsProspected = true };
                PauseManager.isPaused = true;
                resumeField.SetValue(null, "Ocean Interactables");
                Time.timeScale = 0;

                SceneChanger.ResetWrapper();
                check(GameState.days == 1 && GameState.credits == 100 && GameState.tellurium == 0, "New game restores starting resources");
                check(!GameState.deliveryTutorialShown && GameState.npcsTalks.Count == 0 && GameState.miningSpots.Count == 0, "New game clears tutorial, dialogue and mining progress");
                check(GameState.itemsBought.Count == 1 && GameState.itemsBought[GameState.MiningMachine] == 0, "New game clears purchases");
                check(!PauseManager.isPaused && Time.timeScale == 1 && resumeField.GetValue(null) == null, "New game clears pause state and restores time");
                SceneChanger.ResetWrapper();
                check(GameState.days == 1 && !PauseManager.isPaused && Time.timeScale == 1, "Repeated initialization is safe");
                var hook = typeof(SceneChanger).GetMethod(nameof(SceneChanger.ResetWrapper)).GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                check(hook != null && hook.loadType == RuntimeInitializeLoadType.SubsystemRegistration, "Initialization runs with Domain Reload disabled");

                var title = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Title Screen.unity");
                try
                {
                    var button = title.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                        .Single(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentMethodName(i) == nameof(SceneChanger.ResetWrapper)));
                    check(button.onClick.GetPersistentMethodName(0) == nameof(SceneChanger.ResetWrapper)
                        && button.onClick.GetPersistentMethodName(1) == nameof(SceneChanger.ChangeScene), "Title initializes before loading the first scene");
                }
                finally { EditorSceneManager.ClosePreviewScene(title); }
                Debug.Log("New game initialization checks passed: " + checks + " assertions.");
            }
            finally
            {
                PauseManager.isPaused = paused;
                resumeField.SetValue(null, resumeScene);
                Time.timeScale = timeScale;
            }
        }
    }
}
