using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PilotAssistant.Presets
{
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
    }
}
