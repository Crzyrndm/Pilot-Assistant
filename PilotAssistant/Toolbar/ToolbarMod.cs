using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.Toolbar
{
    public class ToolbarMod
    {
        private static ToolbarMod instance;
        public static ToolbarMod Instance
        {
            get
            {
                if (instance == null)
                    instance = new ToolbarMod();
                return instance;
            }
        }

        private IButton asstButton;
        private IButton SSASButton;
        private IButton stockButton;
        private IButton menuButton;
        public void Start()
        {
            menuButton = ToolbarManager.Instance.add("PilotAssistant", "PilotAssistantMenuBtn");
            menuButton.TexturePath = "Pilot Assistant/Icon/BlizzyIcon";
            menuButton.ToolTip = "Open Pilot Assistant Menu";
            menuButton.OnClick += (e) => PilotAssistantFlightCore.bDisplayOptions = !PilotAssistantFlightCore.bDisplayOptions;

            asstButton = ToolbarManager.Instance.add("PilotAssistant", "PilotAssistantAsstBtn");
            asstButton.TexturePath = "Pilot Assistant/Icon/BlizzyIcon";
            asstButton.ToolTip = "Open Pilot Assistant Window";
            asstButton.OnClick += (e) => PilotAssistantFlightCore.bDisplayAssistant = !PilotAssistantFlightCore.bDisplayAssistant;

            SSASButton = ToolbarManager.Instance.add("PilotAssistant", "PilotAssistantSSASBtn");
            SSASButton.TexturePath = "Pilot Assistant/Icon/BlizzyIcon";
            SSASButton.ToolTip = "Open SSAS Window";
            SSASButton.OnClick += (e) => PilotAssistantFlightCore.bDisplaySSAS = !PilotAssistantFlightCore.bDisplaySSAS;

            stockButton = ToolbarManager.Instance.add("PilotAssistant", "PilotAssistantStockBtn");
            stockButton.TexturePath = "Pilot Assistant/Icon/BlizzyIcon";
            stockButton.ToolTip = "Open Stock SAS tuning Window";
            stockButton.OnClick += (e) => PilotAssistantFlightCore.bDisplaySAS = !PilotAssistantFlightCore.bDisplaySAS;
        }

        public void OnDestroy()
        {
            menuButton.Destroy();
            asstButton.Destroy();
            SSASButton.Destroy();
            stockButton.Destroy();
        }
    }
}
