namespace PilotAssistant.Toolbar
{
    using KSP.UI.Screens;

    public class AppLauncherFlight
    {
        private static ApplicationLauncherButton btnLauncher;

        private static AppLauncherFlight instance;
        public static AppLauncherFlight Instance
        {
            get
            {
                if (instance == null)
                    instance = new AppLauncherFlight();
                return instance;
            }
        }

        public void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            GameEvents.onGUIApplicationLauncherUnreadifying.Add(RemoveButton);
        }

        public void AddButton()
        {
            btnLauncher = ApplicationLauncher.Instance.AddModApplication(
                OnToggleTrue, OnToggleFalse,
                null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT,
                GameDatabase.Instance.GetTexture("Pilot Assistant/Icon/AppLauncherIcon", false));
        }

        public void RemoveButton(GameScenes scene)
        {
            ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
        }

        private void OnToggleTrue()
        {
            PilotAssistantFlightCore.bDisplayAssistant = true;
        }

        private void OnToggleFalse()
        {
            PilotAssistantFlightCore.bDisplayAssistant = false;
        }

        public static void setBtnState(bool state, bool click = false)
        {
            if (state)
                btnLauncher.SetTrue(click);
            else
                btnLauncher.SetFalse(click);
        }
    }
}
