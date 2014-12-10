using System;
using UnityEngine;

namespace PilotAssistant.UI
{
    class Messaging
    {
        static ScreenMessage pauseMessage = new ScreenMessage("Pilot Assistant is paused", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage unpauseMessage = new ScreenMessage("Pilot Assistant is unpaused", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage transferSASMessage = new ScreenMessage("Pilot Assistant control handed to SAS", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage retrieveSASMessage = new ScreenMessage("Pilot Assistant control retrieved from SAS", 3, ScreenMessageStyle.UPPER_RIGHT);
        static ScreenMessage levelMessage = new ScreenMessage("Pilot Assistant is levelling off", 3, ScreenMessageStyle.UPPER_RIGHT);

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
                default:
                    break;
            }
        }

        static bool checkAssistantActive()
        {
            return (PilotAssistant.bHdgActive || PilotAssistant.bVertActive);
        }
    }
}
