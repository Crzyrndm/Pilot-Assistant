using System;
using System.Collections;
using UnityEngine;

namespace PilotAssistant
{
    using Utility;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AppLauncherFlight : MonoBehaviour
    {
        private ApplicationLauncherButton btnLauncher;
        private Rect window;

        public static bool bDisplayOptions = false;
        public static bool bDisplayAssistant = false;
        public static bool bDisplaySAS = false;

        public static KSP.IO.PluginConfiguration config;
        void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(this.OnAppLauncherReady);
            window = new Rect(10, 50, 30, 30);

            RenderingManager.AddToPostDrawQueue(5, Draw);

            StartCoroutine(LoadConfig());
        }

        IEnumerator LoadConfig()
        {
            yield return new WaitForEndOfFrame(); // Make sure PA and SSAS are running first

            config = KSP.IO.PluginConfiguration.CreateForType<AppLauncherFlight>();
            config.load();

            PilotAssistant.Instance.window = config.GetValue("AsstWindow", new Rect());
            SurfSAS.Instance.SASwindow = config.GetValue("SASWindow", new Rect());
        }

        void OnDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(5, Draw);

            if (btnLauncher != null)
                ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
            btnLauncher = null;

            SaveConfig();
        }

        public void SaveConfig()
        {
            config.SetValue("AsstWindow", PilotAssistant.Instance.window);
            config.SetValue("SASWindow", SurfSAS.Instance.SASwindow);
            config.save();
        }

        private void OnAppLauncherReady()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(this.OnAppLauncherReady);
            btnLauncher = ApplicationLauncher.Instance.AddModApplication(OnToggleTrue, OnToggleFalse,
                                                                        null, null, null, null,
                                                                        ApplicationLauncher.AppScenes.ALWAYS,
                                                                        GameDatabase.Instance.GetTexture("Pilot Assistant/Icons/AppLauncherIcon", false));
        }

        void OnGameSceneChange(GameScenes scene)
        {
            ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
        }

        private void OnToggleTrue()
        {
            bDisplayOptions = true;
        }

        private void OnToggleFalse()
        {
            bDisplayOptions = false;
        }

        private void Draw()
        {
            GeneralUI.Styles();
            GUI.skin = GeneralUI.UISkin;

            if (bDisplayOptions)
                window = GUILayout.Window(0984653, window, optionsWindow, "", GUILayout.Width(0), GUILayout.Height(0));
        }

        private void optionsWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
                btnLauncher.SetFalse();

            bool temp = GUILayout.Toggle(bDisplayAssistant, "Pilot Assistant", Utility.GeneralUI.toggleButton);
            if (temp != bDisplayAssistant)
            {
                bDisplayAssistant = temp;
            }
            temp = GUILayout.Toggle(bDisplaySAS, "SAS Systems", Utility.GeneralUI.toggleButton);
            if (temp != bDisplaySAS)
            {
                bDisplaySAS = temp;
            }
            if (GUILayout.Button("Update Defaults"))
            {
                PresetManager.updateDefaults();
            }

            GUI.DragWindow();
        }
    }
}
