using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant
{
    /* Flight core calls Unity functions of all flight scene classes. This improves control over execution order
     * which has previously been a slight annoyance.
     * 
     * It also simplifies management of event subscriptions and the like
     * */

    using Utility;

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

        public void Awake()
        {
            instance = this;
        }

        public void Start()
        {
            FlightData.thisVessel = FlightGlobals.ActiveVessel;

            PilotAssistant.Instance.Start();
            SurfSAS.Instance.Start();
            Stock_SAS.Instance.Start();
            BindingManager.Instance.Start();
            AppLauncherFlight.Instance.Start();
            
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
            // SAS and RSAS need to be handled in a coroutine to ensure they have been initialised.
        }

        public void LoadConfig()
        {
            try
            {
                if (config == null)
                {
                    config = KSP.IO.PluginConfiguration.CreateForType<AppLauncherFlight>();
                    config.load();
                }

                showTooltips = config.GetValue("AsstTooltips", true);

                PilotAssistant.Instance.doublesided = config.GetValue("AsstDoublesided", false);
                PilotAssistant.Instance.showPresets = config.GetValue("AsstPresetWindow", false);
                PilotAssistant.Instance.showPIDLimits = config.GetValue("AsstLimits", false);
                PilotAssistant.Instance.showControlSurfaces = config.GetValue("AsstControlSurfaces", false);
                PilotAssistant.Instance.maxHdgScrollbarHeight = config.GetValue("maxHdgHeight", 55f);
                PilotAssistant.Instance.maxVertScrollbarHeight = config.GetValue("maxVertHeight", 55f);
                PilotAssistant.Instance.maxThrtScrollbarHeight = config.GetValue("maxThrtHeight", 55f);

                // windows
                PilotAssistant.Instance.window = config.GetValue("AsstWindow", new Rect(300, 300, 0, 0));
                SurfSAS.Instance.SSASwindow = config.GetValue("SSASWindow", new Rect(500, 300, 0, 0));
                Stock_SAS.Instance.StockSASwindow = config.GetValue("SASWindow", new Rect(500, 300, 0, 0));
                BindingManager.Instance.windowRect = config.GetValue("BindingWindow", new Rect(300, 50, 0, 0));
                AppLauncherFlight.Instance.window = config.GetValue("AppWindow", new Rect(100, 300, 0, 0));

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
            AppLauncherFlight.Instance.Draw();
            BindingManager.Instance.Draw();
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
                if (config == null)
                {
                    config = KSP.IO.PluginConfiguration.CreateForType<AppLauncherFlight>();
                    config.load();
                }

                config["AsstTooltips"] = showTooltips;
                
                config["AsstDoublesided"] = PilotAssistant.Instance.doublesided;
                config["AsstPresetWindow"] = PilotAssistant.Instance.showPresets;
                config["AsstLimits"] = PilotAssistant.Instance.showPIDLimits;
                config["AsstControlSurfaces"] = PilotAssistant.Instance.showControlSurfaces;
                config["maxHdgHeight"] = PilotAssistant.Instance.maxHdgScrollbarHeight;
                config["maxVertHeight"] = PilotAssistant.Instance.maxVertScrollbarHeight;
                config["maxThrtHeight"] = PilotAssistant.Instance.maxThrtScrollbarHeight;

                // window rect's
                config["AsstWindow"] = PilotAssistant.Instance.window;
                config["SSASWindow"] = SurfSAS.Instance.SSASwindow;
                config["SASWindow"] = Stock_SAS.Instance.StockSASwindow;
                config["AppWindow"] = AppLauncherFlight.Instance.window;
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
