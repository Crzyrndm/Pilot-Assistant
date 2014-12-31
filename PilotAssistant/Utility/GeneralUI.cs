using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.Utility
{
    static class GeneralUI
    {
        private static Color ssasActiveBGColor = XKCDColors.BrightOrange;
        private static Color ssasInactiveBGColor = XKCDColors.BrightSkyBlue;

        private static GUIStyle labelAlertStyle;
        private static GUIStyle numBoxTextStyle;

        private static bool stylesInitialized;
        private static GUIStyle guiSectionStyle;
        private static GUIStyle toggleButtonStyle;
        private static GUIStyle buttonStyle;
        private static GUIStyle boldLabelStyle;

        private static GUIStyle spinnerPlusBtnStyle;
        private static GUIStyle spinnerMinusBtnStyle;

        private static GUIStyle labelStyle;
        private static GUIStyle optionsWindowStyle;

        public static Color SSASActiveBGColor
        {
            get
            {
                return ssasActiveBGColor;
            }
        }
        public static Color SSASInactiveBGColor
        {
            get
            {
                return ssasInactiveBGColor;
            }
        }

        public static GUIStyle LabelAlertStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return labelAlertStyle;
            }
        }

        public static GUIStyle NumBoxTextStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return numBoxTextStyle;
            }
        }

        public static GUIStyle GUISectionStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return guiSectionStyle;
            }
        }
        public static GUIStyle ToggleButtonStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return toggleButtonStyle;
            }
        }
        public static GUIStyle ButtonStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return buttonStyle;
            }
        }
        public static GUIStyle BoldLabelStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return boldLabelStyle;
            }
        }

        public static GUIStyle SpinnerPlusBtnStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return spinnerPlusBtnStyle;
            }
        }
        public static GUIStyle SpinnerMinusBtnStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return spinnerMinusBtnStyle;
            }
        }

        public static GUIStyle LabelStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return labelStyle;
            }
        }

        public static GUIStyle OptionsWindowStyle
        {
            get
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                }
                return optionsWindowStyle;
            }
        }

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

        // Clear the lock for a specific text field group
        public static void ClearLocks(string groupName)
        {
            TextFieldGroupState st = null;
            if (textFieldGroups.TryGetValue(groupName, out st))
            {
                st.counter = 0;
                if (st.locked)
                {
                    st.locked = false;
                    InputLockManager.RemoveControlLock(groupName + "_ControlLock");
                }
            }
        }

        private static void InitStyles()
        {
            GUISkin skin = HighLogic.Skin;
            // style for the paused message
            labelAlertStyle = new GUIStyle(skin.label);
            labelAlertStyle.alignment = TextAnchor.MiddleCenter;
            labelAlertStyle.normal.textColor = XKCDColors.Red;
            labelAlertStyle.fontSize = 21;
            labelAlertStyle.fontStyle = FontStyle.Bold;

            // style for text box to align with increment buttons better
            numBoxTextStyle = new GUIStyle(skin.textField);
            numBoxTextStyle.alignment = TextAnchor.MiddleLeft;
            numBoxTextStyle.margin = new RectOffset(4, 0, 5, 3);

            guiSectionStyle = new GUIStyle(skin.box);
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

            toggleButtonStyle = new GUIStyle(skin.button);
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

            buttonStyle = new GUIStyle(skin.button);
            buttonStyle.padding = new RectOffset(4, 4, 4, 4);
            
            // style for increment button
            spinnerPlusBtnStyle = new GUIStyle(skin.button);
            spinnerPlusBtnStyle.margin = new RectOffset(0, 2, 2, 0);

            // style for derement button
            spinnerMinusBtnStyle = new GUIStyle(skin.button);
            spinnerMinusBtnStyle.margin = new RectOffset(0, 2, 0, 2);

            // style for label to align with increment buttons
            labelStyle = new GUIStyle(skin.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.margin = new RectOffset(4, 4, 5, 3);
            
            boldLabelStyle = new GUIStyle(skin.label);
            boldLabelStyle.fontStyle = FontStyle.Bold;
            boldLabelStyle.alignment = TextAnchor.MiddleLeft;

            optionsWindowStyle = new GUIStyle(skin.window);
            optionsWindowStyle.padding = new RectOffset(0, 0, 0, 0);
            stylesInitialized = true;
        }

        /// <summary>
        /// Draws a label and text box of specified widths with +/- 10% increment buttons. Returns the numeric value of the text box
        /// </summary>
        /// <param name="textFieldGroup">The text field group the input box should have.</param>
        /// <param name="labelText">text for the label</param>
        /// <param name="boxVal">number to display in text box</param>
        /// <param name="labelWidth"></param>
        /// <param name="boxWidth"></param>
        /// <returns>edited value of the text box</returns>
        public static double LabPlusNumBox(
            string textFieldGroup,
            string labelText,
            double boxVal,
            string format,
            float labelWidth = 100,
            float boxWidth = 60)
        {
            string boxText = (format != null) ? boxVal.ToString(format) : boxVal.ToString();
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, LabelStyle, GUILayout.Width(labelWidth));
            GeneralUI.TextFieldNext(textFieldGroup);
            string text = GUILayout.TextField(boxText, NumBoxTextStyle, GUILayout.Width(boxWidth));
            try
            {
                boxVal = double.Parse(text);
            }
            catch {}
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", SpinnerPlusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (boxVal != 0)
                    boxVal *= 1.1;
                else
                    boxVal = 0.01;
            }
            if (GUILayout.Button("-", SpinnerMinusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
            {
                boxVal /= 1.1;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            return boxVal;
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
        public static double TogPlusNumBox(
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
            toggleState = GUILayout.Toggle(toggleState, toggleText, ToggleButtonStyle, GUILayout.Width(toggleWidth));
            GeneralUI.TextFieldNext(textFieldGroup);
            string text = GUILayout.TextField(boxVal.ToString("F2"), NumBoxTextStyle, GUILayout.Width(boxWidth));
            try
            {
                boxVal = double.Parse(text);
            }
            catch {}
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", SpinnerPlusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
            {
                boxVal += 1;
                if (boxVal >= upper)
                    boxVal = lower;
            }
            if (GUILayout.Button("-", SpinnerMinusBtnStyle, GUILayout.Width(20), GUILayout.Height(13)))
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
        public static Texture2D TextureBlock(int w, int h, Color col)
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
