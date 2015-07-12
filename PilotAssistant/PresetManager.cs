using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
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
        public List<SASPreset> SASPresetList = new List<SASPreset>();
        public List<RSASPreset> RSASPresetList = new List<RSASPreset>();
        public List<SSASPreset> SSASPresetList = new List<SSASPreset>();

        public AsstPreset activeAsstPreset = null;
        public SSASPreset activeSSASPreset = null;
        public SASPreset activeSASPreset = null;
        public RSASPreset activeRSASPreset = null;

        public Dictionary<string, CraftPreset> craftPresetDict = new Dictionary<string, CraftPreset>();

        const string presetsPath = "GameData/Pilot Assistant/Presets.cfg";
        const string defaultsPath = "GameData/Pilot Assistant/Defaults.cfg";

        const string craftDefaultName = "default";
        const string asstDefaultName = "default";
        const string ssasDefaultName = "SSAS";
        const string SASDefaultName = "stock";
        const string RSASDefaultName = "RSAS";

        const string craftPresetNodeName = "CraftPreset";
        const string asstPresetNodeName = "PIDPreset";
        const string sasPresetNodeName = "SASPreset";
        const string rsasPresetNodeName = "RSASPreset";
        const string ssasPresetNodeName = "SSASPreset";

        const string craftAsstKey = "pilot";
        const string craftSSASKey = "ssas";
        const string craftSASKey = "stock";
        const string craftRSASKey = "rsas";

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
        const string delay = "Delay";

        double[] defaultPresetPitchGains = { 0.15, 0.0, 0.06, 3, 20 }; // Kp/i/d, scalar, delay
        double[] defaultPresetRollGains = { 0.1, 0.0, 0.06, 3, 20 };
        double[] defaultPresetHdgGains = { 0.15, 0.0, 0.06, 3, 20 };

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

        public void OnGUI()
        {
            // create the GUISkin
            if (GeneralUI.UISkin == null)
                GeneralUI.customSkin();
        }

        public static void loadPresetsFromFile()
        {
            AsstPreset asstDefault = null;
            SASPreset SASDefault = null;
            SSASPreset SSASDefault = null;
            RSASPreset RSASDefault = null;
            
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(asstPresetNodeName))
            {
                if (node == null)
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerGains(node.GetNode(hdgCtrlr), AsstList.HdgBank));
                gains.Add(controllerGains(node.GetNode(yawCtrlr), AsstList.BankToYaw));
                gains.Add(controllerGains(node.GetNode(aileronCtrlr), AsstList.Aileron));
                gains.Add(controllerGains(node.GetNode(rudderCtrlr), AsstList.Rudder));
                gains.Add(controllerGains(node.GetNode(altCtrlr), AsstList.Altitude));
                gains.Add(controllerGains(node.GetNode(vertCtrlr), AsstList.VertSpeed));
                gains.Add(controllerGains(node.GetNode(elevCtrlr), AsstList.Elevator));
                gains.Add(controllerGains(node.GetNode(speedCtrlr), AsstList.Speed));
                gains.Add(controllerGains(node.GetNode(accelCtrlr), AsstList.Acceleration));

                if (node.GetValue("name") == asstDefaultName)
                    asstDefault = new AsstPreset(gains, node.GetValue("name"));
                else if (!instance.AsstPresetList.Any(p => p.name == node.GetValue("name")))
                    instance.AsstPresetList.Add(new AsstPreset(gains, node.GetValue("name")));
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(sasPresetNodeName))
            {
                if (node == null || node.GetValue("stock") == "false")
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerSASGains(node.GetNode(elevCtrlr), SASList.Pitch));
                gains.Add(controllerSASGains(node.GetNode(aileronCtrlr), SASList.Bank));
                gains.Add(controllerSASGains(node.GetNode(rudderCtrlr), SASList.Hdg));

                if (node.GetValue("name") == SASDefaultName)
                    SASDefault = new SASPreset(gains, node.GetValue("name"));
                else if (!instance.SASPresetList.Any(p=> p.name == node.GetValue("name")))
                    instance.SASPresetList.Add(new SASPreset(gains, node.GetValue("name")));
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(ssasPresetNodeName))
            {
                if (node == null)
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerSASGains(node.GetNode(elevCtrlr), SASList.Pitch));
                gains.Add(controllerSASGains(node.GetNode(aileronCtrlr), SASList.Bank));
                gains.Add(controllerSASGains(node.GetNode(rudderCtrlr), SASList.Hdg));
                
                if (node.GetValue("name") == ssasDefaultName)
                    SSASDefault = new SSASPreset(gains, node.GetValue("name"));
                else if (!instance.SSASPresetList.Any(p => p.name == node.GetValue("name")))
                    instance.SSASPresetList.Add(new SSASPreset(gains, node.GetValue("name")));
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(rsasPresetNodeName))
            {
                if (node == null)
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerSASGains(node.GetNode(elevCtrlr), SASList.Pitch));
                gains.Add(controllerSASGains(node.GetNode(aileronCtrlr), SASList.Bank));
                gains.Add(controllerSASGains(node.GetNode(rudderCtrlr), SASList.Hdg));
                if (node.GetValue("name") == RSASDefaultName)
                    RSASDefault = new RSASPreset(gains, node.GetValue("name"));
                else if (!instance.RSASPresetList.Any(p => p.name == node.GetValue("name")))
                    instance.RSASPresetList.Add(new RSASPreset(gains, node.GetValue("name")));
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(craftPresetNodeName))
            {
                if (node == null || instance.craftPresetDict.ContainsKey(node.GetValue("name")))
                    continue;

                if (node.GetValue("name") == craftDefaultName)
                    instance.craftPresetDict.Add(craftDefaultName, new CraftPreset(craftDefaultName, asstDefault, SSASDefault, SASDefault, RSASDefault));
                else
                {
                    CraftPreset cP = new CraftPreset(node.GetValue("name"),
                                            instance.AsstPresetList.FirstOrDefault(p => p.name == node.GetValue(craftAsstKey)),
                                            instance.SSASPresetList.FirstOrDefault(p => p.name == node.GetValue(craftSSASKey)),
                                            instance.SASPresetList.FirstOrDefault(p => p.name == node.GetValue(craftSASKey)),
                                            instance.RSASPresetList.FirstOrDefault(p => p.name == node.GetValue(craftRSASKey)));

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
            foreach (SASPreset p in instance.SASPresetList)
            {
                node.AddNode(SASPresetNode(p));
            }
            foreach (SSASPreset p in instance.SSASPresetList)
            {
                node.AddNode(SSASPresetNode(p));
            }
            foreach (RSASPreset p in instance.RSASPresetList)
            {
                node.AddNode(RSASPresetNode(p));
            }
            foreach (KeyValuePair<string, CraftPreset> cP in instance.craftPresetDict)
            {
                if (cP.Value == null || cP.Key == craftDefaultName || cP.Value.Dead)
                    continue;
                node.AddNode(CraftNode(cP.Value));
            }
            
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + presetsPath);
        }

        public static void saveDefaults()
        {
            ConfigNode node = new ConfigNode();
            CraftPreset cP = instance.craftPresetDict[craftDefaultName];

            if (cP.AsstPreset != null)
                node.AddNode(AsstPresetNode(cP.AsstPreset));
            if (cP.SSASPreset != null)
                node.AddNode(SSASPresetNode(cP.SSASPreset));
            if (cP.SASPreset != null)
                node.AddNode(SASPresetNode(cP.SASPreset));
            if (cP.RSASPreset != null)
                node.AddNode(RSASPresetNode(cP.RSASPreset));

            node.AddNode(CraftNode(cP));
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + defaultsPath);
        }

        public static void updateDefaults()
        {
            instance.craftPresetDict[craftDefaultName].AsstPreset.PIDGains = instance.activeAsstPreset.PIDGains;
            instance.craftPresetDict[craftDefaultName].SSASPreset.PIDGains = instance.activeSSASPreset.PIDGains;
            instance.craftPresetDict[craftDefaultName].SASPreset.PIDGains = instance.activeSASPreset.PIDGains;
            instance.craftPresetDict[craftDefaultName].RSASPreset.PIDGains = instance.activeRSASPreset.PIDGains;

            saveDefaults();
        }

        public static double[] controllerGains(ConfigNode node, AsstList type)
        {
            double[] gains = new double[9];

            if (node == null)
                return defaultControllerGains(type);

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

        public static double[] controllerSASGains(ConfigNode node, SASList type)
        {
            double[] gains = new double[5];

            if (node == null)
                return defaultControllerGains(type);

            double.TryParse(node.GetValue(pGain), out gains[0]);
            double.TryParse(node.GetValue(iGain), out gains[1]);
            double.TryParse(node.GetValue(dGain), out gains[2]);
            double.TryParse(node.GetValue(scalar), out gains[3]);
            double.TryParse(node.GetValue(delay), out gains[4]);

            return gains;
        }

        public static double[] defaultControllerGains(SASList type)
        {
            switch (type)
            {
                case SASList.Pitch:
                    return Instance.defaultPresetPitchGains;
                case SASList.Bank:
                    return Instance.defaultPresetRollGains;
                case SASList.Hdg:
                    return Instance.defaultPresetHdgGains;
                default:
                    return Instance.defaultPresetPitchGains;
            }
        }

        public static ConfigNode AsstPresetNode(AsstPreset preset)
        {
            ConfigNode node = new ConfigNode(asstPresetNodeName);
            node.AddValue("name", preset.name);
            node.AddNode(PIDnode(hdgCtrlr, (int)AsstList.HdgBank, preset));
            node.AddNode(PIDnode(yawCtrlr, (int)AsstList.BankToYaw, preset));
            node.AddNode(PIDnode(aileronCtrlr, (int)AsstList.Aileron, preset));
            node.AddNode(PIDnode(rudderCtrlr, (int)AsstList.Rudder, preset));
            node.AddNode(PIDnode(altCtrlr, (int)AsstList.Altitude, preset));
            node.AddNode(PIDnode(vertCtrlr, (int)AsstList.VertSpeed, preset));
            node.AddNode(PIDnode(elevCtrlr, (int)AsstList.Elevator, preset));
            node.AddNode(PIDnode(speedCtrlr, (int)AsstList.Speed, preset));
            node.AddNode(PIDnode(accelCtrlr, (int)AsstList.Acceleration, preset));

            return node;
        }

        public static ConfigNode SASPresetNode(SASPreset preset)
        {
            ConfigNode node = new ConfigNode(sasPresetNodeName);
            node.AddValue("name", preset.name);
            node.AddNode(PIDnode(aileronCtrlr, (int)SASList.Bank, preset));
            node.AddNode(PIDnode(rudderCtrlr, (int)SASList.Hdg, preset));
            node.AddNode(PIDnode(elevCtrlr, (int)SASList.Pitch, preset));

            return node;
        }

        public static ConfigNode SSASPresetNode(SSASPreset preset)
        {
            ConfigNode node = new ConfigNode(ssasPresetNodeName);
            node.AddValue("name", preset.name);
            node.AddNode(PIDnode(aileronCtrlr, (int)SASList.Bank, preset));
            node.AddNode(PIDnode(rudderCtrlr, (int)SASList.Hdg, preset));
            node.AddNode(PIDnode(elevCtrlr, (int)SASList.Pitch, preset));

            return node;
        }

        public static ConfigNode RSASPresetNode(RSASPreset preset)
        {
            ConfigNode node = new ConfigNode(rsasPresetNodeName);
            node.AddValue("name", preset.name);
            node.AddNode(PIDnode(aileronCtrlr, (int)SASList.Bank, preset));
            node.AddNode(PIDnode(rudderCtrlr, (int)SASList.Hdg, preset));
            node.AddNode(PIDnode(elevCtrlr, (int)SASList.Pitch, preset));

            return node;
        }

        public static ConfigNode PIDnode(string name, int index, AsstPreset preset)
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

        public static ConfigNode PIDnode(string name, int index, SASPreset preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue(pGain, preset.PIDGains[index, 0]);
            node.AddValue(iGain, preset.PIDGains[index, 1]);
            node.AddValue(dGain, preset.PIDGains[index, 2]);
            node.AddValue(scalar, preset.PIDGains[index, 3]);
            return node;
        }

        public static ConfigNode PIDnode(string name, int index, SSASPreset preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue(pGain, preset.PIDGains[index, 0]);
            node.AddValue(iGain, preset.PIDGains[index, 1]);
            node.AddValue(dGain, preset.PIDGains[index, 2]);
            node.AddValue(scalar, preset.PIDGains[index, 3]);
            return node;
        }

        public static ConfigNode PIDnode(string name, int index, RSASPreset preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue(pGain, preset.PIDGains[index, 0]);
            node.AddValue(iGain, preset.PIDGains[index, 1]);
            node.AddValue(dGain, preset.PIDGains[index, 2]);
            return node;
        }

        public static ConfigNode CraftNode(CraftPreset preset)
        {
            ConfigNode node = new ConfigNode(craftPresetNodeName);
            if (!string.IsNullOrEmpty(preset.Name))
            {
                node.AddValue("name", preset.Name);
                if (preset.AsstPreset != null && !string.IsNullOrEmpty(preset.AsstPreset.name))
                    node.AddValue(craftAsstKey, preset.AsstPreset.name);
                if (preset.SSASPreset != null && !string.IsNullOrEmpty(preset.SSASPreset.name))
                    node.AddValue(craftSSASKey, preset.SSASPreset.name);
                if (preset.SASPreset != null && !string.IsNullOrEmpty(preset.SASPreset.name))
                    node.AddValue(craftSASKey, preset.SASPreset.name);
                if (preset.RSASPreset != null && !string.IsNullOrEmpty(preset.RSASPreset.name))
                    node.AddValue(craftRSASKey, preset.RSASPreset.name);
            }

            return node;
        }

        #region AsstPreset
        public static void newAsstPreset(ref string name, AsstController[] controllers, Vessel v)
        {
            if (name == "")
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
            AsstController[] c = instance.controllers;
            for (int i = 0; i < 8; i++)
            {
                c[i].PGain = p.PIDGains[i][0];
                c[i].IGain = p.PIDGains[i][1];
                c[i].DGain = p.PIDGains[i][2];
                c[i].OutMin = p.PIDGains[i][3];
                c[i].OutMax = p.PIDGains[i][4];
                c[i].ClampLower = p.PIDGains[i][5];
                c[i].ClampUpper = p.PIDGains[i][6];
                c[i].Scalar = p.PIDGains[i][7];
                c[i].Easing = p.PIDGains[i][8];
            }
            
            Instance.activeAsstPreset = p;
            GeneralUI.postMessage("Loaded preset " + p.name);
            
            if (Instance.activeAsstPreset != Instance.craftPresetDict[craftDefaultName].AsstPreset)
                updateCraftPreset(Instance.activeAsstPreset, instance.vesRef.vesselRef);
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

        #region SAS Preset
        public static void newSASPreset(ref string name, Vessel v)
        {
            if (string.IsNullOrEmpty(name))
                return;

            string nameTest = name;
            if (Instance.SASPresetList.Any(p => p.name == nameTest))
                return;

            SASPreset newPreset = new SASPreset(v.Autopilot.SAS, name);
            Instance.SASPresetList.Add(newPreset);
            updateCraftPreset(newPreset, v);
            Instance.activeSASPreset = Instance.SASPresetList.Last();

            saveToFile();
            name = "";
        }

        public static void loadSASPreset(SASPreset p, Stock_SAS instance)
        {
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedPitch.kp = p.PIDGains[(int)SASList.Pitch, 0];
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedPitch.ki = p.PIDGains[(int)SASList.Pitch, 1];
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedPitch.kd = p.PIDGains[(int)SASList.Pitch, 2];
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedPitch.clamp = p.PIDGains[(int)SASList.Pitch, 3];

            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedRoll.kp = p.PIDGains[(int)SASList.Bank, 0];
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedRoll.ki = p.PIDGains[(int)SASList.Bank, 1];
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedRoll.kd = p.PIDGains[(int)SASList.Bank, 2];
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedRoll.clamp = p.PIDGains[(int)SASList.Bank, 3];

            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedYaw.kp = p.PIDGains[(int)SASList.Hdg, 0];
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedYaw.ki = p.PIDGains[(int)SASList.Hdg, 1];
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedYaw.kd = p.PIDGains[(int)SASList.Hdg, 2];
            instance.vesRef.vesselRef.Autopilot.SAS.pidLockedYaw.clamp = p.PIDGains[(int)SASList.Hdg, 3];

            Instance.activeSASPreset = p;

            if (Instance.activeSASPreset != Instance.craftPresetDict[craftDefaultName].SASPreset)
                updateCraftPreset(p, instance.vesRef.vesselRef);
            saveToFile();
        }

        public static void UpdateSASPreset(Stock_SAS instance)
        {
            Instance.activeSASPreset.Update(instance.vesRef.vesselRef.Autopilot.SAS);
            saveToFile();
        }

        public static void deleteSASPreset(SASPreset p)
        {
            GeneralUI.postMessage("Deleted preset " + p.name);
            if (Instance.activeSASPreset == p)
                Instance.activeSASPreset = null;
            Instance.SASPresetList.Remove(p);

            foreach (KeyValuePair<string, CraftPreset> cp in instance.craftPresetDict)
            {
                if (cp.Value != null && cp.Value.SASPreset == p)
                    cp.Value.SASPreset = null;
            }
            p = null;
            saveToFile();
        }
        #endregion

        #region SSAS Preset
        public static void newSSASPreset(ref string name, PIDErrorController[] controllers, Vessel v)
        {
            if (string.IsNullOrEmpty(name))
                return;

            string nameTest = name;
            if (Instance.SSASPresetList.Any(p => p.name == nameTest))
                return;

            SSASPreset newPreset = new SSASPreset(controllers, name);
            Instance.SSASPresetList.Add(newPreset);
            updateCraftPreset(newPreset, v);
            Instance.activeSSASPreset = Instance.SSASPresetList.Last();
            saveToFile();
            name = "";
        }

        public static void loadSSASPreset(SSASPreset p, SurfSAS instance)
        {
            PIDErrorController[] c = instance.SASControllers;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                c[(int)s].PGain = p.PIDGains[(int)s, 0];
                c[(int)s].IGain = p.PIDGains[(int)s, 1];
                c[(int)s].DGain = p.PIDGains[(int)s, 2];
                c[(int)s].Scalar = p.PIDGains[(int)s, 3];
            }

            Instance.activeSSASPreset = p;

            if (Instance.activeSSASPreset != Instance.craftPresetDict[craftDefaultName].SSASPreset)
                updateCraftPreset(p, instance.vesRef.vesselRef);
            saveToFile();
        }

        public static void UpdateSSASPreset(SurfSAS instance)
        {
            Instance.activeSSASPreset.Update(instance.SASControllers);
            saveToFile();
        }

        public static void deleteSSASPreset(SSASPreset p)
        {
            GeneralUI.postMessage("Deleted preset " + p.name);
            if (Instance.activeSSASPreset == p)
                Instance.activeSSASPreset = null;
            Instance.SSASPresetList.Remove(p);

            foreach (KeyValuePair<string, CraftPreset> cp in instance.craftPresetDict)
            {
                if (cp.Value != null && cp.Value.SSASPreset == p)
                    cp.Value.SASPreset = null;
            }

            p = null;
            saveToFile();
        }
        #endregion

        #region RSAS Preset
        public static void newRSASPreset(ref string name, Vessel v)
        {
            if (string.IsNullOrEmpty(name))
                return;

            string nameTest = name;
            if (Instance.RSASPresetList.Any(p => p.name == nameTest))
                return;

            RSASPreset newPreset = new RSASPreset(v.Autopilot.RSAS, name);
            Instance.RSASPresetList.Add(newPreset);
            updateCraftPreset(newPreset, v);
            Instance.activeRSASPreset = Instance.RSASPresetList.Last();
            
            saveToFile();
            name = "";
        }

        public static void loadRSASPreset(RSASPreset p, Stock_SAS instance)
        {
            instance.vesRef.vesselRef.Autopilot.RSAS.pidPitch.ReinitializePIDsOnly((float)p.PIDGains[(int)SASList.Pitch, 0], (float)p.PIDGains[(int)SASList.Pitch, 1], (float)p.PIDGains[(int)SASList.Pitch, 2]);
            instance.vesRef.vesselRef.Autopilot.RSAS.pidRoll.ReinitializePIDsOnly((float)p.PIDGains[(int)SASList.Bank, 0], (float)p.PIDGains[(int)SASList.Bank, 1], (float)p.PIDGains[(int)SASList.Bank, 2]);
            instance.vesRef.vesselRef.Autopilot.RSAS.pidYaw.ReinitializePIDsOnly((float)p.PIDGains[(int)SASList.Hdg, 0], (float)p.PIDGains[(int)SASList.Hdg, 1], (float)p.PIDGains[(int)SASList.Hdg, 2]);

            Instance.activeRSASPreset = p;

            if (Instance.activeRSASPreset != Instance.craftPresetDict[craftDefaultName].RSASPreset)
                updateCraftPreset(p, instance.vesRef.vesselRef);
            saveToFile();
        }

        public static void UpdateRSASPreset(Stock_SAS instance)
        {
            Instance.activeRSASPreset.Update(instance.vesRef.vesselRef.Autopilot.RSAS);
            saveToFile();
        }

        public static void deleteRSASPreset(RSASPreset p)
        {
            GeneralUI.postMessage("Deleted preset " + p.name);
            if (Instance.activeRSASPreset == p)
                Instance.activeRSASPreset = null;
            Instance.RSASPresetList.Remove(p);

            foreach (KeyValuePair<string, CraftPreset> cp in instance.craftPresetDict)
            {
                if (cp.Value != null && cp.Value.RSASPreset == p)
                    cp.Value.SASPreset = null;
            }

            p = null;
            saveToFile();
        }
        #endregion

        #region Craft Presets
        // called on vessel load
        public static void loadCraftAsstPreset(PilotAssistant instance)
        {
            if (Instance.craftPresetDict.ContainsKey(FlightGlobals.ActiveVessel.vesselName) && Instance.craftPresetDict[FlightGlobals.ActiveVessel.vesselName].AsstPreset != null)
                loadAsstPreset(Instance.craftPresetDict[FlightGlobals.ActiveVessel.vesselName].AsstPreset, instance);
            else
                loadAsstPreset(Instance.craftPresetDict[craftDefaultName].AsstPreset, instance);
        }

        public static void loadCraftSSASPreset(SurfSAS instance)
        {
            if (Instance.craftPresetDict.ContainsKey(FlightGlobals.ActiveVessel.vesselName) && Instance.craftPresetDict[FlightGlobals.ActiveVessel.vesselName].SSASPreset != null)
                loadSSASPreset(Instance.craftPresetDict[FlightGlobals.ActiveVessel.vesselName].SSASPreset, instance);
            else
                loadSSASPreset(Instance.craftPresetDict[craftDefaultName].SSASPreset, instance);
        }

        // called on vessel load
        public static void loadCraftSASPreset(Stock_SAS instance)
        {
            if (Instance.craftPresetDict.ContainsKey(FlightGlobals.ActiveVessel.vesselName))
            {
                if (Instance.craftPresetDict[FlightGlobals.ActiveVessel.vesselName].SASPreset != null)
                    loadSASPreset(Instance.craftPresetDict[FlightGlobals.ActiveVessel.vesselName].SASPreset, instance);
                else
                    loadSASPreset(Instance.craftPresetDict[craftDefaultName].SASPreset, instance);

                if (Instance.craftPresetDict[FlightGlobals.ActiveVessel.vesselName].RSASPreset != null)
                    loadRSASPreset(Instance.craftPresetDict[FlightGlobals.ActiveVessel.vesselName].RSASPreset, instance);
                else
                    loadRSASPreset(Instance.craftPresetDict[craftDefaultName].RSASPreset, instance);
            }
            else
            {
                loadSASPreset(Instance.craftPresetDict[craftDefaultName].SASPreset, instance);
                loadRSASPreset(Instance.craftPresetDict[craftDefaultName].RSASPreset, instance);
            }
        }

        public static void initDefaultPresets(AsstPreset p)
        {
            initDefaultPresets();
            if (Instance.craftPresetDict[craftDefaultName].AsstPreset == null)
                Instance.craftPresetDict[craftDefaultName].AsstPreset = p;
            PresetManager.saveDefaults();
        }

        public static void initDefaultPresets(SASPreset p)
        {
            initDefaultPresets();
            if (Instance.craftPresetDict[craftDefaultName].SASPreset == null)
                Instance.craftPresetDict[craftDefaultName].SASPreset = p;
            PresetManager.saveDefaults();
        }

        public static void initDefaultPresets(SSASPreset p)
        {
            initDefaultPresets();
            if (Instance.craftPresetDict[craftDefaultName].SSASPreset == null)
                Instance.craftPresetDict[craftDefaultName].SSASPreset = p;
            PresetManager.saveDefaults();
        }

        public static void initDefaultPresets(RSASPreset p)
        {
            initDefaultPresets();
            if (Instance.craftPresetDict[craftDefaultName].RSASPreset == null)
                Instance.craftPresetDict[craftDefaultName].RSASPreset = p;
            PresetManager.saveDefaults();
        }

        public static void initDefaultPresets()
        {
            if (!Instance.craftPresetDict.ContainsKey("default"))
                Instance.craftPresetDict.Add("default", new CraftPreset("default", null, null, null, null));
        }

        public static void updateCraftPreset(AsstPreset p, Vessel v)
        {
            initCraftPreset(v);
            Instance.craftPresetDict[v.vesselName].AsstPreset = p;
        }

        public static void updateCraftPreset(SASPreset p, Vessel v)
        {
            initCraftPreset(v);
            Instance.craftPresetDict[v.vesselName].SASPreset = p;
        }

        public static void updateCraftPreset(SSASPreset p, Vessel v)
        {
            initCraftPreset(v);
            Instance.craftPresetDict[v.vesselName].SSASPreset = p;
        }

        public static void updateCraftPreset(RSASPreset p, Vessel v)
        {
            initCraftPreset(v);
            Instance.craftPresetDict[v.vesselName].RSASPreset = p;
        }

        public static void initCraftPreset(Vessel v)
        {
            if (!Instance.craftPresetDict.ContainsKey(v.vesselName))
            {
                Instance.craftPresetDict.Add(v.vesselName,
                                                new CraftPreset(v.vesselName,
                                                    Instance.activeAsstPreset == Instance.craftPresetDict[craftDefaultName].AsstPreset ? null : Instance.activeAsstPreset,
                                                    Instance.activeSSASPreset == Instance.craftPresetDict[craftDefaultName].SSASPreset ? null : Instance.activeSSASPreset,
                                                    Instance.activeSASPreset == Instance.craftPresetDict[craftDefaultName].SASPreset ? null : Instance.activeSASPreset,
                                                    Instance.activeRSASPreset == Instance.craftPresetDict[craftDefaultName].RSASPreset ? null : Instance.activeRSASPreset));
            }
        }
        #endregion
    }
}
