using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using PID;
    using Utility;

    static class ModeratorMainWindow
    {
        internal static Rect window = new Rect(500, 500, 220, 500);
        internal static Vector2 scroll = new Vector2();

        internal static void Draw()
        {
            GeneralUI.Styles();

            if (AppLauncher.AppLauncherInstance.bDisplayModerator)
                window = GUI.Window(95743658, window, drawWindow, "Input Moderator");
        }

        private static void drawWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
            {
                AppLauncher.AppLauncherInstance.bDisplayModerator = false;
            }

            scroll = GUILayout.BeginScrollView(scroll);
            foreach (Monitor m in InputModerator.Monitors)
            {
                drawMonitor(m);
            }
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private static void drawMonitor(Monitor m)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(m.Name, GUILayout.Width(150)))
                m.bShow = !m.bShow;

            m.Active = GUILayout.Toggle(m.Active, "");
            GUILayout.EndHorizontal();

            if (m.bShow)
            {
                GUILayout.Label(string.Format("{0}: {1}", m.Name, m.current.ToString()));
                m.Lower = GeneralUI.labPlusNumBox("Lower", m.Lower.ToString());
                m.Upper = GeneralUI.labPlusNumBox("Upper", m.Upper.ToString());
                m.BoundKp = GeneralUI.labPlusNumBox("Bound Kp", m.BoundKp.ToString());
                GUILayout.Label(string.Format("{0}': {1}", m.Name, m.diff.ToString()));
                m.Rate = GeneralUI.labPlusNumBox("Rate", m.Rate.ToString());
                m.RateKp = GeneralUI.labPlusNumBox("Rate Kp", m.RateKp.ToString());
            }
        }
    }
}
