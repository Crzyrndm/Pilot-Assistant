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
        internal static GUIStyle numBoxTextStyle;

        // New
        internal static bool stylesInitialized;
        internal static GUIStyle guiSectionStyle;
        internal static GUIStyle toggleButtonStyle;
        internal static GUIStyle buttonStyle;
        internal static GUIStyle boldLabelStyle;

        internal static GUIStyle spinnerPlusBtnStyle;
        internal static GUIStyle spinnerMinusBtnStyle;

        internal static GUIStyle labelStyle;

        // Used to track the state of a text field group.
        private class TextFieldGroupState
        {
            public TextFieldGroupState() { counter = 0; locked = false; }
            public int counter;
            public bool locked;
        }

        // Map from text field group name to text field group state. 
        private static Dictionary<string, TextFieldGroupState> textFieldGroups = new Dictionary<string, TextFieldGroupState>();

        // Begin a new text field group, sets the counter to zero
        public static void StartTextFieldGroup(string groupName)
        {
            TextFieldGroupState st = null;
            if (textFieldGroups.TryGetValue(groupName, out st))
                st.counter = 0;
            else
                textFieldGroups[groupName] = new TextFieldGroupState();
        }

        // Mark the next control as a text field. Actually any control which we want to lock input for.
        public static void TextFieldNext(string groupName)
        {
            TextFieldGroupState st = null;
            if (textFieldGroups.TryGetValue(groupName, out st))
            {
                // st.counter used so names are unique.
                GUI.SetNextControlName("IMPORTANT_TEXTFIELD_" + groupName + st.counter);
                ++st.counter;
            }
        }

        // Mark the end of the text field group, automatically lock if any control has focus. 
        public static bool AutolockTextFieldGroup(string groupName, ControlTypes mask)
        {
            TextFieldGroupState st = null;
            if (textFieldGroups.TryGetValue(groupName, out st))
            {
                string name = GUI.GetNameOfFocusedControl();
                bool focus = name.StartsWith("IMPORTANT_TEXTFIELD_" + groupName);
                if (focus && !st.locked)
                {
                    st.locked = true;
                    InputLockManager.SetControlLock(mask, groupName + "_ControlLock");
                }
                else if (!focus && st.locked)
                {
                    st.locked = false;
                    InputLockManager.RemoveControlLock(groupName + "_ControlLock");
                }
                return st.locked;
            }
            return false;
        }

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
            numBoxTextStyle = new GUIStyle(GUI.skin.textField);
            numBoxTextStyle.alignment = TextAnchor.MiddleLeft;
            numBoxTextStyle.margin = new RectOffset(4, 0, 5, 3);

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
        /// <param name="textFieldGroup">The text field group the input box should have.</param>
        /// <param name="labelText">text for the label</param>
        /// <param name="boxText">number to display in text box</param>
        /// <param name="labelWidth"></param>
        /// <param name="boxWidth"></param>
        /// <returns>edited value of the text box</returns>
        internal static double labPlusNumBox(
            string textFieldGroup,
            string labelText,
            string boxText,
            float labelWidth = 100,
            float boxWidth = 60)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, labelStyle, GUILayout.Width(labelWidth));
            val = double.Parse(boxText);
            boxText = val.ToString(",0.0#####");
            GeneralUI.TextFieldNext(textFieldGroup);
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
        /// <param name="textFieldGroup">The text field group the input box should have.</param>
        /// <param name="toggleText"></param>
        /// <param name="boxVal"></param>
        /// <param name="toggleWidth"></param>
        /// <param name="boxWidth"></param>
        /// <param name="upper">upper value to which input will be clamped, attempting to increase will roll value down to lower</param>
        /// <param name="lower">lower value to which input will be clamped, attempting to decrease will roll value up to upper</param>
        /// <returns></returns>
        internal static double TogPlusNumBox(
            string textFieldGroup,
            string toggleText,
            ref bool toggleState,
            double boxVal,
            float toggleWidth = 100,
            float boxWidth = 60,
            float upper = 360,
            float lower = -360)
        {
            GUILayout.BeginHorizontal();
            // state is returned by reference
            toggleState = GUILayout.Toggle(toggleState, toggleText, toggleButtonStyle, GUILayout.Width(toggleWidth));
            GeneralUI.TextFieldNext(textFieldGroup);
            string text = GUILayout.TextField(boxVal.ToString("N2"), numBoxTextStyle, GUILayout.Width(boxWidth));
            try
            {
                boxVal = double.Parse(text);
            }
            catch {}
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", spinnerPlusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
            {
                boxVal += 1;
                if (boxVal >= upper)
                    boxVal = lower;
            }
            if (GUILayout.Button("-", spinnerMinusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
            {
                boxVal -= 1;
                if (boxVal < lower)
                    boxVal = upper - 1;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            return Functions.Clamp(boxVal, lower, upper);
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
