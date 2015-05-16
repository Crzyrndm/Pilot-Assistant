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
            AppLauncherFlight.Instance.Start(); // must be the last to start as it loads settings and assigns it to the others
            
            FlightData.thisVessel.OnPreAutopilotUpdate += new FlightInputCallback(onPreAutoPilotUpdate);
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(onAutoPilotUpdate);
            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(onPostAutoPilotUpdate);

            GameEvents.onHideUI.Add(hideUI);
            GameEvents.onShowUI.Add(showUI);
            GameEvents.onVesselChange.Add(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Add(warpRateChanged);

            LoadConfig();
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
                BindingManager.bindingList[(int)bindingIndex.Pause].primaryBindingCode = config.GetValue("pausePrimary", KeyCode.Tab);
                BindingManager.bindingList[(int)bindingIndex.Pause].secondaryBindingCode = config.GetValue("pauseSecondary", KeyCode.None);
                BindingManager.bindingList[(int)bindingIndex.HdgTgl].primaryBindingCode = config.GetValue("hdgTglPrimary", KeyCode.Keypad9);
                BindingManager.bindingList[(int)bindingIndex.HdgTgl].secondaryBindingCode = config.GetValue("hdgTglSecondary", KeyCode.LeftAlt);
                BindingManager.bindingList[(int)bindingIndex.VertTgl].primaryBindingCode = config.GetValue("vertTglPrimary", KeyCode.Keypad6);
                BindingManager.bindingList[(int)bindingIndex.VertTgl].secondaryBindingCode = config.GetValue("vertTglSecondary", KeyCode.LeftAlt);
                BindingManager.bindingList[(int)bindingIndex.ThrtTgl].primaryBindingCode = config.GetValue("thrtTglPrimary", KeyCode.Keypad3);
                BindingManager.bindingList[(int)bindingIndex.ThrtTgl].secondaryBindingCode = config.GetValue("thrtTglSecondary", KeyCode.LeftAlt);
                AppLauncherFlight.Instance.window = config.GetValue("AppWindow", new Rect(100, 300, 0, 0));
            }
            catch (Exception ex)
            {
                Debug.Log(ex.Message);
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
            //if (FlightData.thisVessel == null)
            //    return;
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
            //if (FlightData.thisVessel == null)
            //    return;
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
                config["AppWindow"] = AppLauncherFlight.Instance.window;
                config["BindingWindow"] = BindingManager.Instance.windowRect;
                config["pausePrimary"] = BindingManager.bindingList[(int)bindingIndex.Pause].primaryBindingCode;
                config["pauseSecondary"] = BindingManager.bindingList[(int)bindingIndex.Pause].secondaryBindingCode;
                config["hdgTglPrimary"] = BindingManager.bindingList[(int)bindingIndex.HdgTgl].primaryBindingCode;
                config["hdgTglSecondary"] = BindingManager.bindingList[(int)bindingIndex.HdgTgl].secondaryBindingCode;
                config["vertTglPrimary"] = BindingManager.bindingList[(int)bindingIndex.VertTgl].primaryBindingCode;
                config["vertTglSecondary"] = BindingManager.bindingList[(int)bindingIndex.VertTgl].secondaryBindingCode;
                config["thrtTglPrimary"] = BindingManager.bindingList[(int)bindingIndex.ThrtTgl].primaryBindingCode;
                config["thrtTglSecondary"] = BindingManager.bindingList[(int)bindingIndex.ThrtTgl].secondaryBindingCode;


                config.save();
            }
            catch
            {
                Debug.Log("Pilot Assistant: Save failed");
            }
        }
    }
}
