using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant
{
    using Utility;

    enum Display
    {
        Asst,
        SSAS
    }

    enum BindingIndex
    {
        Pause,
        HdgTgl,
        VertTgl,
        ThrtTgl,
        ArmSSAS
    }

    class BindingManager
    {
        static BindingManager instance;
        public static BindingManager Instance
        {
            get
            {
                if (ReferenceEquals(instance, null))
                {
                    instance = new BindingManager();
                }

                return instance;
            }
        }

        public Rect windowRect = new Rect();
        Display selector = Display.Asst;
        GUIContent[] selectorLabels = new GUIContent[2] { new GUIContent("Pilot Assistant"), new GUIContent("SSAS") };

        public static Binding[] bindings;

        public void Start()
        {
            bindings = new Binding[Enum.GetNames(typeof(BindingIndex)).GetLength(0)];
            bindings[(int)BindingIndex.Pause] = new Binding("Pause Control", KeyCode.Tab, KeyCode.None, Display.Asst);
            bindings[(int)BindingIndex.HdgTgl] = new Binding("Toggle Heading Control", KeyCode.Keypad9, KeyCode.LeftAlt, Display.Asst);
            bindings[(int)BindingIndex.VertTgl] = new Binding("Toggle Vert Control", KeyCode.Keypad6, KeyCode.LeftAlt, Display.Asst);
            bindings[(int)BindingIndex.ThrtTgl] = new Binding("Toggle Throttle Control", KeyCode.Keypad3, KeyCode.LeftAlt, Display.Asst);
            bindings[(int)BindingIndex.ArmSSAS] = new Binding("Arm SSAS", GameSettings.SAS_TOGGLE.primary.code, KeyCode.LeftAlt, Display.SSAS);
        }

        public void Draw()
        {
            if (PilotAssistantFlightCore.bDisplayBindings)
            {
                windowRect = GUILayout.Window(6849762, windowRect, DrawWindow, string.Empty);
            }
        }

        void DrawWindow(int id)
        {
            selector = (Display)GUILayout.SelectionGrid((int)selector, selectorLabels, 2);
            if (selector == Display.Asst)
            {
                DrawLabelsInRow("Reduce Target Heading", GameSettings.YAW_LEFT.primary.code);
                DrawLabelsInRow("Increase Target Heading", GameSettings.YAW_RIGHT.primary.code);
                DrawLabelsInRow("Reduce Vert Target", GameSettings.PITCH_DOWN.primary.code);
                DrawLabelsInRow("Increase Vert Target", GameSettings.PITCH_UP.primary.code);
                DrawLabelsInRow("Reduce Target Speed", GameSettings.THROTTLE_DOWN.primary.code);
                DrawLabelsInRow("Increase Target Speed", GameSettings.THROTTLE_UP.primary.code);
                DrawLabelsInRow("Toggle Fine Mode", GameSettings.PRECISION_CTRL.primary.code);
                DrawLabelsInRow("Rate x10", GameSettings.MODIFIER_KEY.primary.code);
                GUILayout.Space(20);
                foreach (Binding b in bindings)
                {
                    if (b.ToDisplay != Display.Asst)
                    {
                        continue;
                    }

                    DrawSetKey(b);
                }
            }
            else if (selector == Display.SSAS)
            {
                DrawSetKey(bindings[(int)BindingIndex.ArmSSAS]);
                DrawLabelsInRow("Toggle SSAS", GameSettings.SAS_TOGGLE.primary.code);
            }
            GUI.DragWindow();
        }

        void DrawLabelsInRow(string Action, KeyCode Keycode)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Action, GUILayout.Width(150));
            GUILayout.Label(Keycode.ToString(), GUILayout.Width(120));
            GUILayout.EndHorizontal();
        }

        void DrawSetKey(Binding keybind)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(keybind.BindingDescription, GUILayout.Width(150));
            keybind.WaitingToSetPrimary = GUILayout.Toggle(keybind.WaitingToSetPrimary, keybind.PrimaryBindingCode.ToString(), GeneralUI.UISkin.customStyles[(int)MyStyles.btnToggle], GUILayout.Width(100));
            if (keybind.WaitingToSetPrimary)
            {
                if (Input.GetMouseButton(0) || Event.current.keyCode == KeyCode.Escape)
                {
                    keybind.WaitingToSetPrimary = false;
                }
                else if (Event.current.type == EventType.KeyDown)
                {
                    keybind.PrimaryBindingCode.code = Event.current.keyCode;
                    keybind.WaitingToSetPrimary = false;
                }
            }
            keybind.WaitingToSetSecondary = GUILayout.Toggle(keybind.WaitingToSetSecondary, keybind.SecondaryBindingCode.ToString(), GeneralUI.UISkin.customStyles[(int)MyStyles.btnToggle], GUILayout.Width(100));
            if (keybind.WaitingToSetSecondary)
            {
                if (Input.GetMouseButton(0) || Event.current.keyCode == KeyCode.Escape)
                {
                    keybind.SecondaryBindingCode.code = KeyCode.None;
                    keybind.WaitingToSetSecondary = false;
                }
                else if (Event.current.type == EventType.KeyDown)
                {
                    keybind.SecondaryBindingCode.code = Event.current.keyCode;
                    keybind.WaitingToSetSecondary = false;
                }
            }
            GUILayout.EndHorizontal();
        }

        public void OnDestroy()
        {
            bindings = null;
            instance = null;
        }

        public class Binding
        {
            public Display ToDisplay { get; set; }
            public string BindingDescription { get; set; }
            public KeyCodeExtended PrimaryBindingCode { get; set; }
            public bool WaitingToSetPrimary { get; set; }
            public KeyCodeExtended SecondaryBindingCode { get; set; }
            public bool WaitingToSetSecondary { get; set; }
            public Binding(string description, KeyCode primary, KeyCode secondary, Display display)
            {
                BindingDescription = description;
                PrimaryBindingCode = new KeyCodeExtended(primary);
                SecondaryBindingCode = new KeyCodeExtended(secondary);
                ToDisplay = display;
            }

            public bool IsPressed
            {
                get
                {
                    return Input.GetKeyDown(PrimaryBindingCode.code) && (SecondaryBindingCode.isNone ? true : Input.GetKey(SecondaryBindingCode.code));
                }
            }
        }
    }
}
