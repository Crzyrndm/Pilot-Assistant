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

        internal static GUIStyle labelAlertStyle;
        internal static GUIStyle textStyle;

        // New
        internal static bool stylesInitialized;
        internal static GUIStyle guiSectionStyle;
        internal static GUIStyle toggleButtonStyle;
        internal static GUIStyle buttonStyle;
        internal static GUIStyle boldLabelStyle;

        internal static GUIStyle spinnerPlusBtnStyle;
        internal static GUIStyle spinnerMinusBtnStyle;

        internal static GUIStyle labelStyle;

        internal static void InitColors()
        {
            stockBackgroundGUIColor = GUI.backgroundColor;
            ActiveBackground = XKCDColors.BrightOrange;
            InActiveBackground = XKCDColors.BrightSkyBlue;
            HeaderButtonBackground = XKCDColors.BlueBlue;
        }

        internal static void Styles()
        {
            if (stylesInitialized)
                return;
            GUI.skin = HighLogic.Skin;

            // style for the paused message
            labelAlertStyle = new GUIStyle(GUI.skin.label);
            labelAlertStyle.normal.textColor = XKCDColors.Red;
            labelAlertStyle.fontSize = 21;
            labelAlertStyle.fontStyle = FontStyle.Bold;

            // style for text box to align with increment buttons better
            textStyle = new GUIStyle(GUI.skin.textField);
            textStyle.alignment = TextAnchor.MiddleLeft;
            textStyle.margin = new RectOffset(4, 0, 5, 3);

            guiSectionStyle = new GUIStyle(GUI.skin.box);
            guiSectionStyle.normal.textColor
                = guiSectionStyle.focused.textColor
                = Color.white;
            guiSectionStyle.hover.textColor
                = guiSectionStyle.active.textColor
                = Color.yellow;
            guiSectionStyle.onNormal.textColor
                = guiSectionStyle.onFocused.textColor
                = guiSectionStyle.onHover.textColor
                = guiSectionStyle.onActive.textColor
                = Color.green;
            guiSectionStyle.padding = new RectOffset(4, 4, 4, 4);

            toggleButtonStyle = new GUIStyle(GUI.skin.button);
            toggleButtonStyle.normal.textColor
                = toggleButtonStyle.focused.textColor
                = Color.white;
            toggleButtonStyle.hover.textColor
                = toggleButtonStyle.active.textColor
                = toggleButtonStyle.onActive.textColor
                = Color.yellow;
            toggleButtonStyle.onNormal.textColor
                = toggleButtonStyle.onFocused.textColor
                = toggleButtonStyle.onHover.textColor
                = Color.green;
            toggleButtonStyle.padding = new RectOffset(4, 4, 4, 4);

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(4, 4, 4, 4);
            
            // style for increment button
            spinnerPlusBtnStyle = new GUIStyle(GUI.skin.button);
            spinnerPlusBtnStyle.margin = new RectOffset(0, 2, 2, 0);

            // style for derement button
            spinnerMinusBtnStyle = new GUIStyle(GUI.skin.button);
            spinnerMinusBtnStyle.margin = new RectOffset(0, 2, 0, 2);

            // style for label to align with increment buttons
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.margin = new RectOffset(4, 4, 5, 3);
            
            boldLabelStyle = new GUIStyle(GUI.skin.label);
            boldLabelStyle.fontStyle = FontStyle.Bold;
            boldLabelStyle.alignment = TextAnchor.MiddleLeft;

            stylesInitialized = false;
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
            if (GUILayout.Button("+", spinnerPlusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", spinnerMinusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val /= 1.1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return val;
        }

        /// <summary>
        /// Draws a label and text box of specified widths with +/- 1 increment buttons. Returns the numeric value of the text box
        /// </summary>
        /// <param name="labelText"></param>
        /// <param name="boxText"></param>
        /// <param name="labelWidth"></param>
        /// <param name="boxWidth"></param>
        /// <param name="upper">upper value to which input will be clamped, attempting to increase will roll value down to lower</param>
        /// <param name="lower">lower value to which input will be clamped, attempting to decrease will roll value up to upper</param>
        /// <returns></returns>
        internal static double labPlusNumBox2(string labelText, string boxText, float labelWidth = 100, float boxWidth = 60, float upper = 360, float lower = -360)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, labelStyle, GUILayout.Width(labelWidth));
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
            if (GUILayout.Button("+", spinnerPlusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val += 1;
                if (val >= upper)
                    val = lower;
            }
            if (GUILayout.Button("-", spinnerMinusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val -= 1;
                if (val < lower)
                    val = upper - 1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return Functions.Clamp(val, lower, upper);
        }

        // Unused?
        internal static Texture2D textureBlock(int w, int h, Color col)
        {
            Color[] pixels = new Color[w * h];
            for( int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = col;
            }
            Texture2D texture = new Texture2D(w, h);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
