using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using PID;

    static class ModeratorMainWindow
    {
        internal static Rect window = new Rect(500, 500, 220, 500);
        internal static Vector2 scroll = new Vector2();

        static GUIStyle labelStyle;
        static GUIStyle textStyle;
        static GUIStyle btnStyle1;
        static GUIStyle btnStyle2;

        internal static void Draw()
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.margin = new RectOffset(4, 4, 5, 3);

            textStyle = new GUIStyle(GUI.skin.textField);
            textStyle.alignment = TextAnchor.MiddleLeft;
            textStyle.margin = new RectOffset(4, 0, 5, 3);

            btnStyle1 = new GUIStyle(GUI.skin.button);
            btnStyle1.margin = new RectOffset(0, 4, 2, 0);

            btnStyle2 = new GUIStyle(GUI.skin.button);
            btnStyle2.margin = new RectOffset(0, 4, 0, 2);

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
                m.Lower = labPlusNumBox("Lower", m.Lower.ToString());
                m.Upper = labPlusNumBox("Upper", m.Upper.ToString());
                m.BoundKp = labPlusNumBox("Bound Kp", m.BoundKp.ToString());
                GUILayout.Label(string.Format("{0}': {1}", m.Name, m.diff.ToString()));
                m.Rate = labPlusNumBox("Rate", m.Rate.ToString());
                m.RateKp = labPlusNumBox("Rate Kp", m.RateKp.ToString());
            }
        }

        private static double labPlusNumBox(string labelText, string boxText, float labelWidth = 100, float boxWidth = 60)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, labelStyle, GUILayout.Width(labelWidth));
            val = double.Parse(boxText);
            boxText = val.ToString(",0.0#####");
            string text = GUILayout.TextField(boxText, textStyle, GUILayout.Width(boxWidth));
            //
            try
            {
                val = double.Parse(text);
            }
            catch
            {
                val = double.Parse(boxText);
            }
            //
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", btnStyle1, GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", btnStyle2, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val /= 1.1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return val;
        }
    }
}
