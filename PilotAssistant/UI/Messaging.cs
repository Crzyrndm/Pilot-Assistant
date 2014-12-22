using System;
using UnityEngine;

namespace PilotAssistant.UI
{
    class Messaging
    {
        public static void PostMessage(string msg)
        {
            ScreenMessages.PostScreenMessage(new ScreenMessage(msg, 3, ScreenMessageStyle.UPPER_RIGHT));
        }
    }
}
