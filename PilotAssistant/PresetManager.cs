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

        //
        // A list of all the loaded PA presets and the one currently loaded
        public List<AsstPreset> AsstPresetList = new List<AsstPreset>();
        [Obsolete("Need to move this inside the PA class as can be done per instance", true)]
        public AsstPreset activeAsstPreset = null;

        //
        // stores all craft presets by name
        public Dictionary<string, string> craftPresetDict = new Dictionary<string, string>();

        //
        // save/load paths
        public const string presetsPath = "GameData/Pilot Assistant/Presets.cfg";
        public const string defaultsPath = "GameData/Pilot Assistant/Defaults.cfg";

        //
        // names of default presets
        public const string craftDefaultName = "default";
        public const string asstDefaultName = "default";

        //
        // node ID's for craft and PA presets
        public const string craftPresetNodeName = "CraftPreset";
        public const string asstPresetNodeName = "PIDPreset";

        //
        // PA preset name ID in the craft preset
        public const string craftAsstKey = "pilot";

        //
        // controller node ID's for the PA preset
        public const string hdgCtrlr = "HdgBankController";
        public const string yawCtrlr = "HdgYawController";
        public const string aileronCtrlr = "AileronController";
        public const string rudderCtrlr = "RudderController";
        public const string altCtrlr = "AltitudeController";
        public const string vertCtrlr = "AoAController";
        public const string elevCtrlr = "ElevatorController";
        public const string speedCtrlr = "SpeedController";
        public const string accelCtrlr = "AccelController";

        //
        // controller property keys for PA
        public const string pGain = "PGain";
        public const string iGain = "IGain";
        public const string dGain = "DGain";
        public const string min = "MinOut";
        public const string max = "MaxOut";
        public const string iLower = "ClampLower";
        public const string iUpper = "ClampUpper";
        public const string scalar = "Scalar";
        public const string ease = "Ease";

        public void Start()
        {
            // only ever a single instance of this class created upon reaching the main menu for the first time
            instance = this;
            // make sure that instance is never recovered while loading
            DontDestroyOnLoad(this);
            // make sure the default is always there
            craftPresetDict.Add(craftDefaultName, asstDefaultName);
            // load preset data saved from a previous time
            loadPresetsFromFile();
        }

        public void OnDestroy()
        {
            // probably not ever called but if it is, changes are saved
            saveToFile();
        }

        /// <summary>
        /// process previously saved data loading PA and craft presets into a usable format
        /// </summary>
        public void loadPresetsFromFile()
        {
            // fallback default preset is only ever recorded in the default craft preset. Not user created, so don't list it
            AsstPreset asstDefault = null;
            
            // PA nodes
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(asstPresetNodeName)) // want to move this outside GameDatabase at some point
            {
                string name = node.GetValue("name");
                if (ReferenceEquals(node, null) || instance.AsstPresetList.Any(p => p.name == name))
                    continue;

                // process controller nodes to a more easily accesible array format.
                // Could possibly do this a bit neater by iterating through the nodes and doing a switch on the node name. Downside would be trying to keep the order intact
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

                // storing the new preset
                if (name == asstDefaultName)
                    asstDefault = new AsstPreset(gains, name);
                else
                    instance.AsstPresetList.Add(new AsstPreset(gains, name));
            }

            // craft nodes are just a list of craft/preset pairs with a comma delimiter
            char[] delimiter = new char[] { ',' };
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(craftPresetNodeName)) // want to move this outside GameDatabase at some point
            {
                if (ReferenceEquals(node, null))
                    continue;
                string[] values = node.GetValues();
                for (int i = 0; i < values.Length; ++i )
                {
                    string[] tmp = values[i].Split(delimiter, 2, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
                    if (tmp.Length != 2)
                        continue;

                    if (tmp[0] == craftDefaultName)
                        craftPresetDict[craftDefaultName] = tmp[1];
                    else if (!craftPresetDict.ContainsKey(tmp[0]))
                        craftPresetDict.Add(tmp[0], tmp[1]);
                }
            }
        }

        /// <summary>
        /// saves user created and default presets for next run
        /// </summary>
        public static void saveToFile()
        {
            ConfigNode node = new ConfigNode();
            // dummy value is required incase nothing else will be added to the file. KSP doesn't like blank .cfg's
            node.AddValue("dummy", "do not delete me");
            foreach (AsstPreset p in instance.AsstPresetList)
                node.AddNode(AsstPresetToNode(p));
            foreach (KeyValuePair<string, string> cP in instance.craftPresetDict)
                node.AddValue("pair", string.Concat(cP.Key, ",", cP.Value)); // pair = craft,preset
            
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + presetsPath);
        }

        /// <summary>
        /// Sets the current active PA preset to be the default
        /// </summary>
        public void updateDefaultAsstPreset(AsstPreset preset)
        {
            craftPresetDict[craftDefaultName] = preset.name;
            saveToFile();
        }

        /// <summary>
        /// Processes a config node for a controller into a more accessible array of doubles
        /// </summary>
        /// <param name="node">A controller node</param>
        /// <param name="type">An ID to use for referencing the default values in cases of null input</param>
        /// <returns>an array of doubles containing the gains for a controller</returns>
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

        /// <summary>
        /// Processes an array of gains into a Config node ready to be saved
        /// </summary>
        /// <param name="name">Node name</param>
        /// <param name="index">index of the array in the preset storage</param>
        /// <param name="preset">object to source the array from</param>
        /// <returns>A config node holding the gains for a controller</returns>
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

        /// <summary>
        /// Turns a PA preset into a config node holding nodes of all its controllers
        /// </summary>
        /// <param name="preset">preset to process</param>
        /// <returns>config node holding all PA controller values</returns>
        public static ConfigNode AsstPresetToNode(AsstPreset preset)
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

        /// <summary>
        /// returns the default gains for the controller
        /// </summary>
        /// <param name="type">controller ID</param>
        /// <returns>default gains array</returns>
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

        /// <summary>
        /// Creates a preset from an array of controllers. Can't access Asst controllers directly because more than one instance can be active
        /// </summary>
        /// <param name="name">preset name</param>
        /// <param name="controllers">controllers to build from</param>
        /// <param name="v">vessel to associate with</param>
        public static bool newAsstPreset(string name, Asst_PID_Controller[] controllers, Vessel v)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (Instance.AsstPresetList.Any(p => p.name == name))
            {
                GeneralUI.postMessage("Failed to add preset with duplicate name");
                return false;
            }
            AsstPreset newPreset = new AsstPreset(controllers, name);
            Instance.updateCraftPreset(newPreset, v);
            Instance.AsstPresetList.Add(newPreset);
            saveToFile();

            return true; // new preset created successfully, can clear the string
        }

        /// <summary>
        /// loads a preset into the controllers of a PA instance
        /// </summary>
        /// <param name="p">the preset to load</param>
        /// <param name="asstInstance">the PA instance to load to</param>
        public static void loadAsstPreset(AsstPreset p, PilotAssistant asstInstance)
        {
            if (ReferenceEquals(p, null))
                return;
            Asst_PID_Controller[] c = asstInstance.controllers;
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
            
            asstInstance.activePreset = p;
            GeneralUI.postMessage("Loaded preset " + p.name);
            
            if (asstInstance.activePreset.name != Instance.craftPresetDict[craftDefaultName])
                Instance.updateCraftPreset(asstInstance.activePreset, asstInstance.vesModule.vesselRef);
            saveToFile();
        }

        /// <summary>
        /// loads a preset into the controllers of a PA instance
        /// </summary>
        /// <param name="p">the preset to load</param>
        /// <param name="asstInstance">the PA instance to load to</param>
        public static void loadAsstPreset(string presetName, PilotAssistant asstInstance)
        {
            if (string.IsNullOrEmpty(presetName))
                return;
            AsstPreset p = Instance.AsstPresetList.FirstOrDefault(pr => pr.name == presetName);
            if (ReferenceEquals(p , null))
                return;
            Asst_PID_Controller[] c = asstInstance.controllers;
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
            
            asstInstance.activePreset = p;
            GeneralUI.postMessage("Loaded preset " + p.name);
            
            if (asstInstance.activePreset.name != Instance.craftPresetDict[craftDefaultName])
                Instance.updateCraftPreset(asstInstance.activePreset, asstInstance.vesModule.vesselRef);
            saveToFile();
        }

        /// <summary>
        /// remove a preset from the stored list and remove any references to it on active vessels
        /// </summary>
        /// <param name="p"></param>
        public void deleteAsstPreset(AsstPreset p)
        {
            GeneralUI.postMessage("Deleted preset " + p.name);
            foreach (AsstVesselModule avm in PilotAssistantFlightCore.Instance.controlledVessels)
            {
                if (avm.vesselAsst.activePreset == p)
                    avm.vesselAsst.activePreset = null;
            }
            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, string> kvp in craftPresetDict)
            {
                if (kvp.Value == p.name)
                    toRemove.Add(kvp.Key);
            }
            foreach (string s in toRemove)
                craftPresetDict.Remove(s);
            AsstPresetList.Remove(p);

            p = null;

            saveToFile();
        }

        /// <summary>
        /// called on vessel load to load the correct preset for the vessel being flown
        /// </summary>
        /// <param name="instance">The instance to load for</param>
        public void loadCraftAsstPreset(PilotAssistant instance)
        {
            string presetName;
            if (craftPresetDict.TryGetValue(instance.Vessel.name, out presetName))
                loadAsstPreset(presetName, instance);
            else
                loadAsstPreset(Instance.craftPresetDict[craftDefaultName], instance);
        }

        /// <summary>
        /// updates the craft/preset references
        /// </summary>
        /// <param name="p">preset</param>
        /// <param name="v">craft</param>
        public void updateCraftPreset(AsstPreset p, Vessel v)
        {
            if (!Instance.craftPresetDict.ContainsKey(v.vesselName))
                craftPresetDict.Add(v.vesselName, string.Empty);
            Instance.craftPresetDict[v.vesselName] = p.name;
        }

        public void initDefaultPresets(AsstPreset p)
        {
            if (Instance.craftPresetDict[craftDefaultName] == string.Empty)
            {
                AsstPresetList.Add(p);
                Instance.craftPresetDict[craftDefaultName] = p.name;
            }
            saveToFile();
            // loadAsstPreset(Instance.craftPresetDict[craftDefaultName], instance);
        }
    }
}
