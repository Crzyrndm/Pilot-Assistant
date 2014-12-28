using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    class CraftPreset
    {
        string name;
        PresetPA pa;
        PresetSAS ssas;
        PresetSAS stock;

        public CraftPreset(string Name, PresetPA PA, PresetSAS SSAS, PresetSAS stockSAS)
        {
            name = Name;
            pa = PA;
            ssas = SSAS;
            stock = stockSAS;
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public PresetPA PresetPA
        {
            get { return pa; }
            set { pa = value; }
        }

        public PresetSAS SSAS
        {
            get { return ssas; }
            set { ssas = value; }
        }

        public PresetSAS Stock
        {
            get { return stock; }
            set { stock = value; }
        }
    }
}
