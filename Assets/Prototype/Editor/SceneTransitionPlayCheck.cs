using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DivingPrototype
{
    public static class SceneTransitionPlayCheck
    {
        [MenuItem("Diving Prototype/Check Scene Fades In Play Mode")]
        public static async void Run()
        {
            try { Debug.Log(await Main()); }
            catch(Exception error) { Debug.LogException(error); }
        }

        public static async Task WaitForIdle()
        {
            float deadline=Time.realtimeSinceStartup+10;
            while(SceneChanger.IsTransitioning)
            {
                if(Time.realtimeSinceStartup>deadline) throw new InvalidOperationException("Scene transition timed out");
                await Task.Delay(10);
            }
        }

        // Invokes the bed's existing Yarn command entry; does not play the full dialogue.
        public static async Task<object> Main()
        {
            FlashlightCheck.Require(EditorApplication.isPlaying,"Run in Play mode");
            using var state=new GameStateSnapshot();
            bool background=Application.runInBackground;float scale=Time.timeScale;
            int checks=0;
            Action<bool,string> check=(ok,label)=>{FlashlightCheck.Require(ok,label);checks++;};
            float alphaAtLoad=-1;
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> loaded=(scene,mode)=>{
                var cover=GameObject.Find("Scene Transition");
                alphaAtLoad=cover!=null?cover.GetComponent<UnityEngine.UI.Image>().color.a:-1;
                ScreenCapture.CaptureScreenshot("Temp/scene-fade-review/loaded-black.png");
            };
            try
            {
                Application.runInBackground=true;GameState.reset();
                System.IO.Directory.CreateDirectory("Temp/scene-fade-review");
                await SceneManager.LoadSceneAsync("Sleeping Quarters");await Task.Delay(100);
                SceneManager.sceneLoaded+=loaded;
                int frame=Time.frameCount;
                WorldInteractions.NewDay();
                float clock=Time.time;
                check(SceneChanger.IsTransitioning && Time.timeScale==0 && GameState.days==1,"Sleep starts a paused transition without changing the visible source day");
                WorldInteractions.NewDay();PauseManager.Pause();
                check(GameState.days==1 && !PauseManager.isPaused,"Repeated sleep and pause are blocked during the fade");
                await Task.Delay(100);
                var cover=GameObject.Find("Scene Transition");
                check(cover!=null && cover.GetComponent<UnityEngine.UI.Image>().color.a>0 && cover.GetComponent<UnityEngine.UI.Image>().color.a<1,"Source scene visibly fades toward black");
                check(Vector2.Distance(cover.GetComponent<RectTransform>().rect.size,new Vector2(Screen.width,Screen.height))<1,"Fade cover spans the whole output window");
                ScreenCapture.CaptureScreenshot("Temp/scene-fade-review/sleep-fade-out.png");
                check(SceneManager.GetActiveScene().name=="Sleeping Quarters" && Time.time==clock,"Source scene remains loaded and simulation frozen during fade-out");
                await WaitForIdle();
                check(alphaAtLoad==1 && SceneManager.GetActiveScene().name=="Ocean Interactables","Ocean is loaded only under a full black cover");
                check(Time.timeScale==1 && GameObject.Find("Scene Transition")==null,"Fade-in removes the blocker and restores time");
                var ocean=UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
                check(GameState.days==2 && ocean!=null && ocean.Hydrox.NormalizedFill>.99f && ocean.GetComponent<OceanTutorial>().Stage==1,"Ocean starts on day two with full gas and the tutorial");
                check(Time.frameCount>frame,"Player loop advances during the paused fade");
                GameState.days=4;GameState.credits=73;
                check(ocean.RequestEnterSubmarine() && ocean.ReturnConfirmationOpen,"Manual return still requires confirmation");
                check(ocean.ConfirmReturn() && SceneChanger.IsTransitioning && !ocean.ConfirmReturn(),"Confirmation starts exactly one return transition");
                check(Time.timeScale==0 && SceneManager.GetActiveScene().name=="Ocean Interactables","Return begins by fading the existing ocean");
                float deadline=Time.realtimeSinceStartup+10;
                while(SceneManager.GetActiveScene().name!="Result")
                {
                    if(Time.realtimeSinceStartup>deadline) throw new InvalidOperationException("Results did not load");
                    await Task.Delay(10);
                }
                check(alphaAtLoad==1 && SceneChanger.IsTransitioning,"Results first appears under the full black cover");
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                SceneChanger.ChangeScene("Interior");
                check(GameState.days==4 && GameState.credits==73 && SceneManager.GetActiveScene().name=="Result","Results cannot settle or change scenes during fade-in");
                await WaitForIdle();
                check(Time.timeScale==1 && GameObject.Find("Scene Transition")==null && SceneManager.GetActiveScene().name=="Result","Results becomes interactive after fade-in");
                ScreenCapture.CaptureScreenshot("Temp/scene-fade-review/results-after-fade.png");await Task.Delay(50);
                UnityEngine.Object.FindAnyObjectByType<ResultsText>().onLeave();
                check(GameState.days==5 && GameState.credits==23,"Normal settlement works after the transition");
                SceneChanger.ChangeSceneWithFade("Ocean Interactables");await Task.Delay(100);
                UnityEngine.Object.Destroy(GameObject.Find("Scene Transition"));await Task.Delay(400);
                check(!SceneChanger.IsTransitioning && Time.timeScale==1 && SceneManager.GetActiveScene().name=="Result","Destroying an interrupted fade restores time without a delayed scene load");
                Time.timeScale=.5f;WorldInteractions.NewDay();await Task.Delay(80);
                SceneChanger.ResetWrapper();await Task.Delay(400);
                check(!SceneChanger.IsTransitioning && Time.timeScale==1 && GameState.days==1 && GameState.credits==100 && SceneManager.GetActiveScene().name=="Result","New-game reset cancels a pending fade and restores normal time without advancing the day later");
                return new{checks,fadeSecondsEach=.35f,sleepCommand=true,confirmedReturn=true};
            }
            finally
            {
                SceneManager.sceneLoaded-=loaded;
                await WaitForIdle();
                Application.runInBackground=background;Time.timeScale=scale;
            }
        }
    }
}
