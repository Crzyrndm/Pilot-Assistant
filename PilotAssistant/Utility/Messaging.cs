using System;
using UnityEngine;

namespace PilotAssistant.Utility
{
    class Messaging
    {
        public static ScreenMessage pauseMessage = new ScreenMessage("Pilot Assistant is paused", 3, ScreenMessageStyle.UPPER_RIGHT);
        public static ScreenMessage unpauseMessage = new ScreenMessage("Pilot Assistant is unpaused", 3, ScreenMessageStyle.UPPER_RIGHT);
        public static ScreenMessage transferSASMessage = new ScreenMessage("Pilot Assistant control handed to SAS", 3, ScreenMessageStyle.UPPER_RIGHT);
        public static ScreenMessage retrieveSASMessage = new ScreenMessage("Pilot Assistant control retrieved from SAS", 3, ScreenMessageStyle.UPPER_RIGHT);
        public static ScreenMessage levelMessage = new ScreenMessage("Pilot Assistant is levelling off", 3, ScreenMessageStyle.UPPER_RIGHT);
        public static ScreenMessage PAPresetMessage = new ScreenMessage("Pilot Assistant active preset loaded", 3, ScreenMessageStyle.UPPER_RIGHT);
        public static ScreenMessage SSASPresetMessage = new ScreenMessage("SSAS active preset loaded", 3, ScreenMessageStyle.UPPER_RIGHT);
        public static ScreenMessage StockSASPresetMessage = new ScreenMessage("Stock SAS active preset loaded", 3, ScreenMessageStyle.UPPER_RIGHT);
        public static ScreenMessage SSASArmedMessage = new ScreenMessage("Surface SAS Armed", 3, ScreenMessageStyle.UPPER_RIGHT);
        public static ScreenMessage SSASDisArmedMessage = new ScreenMessage("Surface SAS Disarmed and Deactivated", 3, ScreenMessageStyle.UPPER_RIGHT);

        public static void postMessage(ScreenMessage msg)
        {
            ScreenMessages.PostScreenMessage(msg);
        }

        public static void postMessage(string message)
        {
            ScreenMessages.PostScreenMessage(message);
        }
    }
}
