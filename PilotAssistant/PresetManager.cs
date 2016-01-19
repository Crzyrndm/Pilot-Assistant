using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant
{
    using Utility;
    using Presets;
    using FlightModules;

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class PresetManager : MonoBehaviour
    {
        private static PresetManager instance;
        public static PresetManager Instance
        {
            get
            {
                return instance;
            }
        }

        public List<AsstPreset> AsstPresetList = new List<AsstPreset>();
        public AsstPreset activeAsstPreset = null;

        public Dictionary<string, CraftPreset> craftPresetDict = new Dictionary<string, CraftPreset>();

        const string presetsPath = "GameData/Pilot Assistant/Presets.cfg";
        const string defaultsPath = "GameData/Pilot Assistant/Defaults.cfg";

        const string craftDefaultName = "default";
        const string asstDefaultName = "default";

        const string craftPresetNodeName = "CraftPreset";
        const string asstPresetNodeName = "PIDPreset";

        const string craftAsstKey = "pilot";

        const string hdgCtrlr = "HdgBankController";
        const string yawCtrlr = "HdgYawController";
        const string aileronCtrlr = "AileronController";
        const string rudderCtrlr = "RudderController";
        const string altCtrlr = "AltitudeController";
        const string vertCtrlr = "AoAController";
        const string elevCtrlr = "ElevatorController";
        const string speedCtrlr = "SpeedController";
        const string accelCtrlr = "AccelController";

        const string pGain = "PGain";
        const string iGain = "IGain";
        const string dGain = "DGain";
        const string min = "MinOut";
        const string max = "MaxOut";
        const string iLower = "ClampLower";
        const string iUpper = "ClampUpper";
        const string scalar = "Scalar";
        const string ease = "Ease";

        public void Start()
        {
            instance = this;

            loadPresetsFromFile();
            DontDestroyOnLoad(this);
        }

        public void OnDestroy()
        {
            saveToFile();
        }

        public static void loadPresetsFromFile()
        {
            AsstPreset asstDefault = null;
            
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(asstPresetNodeName))
            {
                if (ReferenceEquals(node, null))
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(gainsArrayFromNode(node.GetNode(hdgCtrlr), AsstList.HdgBank));
                gains.Add(gainsArrayFromNode(node.GetNode(yawCtrlr), AsstList.BankToYaw));
                gains.Add(gainsArrayFromNode(node.GetNode(aileronCtrlr), AsstList.Aileron));
                gains.Add(gainsArrayFromNode(node.GetNode(rudderCtrlr), AsstList.Rudder));
                gains.Add(gainsArrayFromNode(node.GetNode(altCtrlr), AsstList.Altitude));
                gains.Add(gainsArrayFromNode(node.GetNode(vertCtrlr), AsstList.VertSpeed));
                gains.Add(gainsArrayFromNode(node.GetNode(elevCtrlr), AsstList.Elevator));
                gains.Add(gainsArrayFromNode(node.GetNode(speedCtrlr), AsstList.Speed));
                gains.Add(gainsArrayFromNode(node.GetNode(accelCtrlr), AsstList.Acceleration));

                string name = node.GetValue("name");
                if (name == asstDefaultName)
                    asstDefault = new AsstPreset(gains, name);
                else if (!instance.AsstPresetList.Any(p => p.name == name))
                    instance.AsstPresetList.Add(new AsstPreset(gains, name));
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(craftPresetNodeName))
            {
                if (ReferenceEquals(node, null) || instance.craftPresetDict.ContainsKey(node.GetValue("name")))
                    continue;

                string name = node.GetValue("name");
                if (name == craftDefaultName)
                    instance.craftPresetDict.Add(craftDefaultName, new CraftPreset(craftDefaultName, asstDefault));
                else
                {
                    CraftPreset cP = new CraftPreset(name,
                                            instance.AsstPresetList.FirstOrDefault(p => p.name == node.GetValue(craftAsstKey)));
                    instance.craftPresetDict.Add(cP.Name, cP);
                }
            }
        }

        public static void saveToFile()
        {
            ConfigNode node = new ConfigNode();
            node.AddValue("dummy", "do not delete me");
            foreach (AsstPreset p in instance.AsstPresetList)
            {
                node.AddNode(AsstPresetNode(p));
            }
            foreach (KeyValuePair<string, CraftPreset> cP in instance.craftPresetDict)
            {
                if (ReferenceEquals(cP.Value, null) || cP.Key == craftDefaultName || cP.Value.Dead)
                    continue;
                node.AddNode(CraftNode(cP.Value));
            }
            
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + presetsPath);
        }

        public static void saveDefaults()
        {
            ConfigNode node = new ConfigNode();
            CraftPreset cP = instance.craftPresetDict[craftDefaultName];

            if (!ReferenceEquals(cP.AsstPreset, null))
                node.AddNode(AsstPresetNode(cP.AsstPreset));

            node.AddNode(CraftNode(cP));
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + defaultsPath);
        }

        public static void updateDefaults()
        {
            instance.craftPresetDict[craftDefaultName].AsstPreset.PIDGains = instance.activeAsstPreset.PIDGains;

            saveDefaults();
        }

        public static double[] gainsArrayFromNode(ConfigNode node, AsstList type)
        {
            if (ReferenceEquals(node, null))
                return defaultControllerGains(type);
            
            double[] gains = new double[9];
            double.TryParse(node.GetValue(pGain), out gains[0]);
            double.TryParse(node.GetValue(iGain), out gains[1]);
            double.TryParse(node.GetValue(dGain), out gains[2]);
            double.TryParse(node.GetValue(min), out gains[3]);
            double.TryParse(node.GetValue(max), out gains[4]);
            double.TryParse(node.GetValue(iLower), out gains[5]);
            double.TryParse(node.GetValue(iUpper), out gains[6]);
            double.TryParse(node.GetValue(scalar), out gains[7]);
            double.TryParse(node.GetValue(ease), out gains[8]);

            return gains;
        }

        public static ConfigNode gainsArrayToNode(string name, int index, AsstPreset preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue(pGain, preset.PIDGains[index][0]);
            node.AddValue(iGain, preset.PIDGains[index][1]);
            node.AddValue(dGain, preset.PIDGains[index][2]);
            node.AddValue(min, preset.PIDGains[index][3]);
            node.AddValue(max, preset.PIDGains[index][4]);
            node.AddValue(iLower, preset.PIDGains[index][5]);
            node.AddValue(iUpper, preset.PIDGains[index][6]);
            node.AddValue(scalar, preset.PIDGains[index][7]);
            node.AddValue(ease, preset.PIDGains[index][8]);
            return node;
        }

        public static ConfigNode AsstPresetNode(AsstPreset preset)
        {
            ConfigNode node = new ConfigNode(asstPresetNodeName);
            node.AddValue("name", preset.name);
            node.AddNode(gainsArrayToNode(hdgCtrlr, (int)AsstList.HdgBank, preset));
            node.AddNode(gainsArrayToNode(yawCtrlr, (int)AsstList.BankToYaw, preset));
            node.AddNode(gainsArrayToNode(aileronCtrlr, (int)AsstList.Aileron, preset));
            node.AddNode(gainsArrayToNode(rudderCtrlr, (int)AsstList.Rudder, preset));
            node.AddNode(gainsArrayToNode(altCtrlr, (int)AsstList.Altitude, preset));
            node.AddNode(gainsArrayToNode(vertCtrlr, (int)AsstList.VertSpeed, preset));
            node.AddNode(gainsArrayToNode(elevCtrlr, (int)AsstList.Elevator, preset));
            node.AddNode(gainsArrayToNode(speedCtrlr, (int)AsstList.Speed, preset));
            node.AddNode(gainsArrayToNode(accelCtrlr, (int)AsstList.Acceleration, preset));

            return node;
        }

        public static double[] defaultControllerGains(AsstList type)
        {
            switch(type)
            {
                case AsstList.HdgBank:
                    return PilotAssistant.defaultHdgBankGains;
                case AsstList.BankToYaw:
                    return PilotAssistant.defaultBankToYawGains;
                case AsstList.Aileron:
                    return PilotAssistant.defaultAileronGains;
                case AsstList.Rudder:
                    return PilotAssistant.defaultRudderGains;
                case AsstList.Altitude:
                    return PilotAssistant.defaultAltitudeGains;
                case AsstList.VertSpeed:
                    return PilotAssistant.defaultVSpeedGains;
                case AsstList.Elevator:
                    return PilotAssistant.defaultElevatorGains;
                case AsstList.Speed:
                    return PilotAssistant.defaultSpeedGains;
                case AsstList.Acceleration:
                    return PilotAssistant.defaultAccelGains;
                default:
                    return PilotAssistant.defaultAileronGains;
            }
        }

        public static ConfigNode CraftNode(CraftPreset preset)
        {
            ConfigNode node = new ConfigNode(craftPresetNodeName);
            if (!string.IsNullOrEmpty(preset.Name))
            {
                node.AddValue("name", preset.Name);
                if (!ReferenceEquals(preset.AsstPreset, null) && !string.IsNullOrEmpty(preset.AsstPreset.name))
                    node.AddValue(craftAsstKey, preset.AsstPreset.name);
            }

            return node;
        }

        #region AsstPreset
        public static void newAsstPreset(ref string name, Asst_PID_Controller[] controllers, Vessel v)
        {
            if (string.IsNullOrEmpty(name))
                return;

            string tempName = name;
            if (Instance.AsstPresetList.Any(p => p.name == tempName))
            {
                GeneralUI.postMessage("Failed to add preset with duplicate name");
                return;
            }
            AsstPreset newPreset = new AsstPreset(controllers, name);
            updateCraftPreset(newPreset, v);
            Instance.AsstPresetList.Add(newPreset);
            Instance.activeAsstPreset = PresetManager.Instance.AsstPresetList.Last();
            saveToFile();
            name = "";
        }

        public static void loadAsstPreset(AsstPreset p, PilotAssistant instance)
        {
            Asst_PID_Controller[] c = instance.controllers;
            for (int i = 0; i < 8; i++)
            {
                c[i].k_proportional = p.PIDGains[i][0];
                c[i].k_integral = p.PIDGains[i][1];
                c[i].k_derivative = p.PIDGains[i][2];
                c[i].outMin = p.PIDGains[i][3];
                c[i].outMax = p.PIDGains[i][4];
                c[i].integralClampLower = p.PIDGains[i][5];
                c[i].integralClampUpper = p.PIDGains[i][6];
                c[i].Scalar = p.PIDGains[i][7];
                c[i].Easing = p.PIDGains[i][8];
            }
            
            Instance.activeAsstPreset = p;
            GeneralUI.postMessage("Loaded preset " + p.name);
            
            if (Instance.activeAsstPreset != Instance.craftPresetDict[craftDefaultName].AsstPreset)
                updateCraftPreset(Instance.activeAsstPreset, instance.vesModule.vesselRef);
            saveToFile();
        }

        public static void updateAsstPreset(PilotAssistant instance)
        {
            Instance.activeAsstPreset.Update(instance.controllers);
            saveToFile();
        }

        public static void deleteAsstPreset(AsstPreset p)
        {
            GeneralUI.postMessage("Deleted preset " + p.name);
            if (Instance.activeAsstPreset == p)
                Instance.activeAsstPreset = null;
            Instance.AsstPresetList.Remove(p);

            p = null;

            saveToFile();
        }
        #endregion

        #region Craft Presets
        // called on vessel load
        public static void loadCraftAsstPreset(PilotAssistant instance)
        {
            CraftPreset cp;
            if (Instance.craftPresetDict.TryGetValue(FlightGlobals.ActiveVessel.vesselName, out cp) && !ReferenceEquals(cp.AsstPreset, null))
                loadAsstPreset(cp.AsstPreset, instance);
            else
                loadAsstPreset(Instance.craftPresetDict[craftDefaultName].AsstPreset, instance);
        }

        public static void initDefaultPresets(AsstPreset p)
        {
            initDefaultPresets();
            if (ReferenceEquals(Instance.craftPresetDict[craftDefaultName].AsstPreset, null))
                Instance.craftPresetDict[craftDefaultName].AsstPreset = p;
            PresetManager.saveDefaults();
        }

        public static void initDefaultPresets()
        {
            if (!Instance.craftPresetDict.ContainsKey(craftDefaultName))
                Instance.craftPresetDict.Add(craftDefaultName, new CraftPreset(craftDefaultName, null));
        }

        public static void updateCraftPreset(AsstPreset p, Vessel v)
        {
            initCraftPreset(v);
            Instance.craftPresetDict[v.vesselName].AsstPreset = p;
        }

        public static void initCraftPreset(Vessel v)
        {
            if (!Instance.craftPresetDict.ContainsKey(v.vesselName))
            {
                Instance.craftPresetDict.Add(v.vesselName,
                                                new CraftPreset(v.vesselName,
                                                    Instance.activeAsstPreset == Instance.craftPresetDict[craftDefaultName].AsstPreset ? null : Instance.activeAsstPreset));
            }
        }
        #endregion
    }
}
