using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DivingPrototype
{
    // Checks may reset GameState, but must not overwrite the user's run afterward.
    internal sealed class GameStateSnapshot : IDisposable
    {
        private readonly int days = GameState.days, credits = GameState.credits, tellurium = GameState.tellurium;
        private readonly Dictionary<string, GameState.MiningSpotState> miningSpots = GameState.miningSpots;
        private readonly bool tutorialShown = GameState.deliveryTutorialShown;
        private readonly HashSet<string> talks = GameState.npcsTalks;
        private readonly Dictionary<string, int> purchases = GameState.itemsBought;
        public void Dispose()
        {
            GameState.miningSpots = miningSpots;
            GameState.days = days; GameState.credits = credits; GameState.tellurium = tellurium;
            GameState.deliveryTutorialShown = tutorialShown;
            GameState.npcsTalks = talks; GameState.itemsBought = purchases;
        }
    }

    public static class DayDeliveryCheck
    {
        [MenuItem("Diving Prototype/Check Day Tutorials And Delivery Setup")]
        public static void Run()
        {
            using var state = new GameStateSnapshot();
            int checks = 0;
            Action<bool,string> check = (ok,label) => { FlashlightCheck.Require(ok,label); checks++; };
            GameState.reset();
            check(GameState.days==1 && GameState.credits==100 && GameState.tellurium==0,"New game starts on interior day one with 100 credits");
            check(!GameState.deliveryTutorialShown && GameState.itemsBought[GameState.MiningMachine]==0,"New game clears tutorial eligibility and orders");
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Ocean Interactables.unity");
            try
            {
                var ocean=scene.GetRootGameObjects().Select(g=>g.GetComponent<OceanInteractions>()).First(o=>o!=null);
                var tutorial=ocean.GetComponent<OceanTutorial>();
                check(ocean.oceanMap.GetComponentsInChildren<OceanTarget>(true).All(t=>t.kind!=OceanTarget.TargetKind.Item),"No authored test pickups or highlights remain");
                var spots=ocean.targets.Where(t=>t.kind==OceanTarget.TargetKind.MiningSpot).ToArray();
                check(spots.All(t=>!string.IsNullOrEmpty(t.PersistentId)) && spots.Select(t=>t.PersistentId).Distinct().Count()==spots.Length,"Authored mining spots have unique stable identities");
                check(ocean.deliveryPrefab!=null && ocean.deliveryPrefab.kind==OceanTarget.TargetKind.Item,"Reusable delivery prefab wired");
                check(ocean.resultsScene=="Result" && ocean.deliveryRadius==10,"Results exit and default radius saved");
                foreach(int day in new[]{1,2,3,4,6})
                {
                    GameState.days=day; tutorial.BeginExcursion();
                    check(tutorial.IsVisible==(day==2 || day==4),"Only day two and four start guidance without deliveries: day "+day);
                    check(tutorial.Stage==(day==2?1:0),"No numbered tutorial on later days");
                    if(day==4)
                    {
                        check(tutorial.Mode==OceanTutorial.TutorialMode.Depletion && tutorial.Text==OceanTutorial.DepletionMessage,"Day four explains depletion without requiring a purchase");
                        tutorial.Advance(29.99f); check(tutorial.IsVisible,"Depletion guidance provides thirty seconds to read");
                        tutorial.Advance(.02f); check(!tutorial.IsVisible,"Depletion guidance dismisses after reading");
                    }
                }
                GameState.days=4;
                GameState.itemsBought[GameState.MiningMachine]=2;
                ocean.deliveryRadius=.1f;
                typeof(OceanInteractions).GetMethod("SpawnDeliveries",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ocean,null);
                check(GameState.itemsBought[GameState.MiningMachine]==2 && ocean.DeliveredItemCount==0,"Blocked delivery area preserves unspawned purchases");
                tutorial.BeginExcursion();
                check(tutorial.Mode==OceanTutorial.TutorialMode.Depletion && !GameState.deliveryTutorialShown,"Blocked orders do not consume delivery guidance eligibility or hide day-four depletion guidance");
                GameState.deliveryTutorialShown=true; GameState.credits=0;
                GameState.reset();
                check(GameState.credits==100 && !GameState.deliveryTutorialShown && GameState.itemsBought[GameState.MiningMachine]==0,"New game restores credits and resets delivery history");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("Day tutorial and delivery setup checks passed: "+checks+" assertions. One blocked-delivery warning is expected.");
        }
    }
}
