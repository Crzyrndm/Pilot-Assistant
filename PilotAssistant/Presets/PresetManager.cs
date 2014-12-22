using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PilotAssistant.Presets
{
    using UI;

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class PresetManager : MonoBehaviour
    {
        private static PAPreset defaultPATuning = null;
        private static List<PAPreset> PAPresetList = new List<PAPreset>();
        private static PAPreset activePAPreset = null;

        private static SASPreset defaultSASTuning;
        private static SASPreset defaultStockSASTuning;
        private static List<SASPreset> SASPresetList = new List<SASPreset>();
        private static SASPreset activeSASPreset = null;
        private static SASPreset activeStockSASPreset = null;

        public void Start()
        {
            LoadPresetsFromFile();
            DontDestroyOnLoad(this);
        }

        public void OnDestroy()
        {
            SavePresetsToFile();
        }

        private static void LoadPresetsFromFile()
        {
            PAPresetList.Clear();
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("PAPreset"))
            {
                if (node == null)
                    continue;

                PAPresetList.Add(new PAPreset(node));
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("SASPreset"))
            {
                if (node == null)
                    continue;

                SASPresetList.Add(new SASPreset(node));
            }
        }
        
        public static void SavePresetsToFile()
        {
            ConfigNode node = new ConfigNode();
            if (PAPresetList.Count == 0 && SASPresetList.Count == 0)
                node.AddValue("dummy", "do not delete me");
            else
            {
                foreach (PAPreset p in PAPresetList)
                {
                    node.AddNode(p.ToConfigNode());
                }
                foreach (SASPreset p in SASPresetList)
                {
                    node.AddNode(p.ToConfigNode());
                }
            }
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/Pilot Assistant/Presets.cfg");
        }

        public static void InitDefaultStockSASTuning()
        {
            defaultStockSASTuning = new SASPreset(Utility.FlightData.thisVessel.Autopilot.SAS, "Stock");
            if (activeStockSASPreset == null)
                activeStockSASPreset = defaultStockSASTuning;
            else if (activeStockSASPreset != defaultStockSASTuning)
            {
                LoadStockSASPreset(activeStockSASPreset);
                Messaging.PostMessage("Stock SAS active preset loaded.");
            }
        }
        
        public static void InitDefaultSASTuning(PID.PID_Controller[] controllers)
        {
            defaultSASTuning = new SASPreset(controllers, "Default");
            if (activeSASPreset == null)
                activeSASPreset = PresetManager.defaultSASTuning;
            else if (activeSASPreset != defaultSASTuning)
            {
                LoadSASPreset(controllers, activeSASPreset);
                Messaging.PostMessage("SSAS active preset loaded.");
            }
        }
        
        public static void InitDefaultPATuning(PID.PID_Controller[] controllers)
        {
            defaultPATuning = new PAPreset(controllers, "Default");
            if (activePAPreset == null)
                activePAPreset = defaultPATuning;
            else if (activePAPreset != defaultPATuning)
            {
                LoadPAPreset(controllers, activePAPreset);
                Messaging.PostMessage("Pilot Assistant active preset loaded.");
            }
        }

        public static SASPreset GetActiveStockSASPreset()
        {
            return activeStockSASPreset;
        }
        
        public static SASPreset GetActiveSASPreset()
        {
            return activeSASPreset;
        }
        
        public static PAPreset GetActivePAPreset()
        {
            return activePAPreset;
        }

        public static SASPreset GetDefaultStockSASTuning()
        {
            return defaultStockSASTuning;
        }

        public static SASPreset GetDefaultSASTuning()
        {
            return defaultSASTuning;
        }

        public static PAPreset GetDefaultPATuning()
        {
            return defaultPATuning;
        }

        public static void RegisterStockSASPreset(string name)
        {
            if (name == "")
            {
                Messaging.PostMessage("Failed to add preset with no name.");
                return;
            } 
            foreach (SASPreset p in SASPresetList)
            {
                if (name == p.GetName())
                {
                    Messaging.PostMessage("Failed to add preset with duplicate name.");
                    return;
                }
            }

            SASPreset p2 = new SASPreset(Utility.FlightData.thisVessel.Autopilot.SAS, name);
            SASPresetList.Add(p2);
            LoadStockSASPreset(p2);
            SavePresetsToFile();
        }
        
        public static void RegisterSASPreset(PID.PID_Controller[] controllers, string name)
        {
            if (name == "")
            {
                Messaging.PostMessage("Failed to add preset with no name.");
                return;
            } 
            foreach (SASPreset p in SASPresetList)
            {
                if (name == p.GetName())
                {
                    Messaging.PostMessage("Failed to add preset with duplicate name.");
                    return;
                }
            }

            SASPreset p2 = new SASPreset(controllers, name);
            SASPresetList.Add(p2);
            LoadSASPreset(controllers, p2);
            SavePresetsToFile();
        }
        
        public static void RegisterPAPreset(PID.PID_Controller[] controllers, string name)
        {
            if (name == "")
            {
                Messaging.PostMessage("Failed to add preset with no name.");
                return;
            }
            foreach (PAPreset p in PAPresetList)
            {
                if (name == p.GetName())
                {
                    Messaging.PostMessage("Failed to add preset with duplicate name.");
                    return;
                }
            }
                
            PAPreset p2 = new PAPreset(controllers, name);
            PAPresetList.Add(p2);
            LoadPAPreset(controllers, p2);
            SavePresetsToFile();
        }

        public static void LoadStockSASPreset(SASPreset p)
        {
            activeStockSASPreset = p;
            p.LoadStockPreset();
            Messaging.PostMessage("Loaded preset \"" + p.GetName() + "\"");
        }
        
        public static void LoadSASPreset(PID.PID_Controller[] controllers, SASPreset p)
        {
            activeSASPreset = p;
            p.LoadPreset(controllers);
            Messaging.PostMessage("Loaded preset \"" + p.GetName() + "\"");
        }
        
        public static void LoadPAPreset(PID.PID_Controller[] controllers, PAPreset p)
        {
            activePAPreset = p;
            p.LoadPreset(controllers);
            Messaging.PostMessage("Loaded preset \"" + p.GetName() + "\"");
        }

        public static List<SASPreset> GetAllSASPresets()
        {
            // return a shallow copy of the list
            List<SASPreset> l = new List<SASPreset>();
            foreach (SASPreset p in SASPresetList)
            {
                if (!p.IsStockSAS())
                    l.Add(p);
            }
            return l;
        }

        public static List<SASPreset> GetAllStockSASPresets()
        {
            List<SASPreset> l = new List<SASPreset>();
            foreach (SASPreset p in SASPresetList)
            {
                if (p.IsStockSAS())
                    l.Add(p);
            }
            return l;
        }
        
        public static List<PAPreset> GetAllPAPresets()
        {
            // return a shallow copy of the list
            return new List<PAPreset>(PAPresetList);
        }

        public static void RemovePreset(SASPreset p)
        {
            if (p.IsStockSAS())
            {
                if (activeStockSASPreset == p)
                    activeStockSASPreset = null;
                SASPresetList.Remove(p);
                SavePresetsToFile();
            }
            else
            {
                if (activeSASPreset == p)
                    activeSASPreset = null;
                SASPresetList.Remove(p);
                SavePresetsToFile();
            }
        }
        
        public static void RemovePreset(PAPreset p)
        {
            if (activePAPreset == p)
                activePAPreset = null;
            PAPresetList.Remove(p);
            SavePresetsToFile();
        }
    }
}
