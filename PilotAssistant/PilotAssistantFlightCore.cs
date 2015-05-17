using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant
{
    /* Flight core calls Unity functions of all flight scene classes. This improves control over execution order
     * which has previously been a slight annoyance.
     * 
     * It also simplifies management of event subscriptions and the like and serves as a location for settings
     * and other common variables
     * */

    using Utility;
    using Toolbar;
    using FlightModules;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class PilotAssistantFlightCore : MonoBehaviour
    {
        static PilotAssistantFlightCore instance;
        public static PilotAssistantFlightCore Instance
        {
            get
            {
                return instance;
            }
        }

        public static bool showTooltips = true;
        bool bHideUI = false;
        public static KSP.IO.PluginConfiguration config;
        public Rect window;
        public bool bUseStockToolbar = true;

        public static bool bDisplayBindings = false;
        public static bool bDisplayOptions = false;
        public static bool bDisplayAssistant = false;
        public static bool bDisplaySAS = false;
        public static bool bDisplaySSAS = false;

        public void Awake()
        {
            instance = this;
            if (config == null)
            {
                config = KSP.IO.PluginConfiguration.CreateForType<PilotAssistantFlightCore>();
                config.load();
            }
            bUseStockToolbar = config.GetValue("UseStockToolbar", true);

            if (!bUseStockToolbar && ToolbarManager.ToolbarAvailable)
                ToolbarMod.Instance.Start();
            else
                AppLauncherFlight.Instance.Start();
        }

        public void Start()
        {
            FlightData.thisVessel = FlightGlobals.ActiveVessel;

            PilotAssistant.Instance.Start();
            SurfSAS.Instance.Start();
            Stock_SAS.Instance.Start();
            BindingManager.Instance.Start();
            
            FlightData.thisVessel.OnPreAutopilotUpdate += new FlightInputCallback(onPreAutoPilotUpdate);
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(onAutoPilotUpdate);
            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(onPostAutoPilotUpdate);

            // don't put these in awake or they trigger on loading the vessel
            GameEvents.onHideUI.Add(hideUI);
            GameEvents.onShowUI.Add(showUI);
            GameEvents.onVesselChange.Add(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Add(warpRateChanged);

            LoadConfig();

            PresetManager.loadCraftAsstPreset();
            PresetManager.loadCraftSSASPreset();
            // SAS and RSAS preset loading needs to be handled in a coroutine to ensure they have been initialised so they are handled by applicable classes
        }

        public void LoadConfig()
        {
            try
            {
                showTooltips = config.GetValue("AsstTooltips", true);

                PilotAssistant.Instance.doublesided = config.GetValue("AsstDoublesided", false);
                PilotAssistant.Instance.showPIDLimits = config.GetValue("AsstLimits", false);
                PilotAssistant.Instance.showControlSurfaces = config.GetValue("AsstControlSurfaces", false);
                PilotAssistant.Instance.maxHdgScrollbarHeight = float.Parse(config.GetValue("maxHdgHeight", "55"));
                PilotAssistant.Instance.maxVertScrollbarHeight = float.Parse(config.GetValue("maxVertHeight", "55"));
                PilotAssistant.Instance.maxThrtScrollbarHeight = float.Parse(config.GetValue("maxThrtHeight", "55"));

                // windows
                PilotAssistant.Instance.window = config.GetValue("AsstWindow", new Rect(300, 300, 0, 0));
                SurfSAS.Instance.SSASwindow = config.GetValue("SSASWindow", new Rect(500, 300, 0, 0));
                Stock_SAS.Instance.StockSASwindow = config.GetValue("SASWindow", new Rect(500, 300, 0, 0));
                BindingManager.Instance.windowRect = config.GetValue("BindingWindow", new Rect(300, 50, 0, 0));
                window = config.GetValue("AppWindow", new Rect(100, 300, 0, 0));

                // key bindings
                BindingManager.bindings[(int)bindingIndex.Pause].primaryBindingCode = config.GetValue("pausePrimary", KeyCode.Tab);
                BindingManager.bindings[(int)bindingIndex.Pause].secondaryBindingCode = config.GetValue("pauseSecondary", KeyCode.None);
                BindingManager.bindings[(int)bindingIndex.HdgTgl].primaryBindingCode = config.GetValue("hdgTglPrimary", KeyCode.Keypad9);
                BindingManager.bindings[(int)bindingIndex.HdgTgl].secondaryBindingCode = config.GetValue("hdgTglSecondary", KeyCode.LeftAlt);
                BindingManager.bindings[(int)bindingIndex.VertTgl].primaryBindingCode = config.GetValue("vertTglPrimary", KeyCode.Keypad6);
                BindingManager.bindings[(int)bindingIndex.VertTgl].secondaryBindingCode = config.GetValue("vertTglSecondary", KeyCode.LeftAlt);
                BindingManager.bindings[(int)bindingIndex.ThrtTgl].primaryBindingCode = config.GetValue("thrtTglPrimary", KeyCode.Keypad3);
                BindingManager.bindings[(int)bindingIndex.ThrtTgl].secondaryBindingCode = config.GetValue("thrtTglSecondary", KeyCode.LeftAlt);
            }
            catch
            {
                Debug.Log("Pilot Assistant: Config load failed");
            }
        }

        public void Update()
        {
            PilotAssistant.Instance.Update();
            SurfSAS.Instance.Update();
        }

        void vesselSwitch(Vessel v)
        {
            FlightData.thisVessel.OnPreAutopilotUpdate -= new FlightInputCallback(onPreAutoPilotUpdate);
            FlightData.thisVessel.OnAutopilotUpdate -= new FlightInputCallback(onAutoPilotUpdate);
            FlightData.thisVessel.OnPostAutopilotUpdate -= new FlightInputCallback(onPostAutoPilotUpdate);

            FlightData.thisVessel = v;

            FlightData.thisVessel.OnPreAutopilotUpdate += new FlightInputCallback(onPreAutoPilotUpdate);
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(onAutoPilotUpdate);
            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(onPostAutoPilotUpdate);

            PresetManager.loadCraftAsstPreset();
            PresetManager.loadCraftSASPreset();
            Stock_SAS.Instance.vesselSwitch();
        }

        void warpRateChanged()
        {
            PilotAssistant.Instance.warpHandler();
            SurfSAS.Instance.warpHandler();
        }

        //public void FixedUpdate()
        //{
        //}

        void onPreAutoPilotUpdate(FlightCtrlState state)
        {
            FlightData.updateAttitude();
        }

        void onAutoPilotUpdate(FlightCtrlState state)
        {
            SurfSAS.Instance.SurfaceSAS(state);
        }

        void onPostAutoPilotUpdate(FlightCtrlState state)
        {
            PilotAssistant.Instance.vesselController(state);
        }

        public void OnGUI()
        {
            if (bHideUI)
                return;

            GUI.skin = GeneralUI.UISkin;
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;

            PilotAssistant.Instance.drawGUI();
            SurfSAS.Instance.drawGUI();
            Stock_SAS.Instance.drawGUI();
            Draw();
            BindingManager.Instance.Draw();
        }

        public void Draw()
        {
            if (bDisplayOptions)
                window = GUILayout.Window(0984653, window, optionsWindow, "", GUILayout.Width(0), GUILayout.Height(0));
        }

        private void optionsWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
                bDisplayOptions = false;

            bDisplayAssistant = GUILayout.Toggle(bDisplayAssistant, "Pilot Assistant", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            bDisplaySAS = GUILayout.Toggle(bDisplaySAS, "Stock SAS", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            bDisplaySSAS = GUILayout.Toggle(bDisplaySSAS, "SSAS", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            bDisplayBindings = GUILayout.Toggle(bDisplayBindings, "Keybindings", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);

            if (GUILayout.Button("Update Defaults"))
                PresetManager.updateDefaults();

            GUI.DragWindow();
        }

        void hideUI()
        {
            bHideUI = true;
        }

        void showUI()
        {
            bHideUI = false;
        }

        public void OnDestroy()
        {
            SaveConfig();

            PilotAssistant.Instance.OnDestroy();
            SurfSAS.Instance.OnDestroy();
            Stock_SAS.Instance.OnDestroy();
            AppLauncherFlight.Instance.OnDestroy();
            ToolbarMod.Instance.OnDestroy();
            BindingManager.Instance.OnDestroy();

            GameEvents.onHideUI.Remove(hideUI);
            GameEvents.onShowUI.Remove(showUI);
            GameEvents.onVesselChange.Remove(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Remove(warpRateChanged);

            instance = null;
        }

        public void SaveConfig()
        {
            try
            {
                config["AsstTooltips"] = showTooltips;
                config["UseStockToolbar"] = bUseStockToolbar;
                
                config["AsstDoublesided"] = PilotAssistant.Instance.doublesided;
                config["AsstLimits"] = PilotAssistant.Instance.showPIDLimits;
                config["AsstControlSurfaces"] = PilotAssistant.Instance.showControlSurfaces;
                config["maxHdgHeight"] = PilotAssistant.Instance.maxHdgScrollbarHeight.ToString("0");
                config["maxVertHeight"] = PilotAssistant.Instance.maxVertScrollbarHeight.ToString("0");
                config["maxThrtHeight"] = PilotAssistant.Instance.maxThrtScrollbarHeight.ToString("0");

                // window rects
                config["AsstWindow"] = PilotAssistant.Instance.window;
                config["SSASWindow"] = SurfSAS.Instance.SSASwindow;
                config["SASWindow"] = Stock_SAS.Instance.StockSASwindow;
                config["AppWindow"] = window;
                config["BindingWindow"] = BindingManager.Instance.windowRect;

                // key bindings
                config["pausePrimary"] = BindingManager.bindings[(int)bindingIndex.Pause].primaryBindingCode;
                config["pauseSecondary"] = BindingManager.bindings[(int)bindingIndex.Pause].secondaryBindingCode;
                config["hdgTglPrimary"] = BindingManager.bindings[(int)bindingIndex.HdgTgl].primaryBindingCode;
                config["hdgTglSecondary"] = BindingManager.bindings[(int)bindingIndex.HdgTgl].secondaryBindingCode;
                config["vertTglPrimary"] = BindingManager.bindings[(int)bindingIndex.VertTgl].primaryBindingCode;
                config["vertTglSecondary"] = BindingManager.bindings[(int)bindingIndex.VertTgl].secondaryBindingCode;
                config["thrtTglPrimary"] = BindingManager.bindings[(int)bindingIndex.ThrtTgl].primaryBindingCode;
                config["thrtTglSecondary"] = BindingManager.bindings[(int)bindingIndex.ThrtTgl].secondaryBindingCode;


                config.save();
            }
            catch
            {
                Debug.Log("Pilot Assistant: Save failed");
            }
        }
    }
}
