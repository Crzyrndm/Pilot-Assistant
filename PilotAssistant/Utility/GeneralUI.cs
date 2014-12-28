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

        /// <summary>
        /// Draws a toggle button and text box of specified widths with +/- 1 increment buttons. Returns the numeric value of the text box
        /// </summary>
        /// <param name="toggleText"></param>
        /// <param name="boxVal"></param>
        /// <param name="toggleWidth"></param>
        /// <param name="boxWidth"></param>
        /// <param name="upper">upper value to which input will be clamped, attempting to increase will roll value down to lower</param>
        /// <param name="lower">lower value to which input will be clamped, attempting to decrease will roll value up to upper</param>
        /// <returns></returns>
        internal static double TogPlusNumBox(string toggleText, ref bool toggleState, double currentVal, double boxVal, float toggleWidth = 100, float boxWidth = 60, float upper = 360, float lower = -360)
        {
            GUILayout.BeginHorizontal();

            // state is returned by reference
            bool tempState = GUILayout.Toggle(toggleState, toggleText, toggleButton, GUILayout.Width(toggleWidth));
            if (tempState != toggleState)
            {
                toggleState = tempState;
                if (toggleState)
                    boxVal = currentVal;
            }

            string text = GUILayout.TextField(boxVal.ToString("N2"), numBoxTextStyle, GUILayout.Width(boxWidth));
            //
            try
            {
                boxVal = double.Parse(text);
            }
            catch {}
            //
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", btnStylePlus, GUILayout.Width(20), GUILayout.Height(13)))
            {
                boxVal += 1;
                if (boxVal >= upper)
                    boxVal = lower;
            }
            if (GUILayout.Button("-", btnStyleMinus, GUILayout.Width(20), GUILayout.Height(13)))
            {
                boxVal -= 1;
                if (boxVal < lower)
                    boxVal = upper - 1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return Functions.Clamp(boxVal, lower, upper);
        }

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
