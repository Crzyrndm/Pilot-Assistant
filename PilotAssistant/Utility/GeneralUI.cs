using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.Utility
{
    enum myStyles
    {
        labelAlert,
        numBoxLabel,
        numBoxText,
        btnPlus,
        btnMinus,
        btnToggle
    }

    static class GeneralUI
    {
        internal static Color stockBackgroundGUIColor;
        internal static Color ActiveBackground = XKCDColors.BrightOrange;
        internal static Color InActiveBackground = XKCDColors.BrightSkyBlue;
        internal static Color HeaderButtonBackground = XKCDColors.BlueBlue;

        // save the skin so other windows can't interfere
        internal static GUISkin UISkin;

        internal static GUIContent KpLabel = new GUIContent("Kp", "Kp is the proportional response factor. The greater the error between the current state and the target, the greater the impact it has. \r\n\r\nP_res = Kp * error");
        internal static GUIContent KiLabel = new GUIContent("Ki", "Ki is the integral response factor. The integral response is the sum of all previous errors and depends on both the magnitude and the duration for which the error remained.\r\n\r\nI_res = Ki * sumOf(error)");
        internal static GUIContent KdLabel = new GUIContent("Kd", "Kd is the derivative response factor. The derivative response acts to prevent the output from changing and will dampen out oscillations when used in moderation.\r\n\r\nD_res = Kd * (error - prev_error)");
        internal static GUIContent ScalarLabel = new GUIContent("Scalar", "The scalar factor increase/decrease the impact of Kp, Ki, and Kd. This is used to accomodate variations in flight conditions.\r\n\r\nOutput = (P_res + I_res + D_res) / Scalar");
        internal static GUIContent IMaxLabel = new GUIContent("I Max", "The maximum value the integral sum can reach. This is mostly used to prevent excessive buildup when the setpoint is changed");
        internal static GUIContent IMinLabel = new GUIContent("I Min", "The minimum value the integral sum can reach. This is mostly used to prevent excessive buildup when the setpoint is changed");
        internal static GUIContent EasingLabel = new GUIContent("Easing", "The rate of change of the setpoint when a new target is set. Higher gives a faster change, lower gives a smoother change");
        internal static GUIContent DelayLabel = new GUIContent("Delay", "The time in ms between there being no input on the axis and the axis attitude being locked");

        internal static void InitColors()
        {
            //ActiveBackground = XKCDColors.BrightOrange;
            //InActiveBackground = XKCDColors.BrightSkyBlue;
            //HeaderButtonBackground = XKCDColors.BlueBlue;
        }

        internal static void customSkin()
        {
            UISkin = (GUISkin)MonoBehaviour.Instantiate(UnityEngine.GUI.skin);
            UISkin.customStyles = new GUIStyle[Enum.GetValues(typeof(myStyles)).GetLength(0)];
            stockBackgroundGUIColor = GUI.backgroundColor;

            // style for the paused message (big, bold, and red)
            UISkin.customStyles[(int)myStyles.labelAlert] = new GUIStyle(GUI.skin.box);
            UISkin.customStyles[(int)myStyles.labelAlert].normal.textColor = XKCDColors.Red;
            UISkin.customStyles[(int)myStyles.labelAlert].fontSize = 21;
            UISkin.customStyles[(int)myStyles.labelAlert].fontStyle = FontStyle.Bold;
            UISkin.customStyles[(int)myStyles.labelAlert].alignment = TextAnchor.MiddleCenter;

            // style for label to align with increment buttons
            UISkin.customStyles[(int)myStyles.numBoxLabel] = new GUIStyle(UISkin.label);
            UISkin.customStyles[(int)myStyles.numBoxLabel].alignment = TextAnchor.MiddleLeft;
            UISkin.customStyles[(int)myStyles.numBoxLabel].margin = new RectOffset(4, 4, 5, 3);

            // style for text box to align with increment buttons better
            UISkin.customStyles[(int)myStyles.numBoxText] = new GUIStyle(UISkin.textField);
            UISkin.customStyles[(int)myStyles.numBoxText].alignment = TextAnchor.MiddleLeft;
            UISkin.customStyles[(int)myStyles.numBoxText].margin = new RectOffset(4, 0, 5, 3);

            // style for increment button
            UISkin.customStyles[(int)myStyles.btnPlus] = new GUIStyle(UISkin.button);
            UISkin.customStyles[(int)myStyles.btnPlus].margin = new RectOffset(0, 4, 2, 0);
            UISkin.customStyles[(int)myStyles.btnPlus].hover.textColor = Color.yellow;
            UISkin.customStyles[(int)myStyles.btnPlus].onActive.textColor = Color.green;

            // style for derement button
            UISkin.customStyles[(int)myStyles.btnMinus] = new GUIStyle(UISkin.button);
            UISkin.customStyles[(int)myStyles.btnMinus].margin = new RectOffset(0, 4, 0, 2);
            UISkin.customStyles[(int)myStyles.btnMinus].hover.textColor = Color.yellow;
            UISkin.customStyles[(int)myStyles.btnMinus].onActive.textColor = Color.green;

            // A toggle that looks like a button
            UISkin.customStyles[(int)myStyles.btnToggle] = new GUIStyle(UISkin.button);
            UISkin.customStyles[(int)myStyles.btnToggle].normal.textColor = UISkin.customStyles[(int)myStyles.btnToggle].focused.textColor = Color.white;
            UISkin.customStyles[(int)myStyles.btnToggle].onNormal.textColor = UISkin.customStyles[(int)myStyles.btnToggle].onFocused.textColor = UISkin.customStyles[(int)myStyles.btnToggle].onHover.textColor
                = UISkin.customStyles[(int)myStyles.btnToggle].active.textColor = UISkin.customStyles[(int)myStyles.btnToggle].hover.textColor = UISkin.customStyles[(int)myStyles.btnToggle].onActive.textColor = Color.green;
            UISkin.customStyles[(int)myStyles.btnToggle].onNormal.background = UISkin.customStyles[(int)myStyles.btnToggle].onHover.background = UISkin.customStyles[(int)myStyles.btnToggle].onActive.background = UISkin.customStyles[(int)myStyles.btnToggle].active.background = HighLogic.Skin.button.onNormal.background;
            UISkin.customStyles[(int)myStyles.btnToggle].hover.background = UISkin.customStyles[(int)myStyles.btnToggle].normal.background;
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

            GUILayout.Label(labelText, UISkin.customStyles[(int)myStyles.numBoxLabel], GUILayout.Width(labelWidth));
            val = double.Parse(boxText);
            boxText = val.ToString(",0.0#####");
            string text = GUILayout.TextField(boxText, UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));
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
            if (GUILayout.Button("+", UISkin.customStyles[(int)myStyles.btnPlus], GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", UISkin.customStyles[(int)myStyles.btnMinus], GUILayout.Width(20), GUILayout.Height(13)))
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

            GUILayout.Label(labelText, UISkin.customStyles[(int)myStyles.numBoxLabel], GUILayout.Width(labelWidth));
            val = double.Parse(boxText);
            boxText = val.ToString(",0.0#####");
            string text = GUILayout.TextField(boxText, UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));
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
            if (GUILayout.Button("+", UISkin.customStyles[(int)myStyles.btnPlus], GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", UISkin.customStyles[(int)myStyles.btnMinus], GUILayout.Width(20), GUILayout.Height(13)))
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

            bool tempState = GUILayout.Toggle(toggleState, toggleText, UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(toggleWidth));
            if (tempState != toggleState)
            {
                toggleState = tempState;
                if (toggleState)
                {
                    setPoint = currentVal;
                    boxText = currentVal.ToString("N2");
                }
            }

            boxText = GUILayout.TextField(boxText, UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));

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
