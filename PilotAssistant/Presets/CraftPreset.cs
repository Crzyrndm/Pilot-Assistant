namespace PilotAssistant.Presets
{
    /// <summary>
    /// This can be made obselete. All that is needed now with a single module is a dictionary associating craft names with PA presets
    /// </summary>
    [System.Obsolete("Store association as a craft-PA preset Dict", true)]
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
