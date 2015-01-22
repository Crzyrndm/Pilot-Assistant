using System;
using UnityEngine;

namespace PilotAssistant.Utility
{
    class Messaging
    {
        static ScreenMessage pauseMessage = new ScreenMessage("Pilot Assistant is paused", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage unpauseMessage = new ScreenMessage("Pilot Assistant is unpaused", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage transferSASMessage = new ScreenMessage("Pilot Assistant control handed to SAS", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage retrieveSASMessage = new ScreenMessage("Pilot Assistant control retrieved from SAS", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage levelMessage = new ScreenMessage("Pilot Assistant is levelling off", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage PAPresetMessage = new ScreenMessage("Pilot Assistant active preset loaded", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage SSASPresetMessage = new ScreenMessage("SSAS active preset loaded", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage StockSASPresetMessage = new ScreenMessage("Stock SAS active preset loaded", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage SSASArmedMessage = new ScreenMessage("Surface SAS Armed", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage SSASDisArmedMessage = new ScreenMessage("Surface SAS Disarmed and Deactivated", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage derpMessage = new ScreenMessage("Idiot, this isn't a valid number", 3, ScreenMessageStyle.UPPER_RIGHT);

        internal static void statusMessage(int messageNumber)
        {
            switch (messageNumber)
            {
                case 0:
                    ScreenMessages.PostScreenMessage(pauseMessage);
                    break;
                case 1:
                    ScreenMessages.PostScreenMessage(unpauseMessage);
                    break;
                case 2:
                    if (checkAssistantActive())
                        ScreenMessages.PostScreenMessage(transferSASMessage);
                    break;
                case 3:
                    if (checkAssistantActive())
                        ScreenMessages.PostScreenMessage(retrieveSASMessage);
                    break;
                case 4:
                    if (checkAssistantActive())
                        ScreenMessages.PostScreenMessage(levelMessage);
                    break;
                case 5:
                    ScreenMessages.PostScreenMessage(PAPresetMessage);
                    break;
                case 6:
                    ScreenMessages.PostScreenMessage(SSASPresetMessage);
                    break;
                case 7:
                    ScreenMessages.PostScreenMessage(StockSASPresetMessage);
                    break;
                case 8:
                    ScreenMessages.PostScreenMessage(SSASArmedMessage);
                    break;
                case 9:
                    ScreenMessages.PostScreenMessage(SSASDisArmedMessage);
                    break;
                default:
                    ScreenMessages.PostScreenMessage(derpMessage); // For debugging purposes
                    break;
            }
        }

        internal static void postMessage(string message)
        {
            ScreenMessages.PostScreenMessage(message);
        }

        static bool checkAssistantActive()
        {
            return (PilotAssistant.Instance.bHdgActive || PilotAssistant.Instance.bVertActive);
        }
    }
}
