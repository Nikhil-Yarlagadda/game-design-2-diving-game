using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DivingPrototype
{
    public static class MiningTutorialCheck
    {
        [MenuItem("Diving Prototype/Check Mining And Tutorial")]
        public static void Run()
        {
            using var state = new GameStateSnapshot();
            GameState.reset();
            GameState.days = 2;
            int checks = 0;
            Action<bool, string> check = (ok, text) => { FlashlightCheck.Require(ok, text); checks++; };
            for (int tier = 1; tier <= 5; tier++)
            {
                var go = new GameObject("Mining simulation");
                try
                {
                    var mine = go.AddComponent<OceanTarget>(); mine.kind = OceanTarget.TargetKind.MiningSpot; mine.level = tier;
                    check(!mine.Place(0), "Undiscovered spots reject placement");
                    mine.Reveal(0);
                    check(mine.Place(0) && !mine.Place(1), "Exactly one machine per spot");
                    float interval = OceanTarget.Interval(tier);
                    int amount = tier == 5 ? 2 : 1;
                    check(mine.YieldAt(interval - .001f) == 0, "No early tick");
                    check(mine.YieldAt(interval) == amount, "Exact first tick");
                    int allowance = tier == 5 ? 60 : tier * 10;
                    float boundary = allowance / amount * interval;
                    check(mine.YieldAt(boundary) == allowance && mine.ActualTier == tier - 1, "Tier allowance immediately downgrades actual tier");
                    check(mine.DisplayedTier == tier, "Tier display remains stale");
                    check(mine.YieldAt(boundary) == allowance, "Repeated evaluation cannot duplicate a tick");
                    mine.Reveal(boundary);
                    check(mine.DisplayedTier == tier - 1, "Re-prospect refreshes tier");
                    check(mine.Retrieve(boundary) == allowance && mine.Retrieve(boundary) == 0, "Retrieval awards stored ore once");
                    if (tier > 1)
                    {
                        check(mine.Place(boundary + 100), "Replace on depleted-to-lower-tier spot");
                        check(mine.YieldAt(boundary + 100 + OceanTarget.Interval(tier - 1) - .001f) == 0, "Replacement starts fresh tick interval");
                        int expected = Enumerable.Range(1, tier - 1).Sum(t => t == 5 ? 60 : t * 10);
                        check(mine.YieldAt(100000) == expected && mine.ActualTier == 0, "Large time jump traverses every remaining tier");
                        check(mine.DisplayedTier == tier - 1, "Exhaustion remains secret");
                        mine.Retrieve(100000);
                        check(mine.Place(100001) && mine.YieldAt(200000) == 0, "Stale productive display permits wasted placement");
                        mine.Retrieve(200000); mine.Reveal(200000);
                    }
                    check(!mine.Place(200001), "Known depleted spots reject placement");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Ocean Interactables.unity");
            try
            {
                var tutorial = scene.GetRootGameObjects().Select(g => g.GetComponent<OceanTutorial>()).First(t => t != null);
                tutorial.BeginExcursion(); tutorial.Advance(10);
                check(tutorial.Stage == 1 && tutorial.Text.Contains("WASD") && tutorial.Text.Contains("mouse"), "Movement and aim are taught before prospecting");
                tutorial.transform.position += Vector3.right * .99f;
                tutorial.Advance(0); check(tutorial.Stage == 1, "Movement shorter than one unit does not advance");
                tutorial.transform.position += Vector3.right * .02f;
                tutorial.Advance(0); check(tutorial.Stage == 2 && tutorial.Text.Contains("Hold E"), "Moving one unit introduces prospecting");
                tutorial.OnScan(0); tutorial.Advance(10);
                check(tutorial.Stage == 2, "Empty scan does not advance tutorial");
                tutorial.OnScan(1); tutorial.Advance(0); check(tutorial.Stage == 3, "Discovery introduces placement");
                tutorial.OnPlaced(); tutorial.OnProduced(null); tutorial.OnRetrieved(); tutorial.OnPlaced();
                tutorial.Advance(4.99f); check(tutorial.Stage == 3, "Reading time gates early milestone");
                tutorial.Advance(.02f); check(tutorial.Stage == 4, "Buffered placement advances after reading");
                tutorial.Advance(5); check(tutorial.Stage == 5, "Buffered production advances");
                tutorial.Advance(5); check(tutorial.Stage == 6, "Positive retrieval advances");
                tutorial.Advance(5); check(tutorial.Stage == 7, "Placement after retrieval advances");
                tutorial.Advance(29.99f); check(tutorial.Stage == 7, "Overnight warning lasts thirty seconds");
                tutorial.Advance(.02f); check(tutorial.Stage == 8 && tutorial.Text.Contains("cyan diamond"), "Return guidance explains submarine marker and interaction");
                tutorial.Advance(30); check(tutorial.Stage == 9 && tutorial.Text == "" && tutorial.IsMiningComplete, "Tutorial finishes and permits return");
                tutorial.EndExcursion(); check(tutorial.Stage == 0 && !tutorial.IsMiningComplete, "Submarine entry clears tutorial");
                tutorial.BeginExcursion(); tutorial.transform.position += Vector3.right; tutorial.Advance(0);
                tutorial.OnScan(1); tutorial.OnPlaced(); tutorial.OnProduced(null);
                tutorial.Advance(0); tutorial.Advance(5); tutorial.Advance(5); tutorial.Advance(100);
                check(tutorial.Stage == 5, "New excursion resets positive retrieval milestone");
                tutorial.OnRetrieved(); tutorial.Advance(0); tutorial.Advance(100);
                check(tutorial.Stage == 6, "Placement before retrieval cannot satisfy replacement");
                tutorial.OnPlaced(); tutorial.Advance(0); check(tutorial.Stage == 7, "Fresh placement satisfies replacement");
                var player = tutorial.GetComponent<SpriteRenderer>();
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(player.sprite));
                check(player.color == Color.white && importer.spriteImportMode == SpriteImportMode.Single && importer.alphaIsTransparency, "Transparent untinted diver import");
                check(Mathf.Abs(1520f / importer.spritePixelsPerUnit * player.transform.lossyScale.y - 3f) < .001f, "Visible sprite height matches rebased player scale");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("Mining and tutorial checks passed: " + checks + " assertions.");
        }
    }
}
