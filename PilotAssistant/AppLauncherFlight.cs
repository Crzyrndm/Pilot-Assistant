using System;
using System.Collections;
using UnityEngine;

namespace PilotAssistant
{
    using Utility;

    public class AppLauncherFlight
    {
        static AppLauncherFlight instance;
        public static AppLauncherFlight Instance
        {
            get
            {
                if (instance == null)
                    instance = new AppLauncherFlight();
                return instance;
            }
        }
        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            PilotAssistantFlightCore.Instance.StartCoroutine(routine);
        }

        private ApplicationLauncherButton btnLauncher;
        private Rect window;

        public static bool bDisplayBindings = false;
        public static bool bDisplayOptions = false;
        public static bool bDisplayAssistant = false;
        public static bool bDisplaySAS = false;
        public static bool bDisplaySSAS = false;

        public static KSP.IO.PluginConfiguration config;
        public void Start()
        {
            LoadConfig();
        }

        void LoadConfig()
        {
            try
            {
                if (config == null)
                {
                    config = KSP.IO.PluginConfiguration.CreateForType<AppLauncherFlight>();
                    config.load();
                }

                PilotAssistant.Instance.window = config.GetValue("AsstWindow", new Rect(300, 300, 0, 0));
                PilotAssistant.Instance.doublesided = config.GetValue("AsstDoublesided", false);
                PilotAssistant.Instance.showTooltips = config.GetValue("AsstTooltips", true);
                PilotAssistant.Instance.showPresets = config.GetValue("AsstPresetWindow", false);
                PilotAssistant.Instance.showPIDLimits = config.GetValue("AsstLimits", false);
                PilotAssistant.Instance.showControlSurfaces = config.GetValue("AsstControlSurfaces", false);
                PilotAssistant.Instance.maxHdgScrollbarHeight = config.GetValue("maxHdgHeight", 55f);
                PilotAssistant.Instance.maxVertScrollbarHeight = config.GetValue("maxVertHeight", 55f);
                PilotAssistant.Instance.maxThrtScrollbarHeight = config.GetValue("maxThrtHeight", 55f);
                SurfSAS.Instance.SSASwindow = config.GetValue("SSASWindow", new Rect(500, 300, 0, 0));
                Stock_SAS.Instance.StockSASwindow = config.GetValue("SASWindow", new Rect(500, 300, 0, 0));
                BindingManager.Instance.windowRect = config.GetValue("BindingWindow", new Rect(300, 50, 0, 0));
                BindingManager.AsstPauseBinding.primaryBindingCode = config.GetValue("pausePrimary", KeyCode.Tab);
                BindingManager.AsstPauseBinding.secondaryBindingCode = config.GetValue("pauseSecondary", KeyCode.None);
                BindingManager.AsstHdgToggleBinding.primaryBindingCode = config.GetValue("hdgTglPrimary", KeyCode.Keypad9);
                BindingManager.AsstHdgToggleBinding.secondaryBindingCode = config.GetValue("hdgTglSecondary", KeyCode.LeftAlt);
                BindingManager.AsstVertToggleBinding.primaryBindingCode = config.GetValue("vertTglPrimary", KeyCode.Keypad6);
                BindingManager.AsstVertToggleBinding.secondaryBindingCode = config.GetValue("vertTglSecondary", KeyCode.LeftAlt);
                BindingManager.AsstThrtToggleBinding.primaryBindingCode = config.GetValue("thrtTglPrimary", KeyCode.Keypad3);
                BindingManager.AsstThrtToggleBinding.secondaryBindingCode = config.GetValue("thrtTglSecondary", KeyCode.LeftAlt);
                window = config.GetValue("AppWindow", new Rect(100, 300, 0, 0));

                OnAppLauncherReady();
            }
            catch
            {
                Debug.Log("Pilot Assistant: Config load failed");
            }
        }

        public void OnDestroy()
        {
            if (btnLauncher != null)
                ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
            btnLauncher = null;

            SaveConfig();
            instance = null;
        }

