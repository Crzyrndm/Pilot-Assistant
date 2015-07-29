namespace PilotAssistant.PID.Presets
{
    public class CraftPreset
    {
        string name;
        AsstPreset pa;
        SSASPreset ssas;
        SASPreset sas;
        RSASPreset rsas;

        public CraftPreset(string Name, AsstPreset PA, SSASPreset SSAS, SASPreset SAS, RSASPreset RSAS)
        {
            name = Name;
            pa = PA;
            ssas = SSAS;
            rsas = RSAS;
            sas = SAS;
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

        public SSASPreset SSASPreset
        {
            get { return ssas; }
            set { ssas = value; }
        }

        public SASPreset SASPreset
        {
            get { return sas; }
            set { sas = value; }
        }

        public RSASPreset RSASPreset
        {
            get { return rsas; }
            set { rsas = value; }
        }

        public bool Dead
        {
            get { return AsstPreset == null && SSASPreset == null && SASPreset == null && RSASPreset == null; }
        }
    }
}
