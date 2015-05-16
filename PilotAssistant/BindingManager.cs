using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant
{
    using Utility;

    enum display
    {
        Asst,
        SSAS
    }
    class BindingManager
    {
        static BindingManager instance;
        public static BindingManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new BindingManager();
                return instance;
            }
        }

        public Rect windowRect = new Rect();
        display selector = display.Asst;
        GUIContent[] selectorLabels = new GUIContent[2] { new GUIContent("Pilot Assistant"), new GUIContent("SSAS") };

        public static Binding AsstPauseBinding = new Binding(KeyCode.Tab, KeyCode.None);
        public static Binding AsstHdgToggleBinding = new Binding(KeyCode.Keypad9, KeyCode.LeftAlt);
        public static Binding AsstVertToggleBinding = new Binding(KeyCode.Keypad6, KeyCode.LeftAlt);
        public static Binding AsstThrtToggleBinding = new Binding(KeyCode.Keypad3, KeyCode.LeftAlt);

        public void Start()
        {

        }

        public void Draw()
        {
            if (AppLauncherFlight.bDisplayBindings)
                windowRect = GUILayout.Window(6849762, windowRect, drawWindow, "");
        }

        void drawWindow(int id)
        {
            selector = (display)GUILayout.SelectionGrid((int)selector, selectorLabels, 2);
            if (selector == display.Asst)
            {
                drawLabelsInRow("Reduce Target Heading", GameSettings.YAW_LEFT.primary);
                drawLabelsInRow("Increase Target Heading", GameSettings.YAW_RIGHT.primary);
                drawLabelsInRow("Reduce Vert Target", GameSettings.PITCH_DOWN.primary);
                drawLabelsInRow("Increase Vert Target", GameSettings.PITCH_UP.primary);
                drawLabelsInRow("Reduce Target Speed", GameSettings.THROTTLE_DOWN.primary);
                drawLabelsInRow("Increase Target Speed", GameSettings.THROTTLE_UP.primary);
                drawLabelsInRow("Toggle Fine Mode", GameSettings.PRECISION_CTRL.primary);
                drawLabelsInRow("Rate x10", GameSettings.MODIFIER_KEY.primary);
                GUILayout.Space(20);
                drawSetKey("Pause Control", AsstPauseBinding);
                drawSetKey("Toggle Heading Control", AsstHdgToggleBinding);
                drawSetKey("Toggle Vert Control", AsstVertToggleBinding);
                drawSetKey("Toggle Throttle Control", AsstThrtToggleBinding);
            }
            else if (selector == display.SSAS)
            {

            }
            GUI.DragWindow();
        }

        void drawLabelsInRow(string Action, KeyCode Keycode)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Action, GUILayout.Width(150));
            GUILayout.Label(Keycode.ToString(), GUILayout.Width(120));
            GUILayout.EndHorizontal();
        }

        void drawSetKey(string Action, Binding keybind)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Action, GUILayout.Width(150));
            keybind.waitingToSetPrimary = GUILayout.Toggle(keybind.waitingToSetPrimary, keybind.primaryBindingCode.ToString(), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            if (keybind.waitingToSetPrimary)
            {
                if (Input.GetMouseButton(0) || Event.current.keyCode == KeyCode.Escape)
                    keybind.waitingToSetPrimary = false;
                else if (Event.current.type == EventType.KeyDown)
                {
                    keybind.primaryBindingCode = Event.current.keyCode;
                    keybind.waitingToSetPrimary = false;
                }
            }
            keybind.waitingToSetSecondary = GUILayout.Toggle(keybind.waitingToSetSecondary, keybind.secondaryBindingCode.ToString(), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            if (keybind.waitingToSetSecondary)
            {
                if (Input.GetMouseButton(0) || Event.current.keyCode == KeyCode.Escape)
                {
                    keybind.secondaryBindingCode = KeyCode.None;
                    keybind.waitingToSetSecondary = false;
                }
                else if (Event.current.type == EventType.KeyDown)
                {
                    keybind.secondaryBindingCode = Event.current.keyCode;
                    keybind.waitingToSetSecondary = false;
                }
            }
            GUILayout.EndHorizontal();
        }

        public void OnDestroy()
        {

        }

        public class Binding
        {
            public KeyCode primaryBindingCode { get; set; }
            public bool waitingToSetPrimary { get; set; }
            public KeyCode secondaryBindingCode { get; set; }
            public bool waitingToSetSecondary { get; set; }
            public Binding(KeyCode primary, KeyCode secondary)
            {
                primaryBindingCode = primary;
                secondaryBindingCode = secondary;
            }

            public bool isPressed
            {
                get
                {
                    return Input.GetKeyDown(primaryBindingCode) && (secondaryBindingCode == KeyCode.None ? true : Input.GetKey(secondaryBindingCode));
                }
            }
        }
    }
}
