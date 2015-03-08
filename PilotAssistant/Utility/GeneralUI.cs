using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.Utility
{
    static class GeneralUI
    {
        internal static Color stockBackgroundGUIColor;
        internal static Color ActiveBackground;
        internal static Color InActiveBackground;
        internal static Color HeaderButtonBackground;

        // save the skin so other windows can't interfere
        internal static GUISkin UISkin;

        // used for the pause message
        internal static GUIStyle labelAlertStyle;

        // styles for the numbox functions
        internal static GUIStyle numBoxLabelStyle; // label that sits before the textbox and increment/decrement buttons
        internal static GUIStyle numBoxTextStyle; // textbox between label and increment/decrement buttons
        internal static GUIStyle btnStylePlus; // increment button
        internal static GUIStyle btnStyleMinus; // decrement button

        // Toggle button
        internal static GUIStyle toggleButton;

        internal static bool styleInit = false;

        internal static GUIContent KpLabel = new GUIContent("Kp", "Kp is the proportional response factor. The greater the error between the current state and the target, the greater the impact it has. \r\n\r\nP_res = Kp * error");
        internal static GUIContent KiLabel = new GUIContent("Ki", "Ki is the integral response factor. The integral response is the sum of all previous errors and depends on both the magnitude and the duration for which the error remained.\r\n\r\nI_res = Ki * sumOf(error)");
        internal static GUIContent KdLabel = new GUIContent("Kd", "Kd is the derivative response factor. The derivative response acts to prevent the output from changing and will dampen out oscillations when used in moderation.\r\n\r\nD_res = Kd * (error - prev_error)");
        internal static GUIContent ScalarLabel = new GUIContent("Scalar", "The scalar factor increase/decrease the impact of Kp, Ki, and Kd. This is used to accomodate variations in flight conditions.\r\n\r\nOutput = (P_res + I_res + D_res) / Scalar");
        internal static GUIContent IMaxLabel = new GUIContent("I Max", "The maximum value the integral sum can reach. This is mostly used to prevent excessive buildup when the setpoint is changed");
        internal static GUIContent IMinLabel = new GUIContent("I Min", "The minimum value the integral sum can reach. This is mostly used to prevent excessive buildup when the setpoint is changed");
        internal static GUIContent EasingLabel = new GUIContent("Easing", "The rate of change of the setpoint when a new target is set. Higher gives a faster change, lower gives a smoother change");

        internal static void InitColors()
        {
            stockBackgroundGUIColor = GUI.backgroundColor;
            ActiveBackground = XKCDColors.BrightOrange;
            InActiveBackground = XKCDColors.BrightSkyBlue;
            HeaderButtonBackground = XKCDColors.BlueBlue;
        }

        internal static void Styles()
        {
            if (styleInit)
                return;
            
            // style for the paused message (big, bold, and red)
            labelAlertStyle = new GUIStyle(GUI.skin.box);
            labelAlertStyle.normal.textColor = XKCDColors.Red;
            labelAlertStyle.fontSize = 21;
            labelAlertStyle.fontStyle = FontStyle.Bold;
            labelAlertStyle.alignment = TextAnchor.MiddleCenter;
            
            // style for label to align with increment buttons
            numBoxLabelStyle = new GUIStyle(GUI.skin.label);
            numBoxLabelStyle.alignment = TextAnchor.MiddleLeft;
            numBoxLabelStyle.margin = new RectOffset(4, 4, 5, 3);
            
            // style for text box to align with increment buttons better
            numBoxTextStyle = new GUIStyle(GUI.skin.textField);
            numBoxTextStyle.alignment = TextAnchor.MiddleLeft;
            numBoxTextStyle.margin = new RectOffset(4, 0, 5, 3);
            
            // style for increment button
            btnStylePlus = new GUIStyle(GUI.skin.button);
            btnStylePlus.margin = new RectOffset(0, 4, 2, 0);
            btnStylePlus.hover.textColor = Color.yellow;
            btnStylePlus.onActive.textColor = Color.green;

            // style for derement button
            btnStyleMinus = new GUIStyle(GUI.skin.button);
            btnStyleMinus.margin = new RectOffset(0, 4, 0, 2);
            btnStyleMinus.hover.textColor = Color.yellow;
            btnStyleMinus.onActive.textColor = Color.green;

            // A toggle that looks like a button
            toggleButton = new GUIStyle(GUI.skin.button);
            toggleButton.normal.textColor = toggleButton.focused.textColor = Color.white;
            toggleButton.onNormal.textColor = toggleButton.onFocused.textColor = toggleButton.onHover.textColor 
                = toggleButton.active.textColor = toggleButton.hover.textColor = toggleButton.onActive.textColor = Color.green;
            toggleButton.onNormal.background = toggleButton.onHover.background = toggleButton.onActive.background = toggleButton.active.background = HighLogic.Skin.button.onNormal.background;
            toggleButton.hover.background = toggleButton.normal.background;
            
            styleInit = true;
        }

        /// <summary>
        /// Draws a label and text box of specified widths with +/- 10% increment buttons. Returns the numeric value of the text box
        /// </summary>
        /// <param name="labelText">text for the label</param>
        /// <param name="boxText">number to display in text box</param>
        /// <param name="labelWidth"></param>
        /// <param name="boxWidth"></param>
        /// <returns>edited value of the text box</returns>
        internal static double labPlusNumBox(string labelText, string boxText, float labelWidth = 100, float boxWidth = 60)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, numBoxLabelStyle, GUILayout.Width(labelWidth));
            val = double.Parse(boxText);
            boxText = val.ToString(",0.0#####");
            string text = GUILayout.TextField(boxText, numBoxTextStyle, GUILayout.Width(boxWidth));
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
            if (GUILayout.Button("+", btnStylePlus, GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", btnStyleMinus, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val /= 1.1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return val;
        }

        internal static double labPlusNumBox(GUIContent labelText, string boxText, float labelWidth = 100, float boxWidth = 60)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, numBoxLabelStyle, GUILayout.Width(labelWidth));
            val = double.Parse(boxText);
            boxText = val.ToString(",0.0#####");
            string text = GUILayout.TextField(boxText, numBoxTextStyle, GUILayout.Width(boxWidth));
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
            if (GUILayout.Button("+", btnStylePlus, GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", btnStyleMinus, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val /= 1.1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return val;
        }

        /// <summary>
        /// Draws a toggle button and text box of specified widths with update button.
        /// </summary>
        /// <param name="toggleText"></param>
        /// <param name="boxVal"></param>
        /// <param name="toggleWidth"></param>
        /// <param name="boxWidth"></param>
        /// <returns></returns>
        internal static float TogPlusNumBox(string toggleText, ref bool toggleState, ref string boxText, double currentVal, double setPoint, float toggleWidth, float boxWidth)
        {
            GUILayout.BeginHorizontal();

            bool tempState = GUILayout.Toggle(toggleState, toggleText, toggleButton, GUILayout.Width(toggleWidth));
            if (tempState != toggleState)
            {
                toggleState = tempState;
                if (toggleState)
                {
                    setPoint = currentVal;
                    boxText = currentVal.ToString("N2");
                }
            }

            boxText = GUILayout.TextField(boxText, numBoxTextStyle, GUILayout.Width(boxWidth));

            if (GUILayout.Button("u"))
            {
                double temp;
                if (double.TryParse(boxText, out temp))
                    setPoint = temp;
            }
            //
            GUILayout.EndHorizontal();
            return (float)setPoint;
        }
    }
}
