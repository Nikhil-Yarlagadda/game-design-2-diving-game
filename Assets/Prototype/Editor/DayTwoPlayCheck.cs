using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public static class DayTwoPlayCheck
    {
        [MenuItem("Diving Prototype/Check Day Two Tutorial In Play Mode")]
        public static async void Run()
        {
            try { Debug.Log(await Main()); }
            catch (Exception error) { Debug.LogException(error); }
        }

        // Exercises real scene transitions; restart Play after this check.
        public static async Task<object> Main()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying, "Run in Play mode");
            using var state = new GameStateSnapshot();
            bool background = Application.runInBackground;
            int checks = 0;
            Action<bool, string> check = (ok, label) => { FlashlightCheck.Require(ok, label); checks++; };
            try
            {
                Application.runInBackground = true;
                GameState.reset(); GameState.days = 2; GameState.credits = 73;
                GameState.npcsTalks.Add("Previous conversation");
                GameState.miningSpots["Earlier discovery"] = new GameState.MiningSpotState { IsProspected = true, ActualTier = 2, DisplayedTier = 3 };
                GameState.itemsBought[GameState.MiningMachine] = 2;
                await SceneManager.LoadSceneAsync("Ocean Interactables"); await Task.Delay(150);
                var ocean = UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                var tutorial = ocean.GetComponent<OceanTutorial>();
                check(tutorial.Stage == 1 && ocean.TutorialIncomplete, "Day two begins with return locked");
                check(!ocean.RequestEnterSubmarine() && !ocean.IsTransitioning && !ocean.InsideSubmarine, "Direct early return cannot transition");
                ocean.Interact();
                check(SceneManager.GetActiveScene().name == "Ocean Interactables" && GameState.days == 2, "Early submarine interaction does not skip the day");
                var mine = ocean.targets.First(t => t.kind == OceanTarget.TargetKind.MiningSpot);
                mine.Reveal(Time.time); mine.Place(Time.time); mine.AdvanceMining(Time.time + 60);
                check(GameState.miningSpots.Count > 1, "Failed attempt has mining progress to restore");
                var package = ocean.targets.First(t => t.kind == OceanTarget.TargetKind.Item);
                ocean.transform.position = package.transform.position;
                var body = ocean.GetComponent<Rigidbody2D>(); body.position = package.transform.position; body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms(); ocean.Interact();
                check(ocean.RemainingDeliveryCount == 1, "Failed attempt collects one starting package");
                ocean.Hydrox.Advance(ocean.Hydrox.MaxCapacity * .86f);
                check(!ocean.RequestEnterSubmarine() && ocean.Hydrox.NormalizedFill < .15f, "Low gas does not bypass incomplete training");
                check(ocean.Prospect(Time.time), "Can be scanning when gas is exhausted");
                ocean.Hydrox.Advance(ocean.Hydrox.MaxCapacity);
                await Task.Delay(200);
                var retry = UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(retry != null && !ReferenceEquals(retry, ocean) && SceneManager.GetActiveScene().name == "Ocean Interactables", "Gas exhaustion reloads the ocean instead of Results");
                check(GameState.days == 2 && GameState.credits == 73 && GameState.tellurium == 0 && GameState.npcsTalks.Contains("Previous conversation"), "Retry preserves day, credits and prior dialogue without settlement");
                check(GameState.miningSpots.Count == 1 && GameState.miningSpots.ContainsKey("Earlier discovery"), "Retry restores pre-excursion mining state");
                check(retry.RemainingDeliveryCount == 2 && GameState.itemsBought[GameState.MiningMachine] == 0, "Retry reissues both starting packages without duplicating pending orders");
                check(retry.Hydrox.NormalizedFill > .99f && retry.transform.position == Vector3.zero && retry.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero, "Retry restores gas, initial position and stopped movement");
                tutorial = retry.GetComponent<OceanTutorial>();
                check(tutorial.Stage == 1 && !retry.IsProspecting && !retry.GetComponent<SwimmerController>().MovementLocked, "Retry starts the first instruction with controls unlocked");
                retry.transform.position += Vector3.right * 1.1f; tutorial.Advance(0);
                tutorial.OnScan(1); tutorial.Advance(0);
                tutorial.OnPlaced(); tutorial.Advance(5);
                tutorial.OnProduced(null); tutorial.Advance(5);
                tutorial.OnRetrieved(); tutorial.Advance(5);
                tutorial.OnPlaced(); tutorial.Advance(5);
                tutorial.Advance(30);
                check(!tutorial.IsMiningComplete && tutorial.Text.Contains("Return unlocks in"), "Final reminder explains the remaining return lock");
                await Task.Delay(50);
                var hud = UnityEngine.Object.FindAnyObjectByType<OceanHud>();
                var label = hud.status.transform.parent.Find("Excursion Tutorial").GetComponentInChildren<TMPro.TMP_Text>();
                label.ForceMeshUpdate();
                check(!label.isTextOverflowing && label.text.Contains("Return unlocks in"), "Final reminder countdown fits the existing tutorial panel");
                tutorial.Advance(30);
                check(tutorial.IsMiningComplete && !retry.TutorialIncomplete && retry.RequestEnterSubmarine() && retry.ReturnConfirmationOpen, "Completed tutorial permits the return confirmation");
                check(retry.ConfirmReturn(), "Confirming completes the return");
                await SceneTransitionPlayCheck.WaitForIdle();
                check(SceneManager.GetActiveScene().name == "Result" && GameState.days == 2, "Successful return reaches Results without advancing early");
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                check(GameState.days == 3 && GameState.credits == 23, "Only successful settlement advances to day three and charges once");
                return new { checks, retryDay = 2, successfulReturnDay = 3 };
            }
            finally { Application.runInBackground = background; }
        }
    }
}
