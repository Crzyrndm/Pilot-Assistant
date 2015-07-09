using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PilotAssistant
{
    /* Flight core calls Unity functions of all flight scene classes. This improves control over execution order
     * which has previously been a slight annoyance.
     * 
     * It also simplifies management of event subscriptions and the like and serves as a location for settings
     * and other common variables
     */

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
        public static bool bHideUI = false;
        public static KSP.IO.PluginConfiguration config;
        public Rect window;
        public bool bUseStockToolbar = true;

        public static bool bDisplayBindings = false;
        public static bool bDisplayOptions = false;
        public static bool bDisplayAssistant = false;
        public static bool bDisplaySAS = false;
        public static bool bDisplaySSAS = false;

        public static bool calculateDirection = true;

        public string blizMenuTexPath;
        public string blizAsstTexPath;
        public string blizSSASTexPath;
        public string blizSASTexPath;

        List<AsstVesselModule> controlledVessels = new List<AsstVesselModule>();

        public void Awake()
        {
            instance = this;
            bHideUI = false;
            LoadConfig();
            
            if (!bUseStockToolbar && ToolbarManager.ToolbarAvailable)
                ToolbarMod.Instance.Awake();
            else
                AppLauncherFlight.Awake();
        }

        public void Start()
        {
            BindingManager.Instance.Start();

            // don't put these in awake or they trigger on loading the vessel and everything gets wierd
            GameEvents.onHideUI.Add(hideUI);
            GameEvents.onShowUI.Add(showUI);
        }

        public void addVessel(AsstVesselModule avm)
        {
            controlledVessels.Add(avm);
            selectedVesselIndex = controlledVessels.Count - 1;
        }

        public void removeVessel(AsstVesselModule avm)
        {
            if (avm.vesselRef != controlledVessels[selectedVesselIndex].vesselRef)
            {
                Vessel ves = controlledVessels[selectedVesselIndex].vesselRef;
                controlledVessels.Remove(avm);
                selectedVesselIndex = controlledVessels.FindIndex(vm => vm.vesselRef == ves);
            }
            else
            {
                controlledVessels.Remove(avm);
                selectedVesselIndex = 0;
            }
        }

        public void LoadConfig()
        {
            try
            {
                if (config == null)
                {
                    config = KSP.IO.PluginConfiguration.CreateForType<PilotAssistantFlightCore>();
                }
                config.load();

                showTooltips = config.GetValue<bool>("AsstTooltips", true);

                bUseStockToolbar = config.GetValue<bool>("UseStockToolbar", true);

                blizMenuTexPath = config.GetValue("blizMenuIcon", "Pilot Assistant/Icon/BlizzyIcon");
                blizAsstTexPath = config.GetValue("blizAsstIcon", "Pilot Assistant/Icon/BlizzyIcon");
                blizSSASTexPath = config.GetValue("blizSSASIcon", "Pilot Assistant/Icon/BlizzyIcon");
                blizSASTexPath = config.GetValue("blizSASIcon", "Pilot Assistant/Icon/BlizzyIcon");

                PilotAssistant.doublesided = config.GetValue<bool>("AsstDoublesided", false);
                PilotAssistant.showPIDLimits = config.GetValue<bool>("AsstLimits", false);
                PilotAssistant.showControlSurfaces = config.GetValue<bool>("AsstControlSurfaces", false);
                PilotAssistant.maxHdgScrollbarHeight = config.GetValue<float>("maxHdgHeight", 55);
                PilotAssistant.maxVertScrollbarHeight = config.GetValue<float>("maxVertHeight", 55);
                PilotAssistant.maxThrtScrollbarHeight = config.GetValue<float>("maxThrtHeight", 55);

                // windows
                PilotAssistant.window = config.GetValue<Rect>("AsstWindow", new Rect(300, 300, 0, 0));
                SurfSAS.SSASwindow = config.GetValue<Rect>("SSASWindow", new Rect(500, 300, 0, 0));
                Stock_SAS.StockSASwindow = config.GetValue<Rect>("SASWindow", new Rect(500, 300, 0, 0));
                BindingManager.Instance.windowRect = config.GetValue<Rect>("BindingWindow", new Rect(300, 50, 0, 0));
                window = config.GetValue<Rect>("AppWindow", new Rect(100, 300, 0, 0));

                // key bindings
                BindingManager.bindings[(int)bindingIndex.Pause].primaryBindingCode = config.GetValue<KeyCode>("pausePrimary", KeyCode.Tab);
                BindingManager.bindings[(int)bindingIndex.Pause].secondaryBindingCode = config.GetValue<KeyCode>("pauseSecondary", KeyCode.None);
                BindingManager.bindings[(int)bindingIndex.HdgTgl].primaryBindingCode = config.GetValue<KeyCode>("hdgTglPrimary", KeyCode.Keypad9);
                BindingManager.bindings[(int)bindingIndex.HdgTgl].secondaryBindingCode = config.GetValue<KeyCode>("hdgTglSecondary", KeyCode.LeftAlt);
                BindingManager.bindings[(int)bindingIndex.VertTgl].primaryBindingCode = config.GetValue<KeyCode>("vertTglPrimary", KeyCode.Keypad6);
                BindingManager.bindings[(int)bindingIndex.VertTgl].secondaryBindingCode = config.GetValue<KeyCode>("vertTglSecondary", KeyCode.LeftAlt);
                BindingManager.bindings[(int)bindingIndex.ThrtTgl].primaryBindingCode = config.GetValue<KeyCode>("thrtTglPrimary", KeyCode.Keypad3);
                BindingManager.bindings[(int)bindingIndex.ThrtTgl].secondaryBindingCode = config.GetValue<KeyCode>("thrtTglSecondary", KeyCode.LeftAlt);
                BindingManager.bindings[(int)bindingIndex.ArmSSAS].primaryBindingCode = config.GetValue<KeyCode>("SSASArmPrimary", GameSettings.SAS_TOGGLE.primary);
                BindingManager.bindings[(int)bindingIndex.ArmSSAS].secondaryBindingCode = config.GetValue<KeyCode>("SSASArmSecondary", KeyCode.LeftAlt);
            }
            catch
            {
                Debug.Log("Pilot Assistant: Config load failed");
            }
        }

        public void SaveConfig()
        {
            try
            {
                config["AsstTooltips"] = showTooltips;
                config["UseStockToolbar"] = bUseStockToolbar;

                config["AsstDoublesided"] = PilotAssistant.doublesided;
                config["AsstLimits"] = PilotAssistant.showPIDLimits;
                config["AsstControlSurfaces"] = PilotAssistant.showControlSurfaces;
                config["maxHdgHeight"] = PilotAssistant.maxHdgScrollbarHeight;
                config["maxVertHeight"] = PilotAssistant.maxVertScrollbarHeight;
                config["maxThrtHeight"] = PilotAssistant.maxThrtScrollbarHeight;

                // window rects
                config["AsstWindow"] = PilotAssistant.window;
                config["SSASWindow"] = SurfSAS.SSASwindow;
                config["SASWindow"] = Stock_SAS.StockSASwindow;
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
                config["SSASArmPrimary"] = BindingManager.bindings[(int)bindingIndex.ArmSSAS].primaryBindingCode;
                config["SSASArmSecondary"] = BindingManager.bindings[(int)bindingIndex.ArmSSAS].secondaryBindingCode;

                // bliz toolbar icons
                config["blizMenuIcon"] = blizMenuTexPath;
                config["blizAsstIcon"] = blizAsstTexPath;
                config["blizSSASIcon"] = blizSSASTexPath;
                config["blizSASIcon"] = blizSASTexPath;

                config.save();
            }
            catch
            {
                Debug.Log("Pilot Assistant: Save failed");
            }
        }

        public void OnGUI()
        {
            if (bHideUI)
                return;

            GUI.skin = GeneralUI.UISkin;
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            Draw();
            BindingManager.Instance.Draw();
        }

        public void Draw()
        {
            if (bDisplayOptions)
                window = GUILayout.Window(0984653, window, optionsWindow, "", GUILayout.Width(0), GUILayout.Height(0));
        }

        int selectedVesselIndex = 0;
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
            if (controlledVessels.Count > 1)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < controlledVessels.Count; i++)
                {
                    if (controlledVessels[i].vesselRef.isActiveVessel)
                        GUI.backgroundColor = Color.green;
                    bool tmp = GUILayout.Toggle(i == selectedVesselIndex, i.ToString(), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
                    if (tmp)
                        selectedVesselIndex = i;
                    GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
                }
                GUILayout.EndHorizontal();
            }
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
            if (Toolbar.ToolbarManager.ToolbarAvailable && !bUseStockToolbar)
                ToolbarMod.Instance.OnDestroy();
            BindingManager.Instance.OnDestroy();

            GameEvents.onHideUI.Remove(hideUI);
            GameEvents.onShowUI.Remove(showUI);
            
            PresetManager.saveToFile();
            instance = null;
        }
    }
}
