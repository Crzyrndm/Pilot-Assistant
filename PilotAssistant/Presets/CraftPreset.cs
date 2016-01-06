using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    public class CraftPreset
    {
        string name;
        AsstPreset pa;

        public CraftPreset(string Name, AsstPreset PA)
        {
            name = Name;
            pa = PA;
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

        public bool Dead
        {
            get { return AsstPreset == null; }
        }
    }
}