        public void SaveConfig()
        {
            try
            {
                if (config == null)
                {
                    config = KSP.IO.PluginConfiguration.CreateForType<AppLauncherFlight>();
                    config.load();
                }
                config["AsstWindow"] = PilotAssistant.Instance.window;
                config["AsstDoublesided"] = PilotAssistant.Instance.doublesided;
                config["AsstTooltips"] = PilotAssistant.Instance.showTooltips;
                config["AsstPresetWindow"] = PilotAssistant.Instance.showPresets;
                config["AsstLimits"] = PilotAssistant.Instance.showPIDLimits;
                config["AsstControlSurfaces"] = PilotAssistant.Instance.showControlSurfaces;
                config["maxHdgHeight"] = PilotAssistant.Instance.maxHdgScrollbarHeight;
                config["maxVertHeight"] = PilotAssistant.Instance.maxVertScrollbarHeight;
                config["maxThrtHeight"] = PilotAssistant.Instance.maxThrtScrollbarHeight;
                config["SSASWindow"] = SurfSAS.Instance.SSASwindow;
                config["SASWindow"] = Stock_SAS.Instance.StockSASwindow;
                config["AppWindow"] = window;
                config["BindingWindow"] = BindingManager.Instance.windowRect;
                config["pausePrimary"] = BindingManager.AsstPauseBinding.primaryBindingCode;
                config["pauseSecondary"] = BindingManager.AsstPauseBinding.secondaryBindingCode;
                config["hdgTglPrimary"] = BindingManager.AsstHdgToggleBinding.primaryBindingCode;
                config["hdgTglSecondary"] = BindingManager.AsstHdgToggleBinding.secondaryBindingCode;
                config["vertTglPrimary"] = BindingManager.AsstVertToggleBinding.primaryBindingCode;
                config["vertTglSecondary"] = BindingManager.AsstVertToggleBinding.secondaryBindingCode;
                config["thrtTglPrimary"] = BindingManager.AsstThrtToggleBinding.primaryBindingCode;
                config["thrtTglSecondary"] = BindingManager.AsstThrtToggleBinding.secondaryBindingCode;


                config.save();
            }
            catch
            {
                Debug.Log("Pilot Assistant: Save failed");
            }
        }

        private void OnAppLauncherReady()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(this.OnAppLauncherReady);
            btnLauncher = ApplicationLauncher.Instance.AddModApplication(OnToggleTrue, OnToggleFalse,
                                                                        null, null, null, null,
                                                                        ApplicationLauncher.AppScenes.ALWAYS,
                                                                        GameDatabase.Instance.GetTexture("Pilot Assistant/Icon/AppLauncherIcon", false));
        }

        void OnGameSceneChange(GameScenes scene)
        {
            ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
        }

        private void OnToggleTrue()
        {
            if (Input.GetMouseButtonUp(0))
                bDisplayOptions = true;
            else if (Input.GetMouseButtonUp(1))
            {
                bDisplayAssistant = true;
                if (bDisplayOptions)
                    btnLauncher.SetTrue(false);
                else
                    btnLauncher.SetFalse(false);
            }
        }

        private void OnToggleFalse()
        {
            if (Input.GetMouseButtonUp(0))
                bDisplayOptions = false;
            else if (Input.GetMouseButtonUp(1))
            {
                bDisplayAssistant = true;
                if (bDisplayOptions)
                    btnLauncher.SetTrue(false);
                else
                    btnLauncher.SetFalse(false);
            }
        }

        public void Draw()
        {
            if (bDisplayOptions)
                window = GUILayout.Window(0984653, window, optionsWindow, "", GUILayout.Width(0), GUILayout.Height(0));
        }

        private void optionsWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
                btnLauncher.SetFalse();

            bDisplayAssistant = GUILayout.Toggle(bDisplayAssistant, "Pilot Assistant", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            bDisplaySAS = GUILayout.Toggle(bDisplaySAS, "Stock SAS", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            bDisplaySSAS = GUILayout.Toggle(bDisplaySSAS, "SSAS", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            bDisplayBindings = GUILayout.Toggle(bDisplayBindings, "Keybindings", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);

            if (GUILayout.Button("Update Defaults"))
                PresetManager.updateDefaults();

            GUI.DragWindow();
        }
    }
}
