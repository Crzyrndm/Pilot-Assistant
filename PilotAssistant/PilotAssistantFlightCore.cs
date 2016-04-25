using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PilotAssistant
{
    using FlightModules;
    using Toolbar;
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
        public static bool bHideUI = false;
        public static ConfigNode config;
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

        public List<AsstVesselModule> controlledVessels = new List<AsstVesselModule>();
        public int selectedVesselIndex = 0;

        public void Awake()
        {
            instance = this;
            bHideUI = false;

            config = ConfigNode.Load(KSP.IO.IOUtils.GetFilePathFor(this.GetType(), "Settings.cfg"));
            if (ReferenceEquals(config, null))
                config = new ConfigNode(string.Empty);

            bUseStockToolbar = config.TryGetValue("UseStockToolbar", true);

            blizMenuTexPath = config.TryGetValue("blizMenuIcon", "Pilot Assistant/Icon/BlizzyIcon");
            blizAsstTexPath = config.TryGetValue("blizAsstIcon", "Pilot Assistant/Icon/BlizzyIcon");
            blizSSASTexPath = config.TryGetValue("blizSSASIcon", "Pilot Assistant/Icon/BlizzyIcon");
            blizSASTexPath = config.TryGetValue("blizSASIcon", "Pilot Assistant/Icon/BlizzyIcon");

            if (!bUseStockToolbar && ToolbarManager.ToolbarAvailable)
                ToolbarMod.Instance.Awake();
            else
                AppLauncherFlight.Awake();
        }

        public void Start()
        {
            BindingManager.Instance.Start();
            LoadConfig();

            // don't put these in awake or they trigger on loading the vessel and everything gets wierd
            GameEvents.onHideUI.Add(hideUI);
            GameEvents.onShowUI.Add(showUI);
        }

        public void addVessel(AsstVesselModule avm)
        {
            controlledVessels.Add(avm);
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
                controlledVessels.RemoveAt(selectedVesselIndex);
                selectedVesselIndex = 0;
            }
        }

        public void LoadConfig()
        {
            try
            {
                config = ConfigNode.Load(KSP.IO.IOUtils.GetFilePathFor(this.GetType(), "Settings.cfg"));
                if (ReferenceEquals(config, null))
                    config = new ConfigNode(string.Empty);
                if (!ReferenceEquals(config, null))
                {
                    showTooltips = config.TryGetValue("AsstTooltips", true);

                    PilotAssistant.doublesided = config.TryGetValue("AsstDoublesided", false);
                    PilotAssistant.showPIDLimits = config.TryGetValue("AsstLimits", false);
                    PilotAssistant.showControlSurfaces = config.TryGetValue("AsstControlSurfaces", false);
                    PilotAssistant.maxHdgScrollbarHeight = config.TryGetValue("maxHdgHeight", 55);
                    PilotAssistant.maxVertScrollbarHeight = config.TryGetValue("maxVertHeight", 55);
                    PilotAssistant.maxThrtScrollbarHeight = config.TryGetValue("maxThrtHeight", 55);

                    // windows
                    PilotAssistant.window = config.TryGetValue("AsstWindow", new Rect(300, 300, 0, 0));
                    BindingManager.Instance.windowRect = config.TryGetValue("BindingWindow", new Rect(300, 50, 0, 0));
                    window = config.TryGetValue("AppWindow", new Rect(100, 300, 0, 0));

                    // key bindings
                    BindingManager.bindings[(int)bindingIndex.Pause].primaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("pausePrimary", KeyCode.Tab.ToString()));
                    BindingManager.bindings[(int)bindingIndex.Pause].secondaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("pauseSecondary", KeyCode.None.ToString()));
                    BindingManager.bindings[(int)bindingIndex.HdgTgl].primaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("hdgTglPrimary", KeyCode.Keypad9.ToString()));
                    BindingManager.bindings[(int)bindingIndex.HdgTgl].secondaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("hdgTglSecondary", KeyCode.LeftAlt.ToString()));
                    BindingManager.bindings[(int)bindingIndex.VertTgl].primaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("vertTglPrimary", KeyCode.Keypad6.ToString()));
                    BindingManager.bindings[(int)bindingIndex.VertTgl].secondaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("vertTglSecondary", KeyCode.LeftAlt.ToString()));
                    BindingManager.bindings[(int)bindingIndex.ThrtTgl].primaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("thrtTglPrimary", KeyCode.Keypad3.ToString()));
                    BindingManager.bindings[(int)bindingIndex.ThrtTgl].secondaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("thrtTglSecondary", KeyCode.LeftAlt.ToString()));
                    BindingManager.bindings[(int)bindingIndex.ArmSSAS].primaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("SSASArmPrimary", GameSettings.SAS_TOGGLE.primary.ToString()));
                    BindingManager.bindings[(int)bindingIndex.ArmSSAS].secondaryBindingCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), config.TryGetValue("SSASArmSecondary", KeyCode.LeftAlt.ToString()));
                }
                else
                {
                    Debug.LogError("Pilot Assistant: Failed to create settings node");
                }
            }
            catch
            {
                Debug.LogError("Pilot Assistant: Config load failed");
            }
        }

        public void SaveConfig()
        {
            try
            {
                if (ReferenceEquals(config, null))
                    config = new ConfigNode(string.Empty);
                if (!ReferenceEquals(config, null))
                {
                    config.SetValue("AsstTooltips", showTooltips.ToString(), true);
                    config.SetValue("UseStockToolbar", bUseStockToolbar.ToString(), true);

                    config.SetValue("AsstDoublesided", PilotAssistant.doublesided.ToString(), true);
                    config.SetValue("AsstLimits", PilotAssistant.showPIDLimits.ToString(), true);
                    config.SetValue("AsstControlSurfaces", PilotAssistant.showControlSurfaces.ToString(), true);
                    config.SetValue("maxHdgHeight", PilotAssistant.maxHdgScrollbarHeight.ToString(), true);
                    config.SetValue("maxVertHeight", PilotAssistant.maxVertScrollbarHeight.ToString(), true);
                    config.SetValue("maxThrtHeight", PilotAssistant.maxThrtScrollbarHeight.ToString(), true);

                    // window rects
                    config.SetValue("AsstWindow", PilotAssistant.window.ToString(), true);
                    config.SetValue("AppWindow", window.ToString(), true);
                    config.SetValue("BindingWindow", BindingManager.Instance.windowRect.ToString(), true);

                    // key bindings
                    config.SetValue("pausePrimary", BindingManager.bindings[(int)bindingIndex.Pause].primaryBindingCode.ToString(), true);
                    config.SetValue("pauseSecondary", BindingManager.bindings[(int)bindingIndex.Pause].secondaryBindingCode.ToString(), true);
                    config.SetValue("hdgTglPrimary", BindingManager.bindings[(int)bindingIndex.HdgTgl].primaryBindingCode.ToString(), true);
                    config.SetValue("hdgTglSecondary", BindingManager.bindings[(int)bindingIndex.HdgTgl].secondaryBindingCode.ToString(), true);
                    config.SetValue("vertTglPrimary", BindingManager.bindings[(int)bindingIndex.VertTgl].primaryBindingCode.ToString(), true);
                    config.SetValue("vertTglSecondary", BindingManager.bindings[(int)bindingIndex.VertTgl].secondaryBindingCode.ToString(), true);
                    config.SetValue("thrtTglPrimary", BindingManager.bindings[(int)bindingIndex.ThrtTgl].primaryBindingCode.ToString(), true);
                    config.SetValue("thrtTglSecondary", BindingManager.bindings[(int)bindingIndex.ThrtTgl].secondaryBindingCode.ToString(), true);
                    config.SetValue("SSASArmPrimary", BindingManager.bindings[(int)bindingIndex.ArmSSAS].primaryBindingCode.ToString(), true);
                    config.SetValue("SSASArmSecondary", BindingManager.bindings[(int)bindingIndex.ArmSSAS].secondaryBindingCode.ToString(), true);

                    // bliz toolbar icons
                    config.SetValue("blizMenuIcon", blizMenuTexPath, true);
                    config.SetValue("blizAsstIcon", blizAsstTexPath, true);
                    config.SetValue("blizSSASIcon", blizSSASTexPath, true);
                    config.SetValue("blizSASIcon", blizSASTexPath, true);

                    Directory.CreateDirectory(KSP.IO.IOUtils.GetFilePathFor(this.GetType(), string.Empty));
                    config.Save(KSP.IO.IOUtils.GetFilePathFor(this.GetType(), "Settings.cfg"));
                }
            }
            catch
            {
                Debug.LogError("Pilot Assistant save failed");
            }
        }

        public void OnGUI()
        {
            if (ReferenceEquals(GeneralUI.UISkin, null))
                GeneralUI.customSkin();
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
                window = GUILayout.Window(0984653, window, optionsWindow, string.Empty, GUILayout.Width(60), GUILayout.Height(0));
        }

        private void optionsWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), string.Empty))
                bDisplayOptions = false;

            if (GUILayout.Button("Update Defaults"))
                PresetManager.Instance.updateDefaultAsstPreset(controlledVessels[selectedVesselIndex].vesselAsst.activePreset);
            if (controlledVessels.Count > 1)
            {
                GUILayout.Box(string.Empty, GUILayout.Height(10));
                for (int i = 0; i < controlledVessels.Count; i++)
                {
                    if (controlledVessels[i].vesselRef.isActiveVessel)
                        GUI.backgroundColor = Color.green;
                    bool tmp = GUILayout.Toggle(i == selectedVesselIndex, controlledVessels[i].vesselRef.vesselName, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(120));
                    if (tmp)
                        selectedVesselIndex = i;
                    GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
                }
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
