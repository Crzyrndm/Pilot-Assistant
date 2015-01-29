using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    class CraftPreset
    {
        string name;
        AsstPreset pa;
        SASPreset ssas;
        SASPreset stock;
        bool sasMode; // true = stock, false = ssas

        public CraftPreset(string Name, AsstPreset PA, SASPreset SSAS, SASPreset stockSAS, bool Mode)
        {
            name = Name;
            pa = PA;
            ssas = SSAS;
            stock = stockSAS;
            sasMode = Mode;
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public AsstPreset AsstPreset
        {
            get { return pa; }
            set { pa = value; }
        }

        public SASPreset SSASPreset
        {
            get { return ssas; }
            set { ssas = value; }
        }

        public SASPreset StockPreset
        {
            get { return stock; }
            set { stock = value; }
        }

        public bool SASMode
        {
            get { return sasMode; }
            set { sasMode = value; }
        }

        public bool Dead
        {
            get { return AsstPreset == null && SSASPreset == null && StockPreset == null; }
        }
    }
}
