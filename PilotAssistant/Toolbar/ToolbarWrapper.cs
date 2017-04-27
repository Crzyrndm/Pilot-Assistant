/*
Copyright (c) 2013-2015, Maik Schreiber
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


// TODO: Change to your plugin's namespace here.
namespace PilotAssistant.Toolbar
{



    /**********************************************************\
    *          --- DO NOT EDIT BELOW THIS COMMENT ---          *
    *                                                          *
    * This file contains classes and interfaces to use the     *
    * Toolbar Plugin without creating a hard dependency on it. *
    *                                                          *
    * There is nothing in this file that needs to be edited    *
    * by hand.                                                 *
    *                                                          *
    *          --- DO NOT EDIT BELOW THIS COMMENT ---          *
    \**********************************************************/



    /// <summary>
    /// The global tool bar manager.
    /// </summary>
    public partial class ToolbarManager : IToolbarManager
    {
        /// <summary>
        /// Whether the Toolbar Plugin is available.
        /// </summary>
        public static bool ToolbarAvailable
        {
            get
            {
                if (toolbarAvailable == null)
                {
                    toolbarAvailable = Instance != null;
                }
                return (bool)toolbarAvailable;
            }
        }

        /// <summary>
        /// The global tool bar manager instance.
        /// </summary>
        public static IToolbarManager Instance
        {
            get
            {
                if ((toolbarAvailable != false) && (instance_ == null))
                {
                    Type type = ToolbarTypes.GetType("Toolbar.ToolbarManager");
                    if (type != null)
                    {
                        object realToolbarManager = ToolbarTypes.GetStaticProperty(type, "Instance").GetValue(null, null);
                        instance_ = new ToolbarManager(realToolbarManager);
                    }
                }
                return instance_;
            }
        }
    }

    #region interfaces

    /// <summary>
    /// A toolbar manager.
    /// </summary>
    public interface IToolbarManager
    {
        /// <summary>
        /// Adds a new button.
        /// </summary>
        /// <remarks>
        /// To replace an existing button, just add a new button using the old button's namespace and ID.
        /// Note that the new button will inherit the screen position of the old button.
        /// </remarks>
        /// <param name="ns">The new button's namespace. This is usually the plugin's name. Must not include special characters like '.'</param>
        /// <param name="id">The new button's ID. This ID must be unique across all buttons in the namespace. Must not include special characters like '.'</param>
        /// <returns>The button created.</returns>
        IButton Add(string ns, string id);
    }

    /// <summary>
    /// Represents a clickable button.
    /// </summary>
    public interface IButton
    {
        /// <summary>
        /// The text displayed on the button. Set to null to hide text.
        /// </summary>
        /// <remarks>
        /// The text can be changed at any time to modify the button's appearance. Note that since this will also
        /// modify the button's size, this feature should be used sparingly, if at all.
        /// </remarks>
        /// <seealso cref="TexturePath"/>
        string Text
        {
            set;
            get;
        }

        /// <summary>
        /// The color the button text is displayed with. Defaults to Color.white.
        /// </summary>
        /// <remarks>
        /// The text color can be changed at any time to modify the button's appearance.
        /// </remarks>
        Color TextColor
        {
            set;
            get;
        }

        /// <summary>
        /// The path of a texture file to display an icon on the button. Set to null to hide icon.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A texture path on a button will have precedence over text. That is, if both text and texture path
        /// have been set on a button, the button will show the texture, not the text.
        /// </para>
        /// <para>
        /// The texture size must not exceed 24x24 pixels.
        /// </para>
        /// <para>
        /// The texture path must be relative to the "GameData" directory, and must not specify a file name suffix.
        /// Valid example: MyAddon/Textures/icon_mybutton
        /// </para>
        /// <para>
        /// The texture path can be changed at any time to modify the button's appearance.
        /// </para>
        /// </remarks>
        /// <seealso cref="Text"/>
        string TexturePath
        {
            set;
            get;
        }

        /// <summary>
        /// The button's tool tip text. Set to null if no tool tip is desired.
        /// </summary>
        /// <remarks>
        /// Tool Tip Text Should Always Use Headline Style Like This.
        /// </remarks>
        string ToolTip
        {
            set;
            get;
        }

        /// <summary>
        /// Whether this button is currently visible or not. Can be used in addition to or as a replacement for <see cref="Visibility"/>.
        /// </summary>
        /// <remarks>
        /// Setting this property to true does not affect the player's ability to hide the button using the configuration.
        /// Conversely, setting this property to false does not enable the player to show the button using the configuration.
        /// </remarks>
        bool Visible
        {
            set;
            get;
        }

        /// <summary>
        /// Determines this button's visibility. Can be used in addition to or as a replacement for <see cref="Visible"/>.
        /// </summary>
        /// <remarks>
        /// The return value from IVisibility.Visible is subject to the same rules as outlined for
        /// <see cref="Visible"/>.
        /// </remarks>
        IVisibility Visibility
        {
            set;
            get;
        }

        /// <summary>
        /// Whether this button is currently effectively visible or not. This is a combination of
        /// <see cref="Visible"/> and <see cref="Visibility"/>.
        /// </summary>
        /// <remarks>
        /// Note that the toolbar is not visible in certain game scenes, for example the loading screens. This property
        /// does not reflect button invisibility in those scenes. In addition, this property does not reflect the
        /// player's configuration of the button's visibility.
        /// </remarks>
        bool EffectivelyVisible
        {
            get;
        }

        /// <summary>
        /// Whether this button is currently enabled (clickable) or not. This does not affect the player's ability to
        /// position the button on their toolbar.
        /// </summary>
        bool Enabled
        {
            set;
            get;
        }

        /// <summary>
        /// Whether this button is currently "important." Set to false to return to normal button behaviour.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This can be used to temporarily force the button to be shown on screen regardless of the toolbar being
        /// currently in auto-hidden mode. For example, a button that signals the arrival of a private message in
        /// a chat room could mark itself as "important" as long as the message has not been read.
        /// </para>
        /// <para>
        /// Setting this property does not change the appearance of the button. Use <see cref="TexturePath"/> to
        /// change the button's icon.
        /// </para>
        /// <para>
        /// Setting this property to true does not affect the player's ability to hide the button using the
        /// configuration.
        /// </para>
        /// <para>
        /// This feature should be used only sparingly, if at all, since it forces the button to be displayed on
        /// screen even when it normally wouldn't.
        /// </para>
        /// </remarks>
        bool Important
        {
            set;
            get;
        }

        /// <summary>
        /// A drawable that is tied to the current button. This can be anything from a popup menu to
        /// an informational window. Set to null to hide the drawable.
        /// </summary>
        IDrawable Drawable
        {
            set;
            get;
        }

        /// <summary>
        /// Event handler that can be registered with to receive "on click" events.
        /// </summary>
        /// <example>
        /// <code>
        /// IButton button = ...
        /// button.OnClick += (e) => {
        ///     Debug.Log("button clicked, mouseButton: " + e.MouseButton);
        /// };
        /// </code>
        /// </example>
        event ClickHandler OnClick;

        /// <summary>
        /// Event handler that can be registered with to receive "on mouse enter" events.
        /// </summary>
        /// <example>
        /// <code>
        /// IButton button = ...
        /// button.OnMouseEnter += (e) => {
        ///     Debug.Log("mouse entered button");
        /// };
        /// </code>
        /// </example>
        event MouseEnterHandler OnMouseEnter;

        /// <summary>
        /// Event handler that can be registered with to receive "on mouse leave" events.
        /// </summary>
        /// <example>
        /// <code>
        /// IButton button = ...
        /// button.OnMouseLeave += (e) => {
        ///     Debug.Log("mouse left button");
        /// };
        /// </code>
        /// </example>
        event MouseLeaveHandler OnMouseLeave;

        /// <summary>
        /// Permanently destroys this button so that it is no longer displayed.
        /// Should be used when a plugin is stopped to remove leftover buttons.
        /// </summary>
        void Destroy();
    }

    /// <summary>
    /// A drawable that is tied to a particular button. This can be anything from a popup menu
    /// to an informational window.
    /// </summary>
    public interface IDrawable
    {
        /// <summary>
        /// Update any information. This is called once per frame.
        /// </summary>
        void Update();

        /// <summary>
        /// Draws GUI widgets for this drawable. This is the equivalent to the OnGUI() message in
        /// <see cref="MonoBehaviour"/>.
        /// </summary>
        /// <remarks>
        /// The drawable will be positioned near its parent toolbar according to the drawable's current
        /// width/height.
        /// </remarks>
        /// <param name="position">The left/top position of where to draw this drawable.</param>
        /// <returns>The current width/height of this drawable.</returns>
        Vector2 Draw(Vector2 position);
    }

    #endregion

    #region events

    /// <summary>
    /// Event describing a click on a button.
    /// </summary>
    public partial class ClickEvent : EventArgs
    {
        /// <summary>
        /// The button that has been clicked.
        /// </summary>
        public readonly IButton Button;

        /// <summary>
        /// The mouse button which the button was clicked with.
        /// </summary>
        /// <remarks>
        /// Is 0 for left mouse button, 1 for right mouse button, and 2 for middle mouse button.
        /// </remarks>
        public readonly int MouseButton;
    }

    /// <summary>
    /// An event handler that is invoked whenever a button has been clicked.
    /// </summary>
    /// <param name="e">An event describing the button click.</param>
    public delegate void ClickHandler(ClickEvent e);

    /// <summary>
    /// Event describing the mouse pointer moving about a button.
    /// </summary>
    public abstract partial class MouseMoveEvent
    {
        /// <summary>
        /// The button in question.
        /// </summary>
        public readonly IButton button;
    }

    /// <summary>
    /// Event describing the mouse pointer entering a button's area.
    /// </summary>
    public partial class MouseEnterEvent : MouseMoveEvent
    {
    }

    /// <summary>
    /// Event describing the mouse pointer leaving a button's area.
    /// </summary>
    public partial class MouseLeaveEvent : MouseMoveEvent
    {
    }

    /// <summary>
    /// An event handler that is invoked whenever the mouse pointer enters a button's area.
    /// </summary>
    /// <param name="e">An event describing the mouse pointer entering.</param>
    public delegate void MouseEnterHandler(MouseEnterEvent e);

    /// <summary>
    /// An event handler that is invoked whenever the mouse pointer leaves a button's area.
    /// </summary>
    /// <param name="e">An event describing the mouse pointer leaving.</param>
    public delegate void MouseLeaveHandler(MouseLeaveEvent e);

    #endregion

    #region visibility

    /// <summary>
    /// Determines visibility of a button.
    /// </summary>
    /// <seealso cref="IButton.Visibility"/>
    public interface IVisibility
    {
        /// <summary>
        /// Whether a button is currently visible or not.
        /// </summary>
        /// <seealso cref="IButton.Visible"/>
        bool Visible
        {
            get;
        }
    }

    /// <summary>
    /// Determines visibility of a button in relation to the currently running game scene.
    /// </summary>
    /// <example>
    /// <code>
    /// IButton button = ...
    /// button.Visibility = new GameScenesVisibility(GameScenes.EDITOR, GameScenes.FLIGHT);
    /// </code>
    /// </example>
    /// <seealso cref="IButton.Visibility"/>
    public class GameScenesVisibility : IVisibility
    {
        public bool Visible
        {
            get
            {
                return (bool)visibleProperty.GetValue(realGameScenesVisibility, null);
            }
        }

        private object realGameScenesVisibility;
        private PropertyInfo visibleProperty;

        public GameScenesVisibility(params GameScenes[] gameScenes)
        {
            Type gameScenesVisibilityType = ToolbarTypes.GetType("Toolbar.GameScenesVisibility");
            realGameScenesVisibility = Activator.CreateInstance(gameScenesVisibilityType, new object[] { gameScenes });
            visibleProperty = ToolbarTypes.GetProperty(gameScenesVisibilityType, "Visible");
        }
    }

    #endregion

    #region drawable

    /// <summary>
    /// A drawable that draws a popup menu.
    /// </summary>
    public partial class PopupMenuDrawable : IDrawable
    {
        /// <summary>
        /// Event handler that can be registered with to receive "any menu option clicked" events.
        /// </summary>
        public event Action OnAnyOptionClicked
        {
            add
            {
                onAnyOptionClickedEvent.AddEventHandler(realPopupMenuDrawable, value);
            }
            remove
            {
                onAnyOptionClickedEvent.RemoveEventHandler(realPopupMenuDrawable, value);
            }
        }

        private object realPopupMenuDrawable;
        private MethodInfo updateMethod;
        private MethodInfo drawMethod;
        private MethodInfo addOptionMethod;
        private MethodInfo addSeparatorMethod;
        private MethodInfo destroyMethod;
        private EventInfo onAnyOptionClickedEvent;

        public PopupMenuDrawable()
        {
            Type popupMenuDrawableType = ToolbarTypes.GetType("Toolbar.PopupMenuDrawable");
            realPopupMenuDrawable = Activator.CreateInstance(popupMenuDrawableType, null);
            updateMethod = ToolbarTypes.GetMethod(popupMenuDrawableType, "Update");
            drawMethod = ToolbarTypes.GetMethod(popupMenuDrawableType, "Draw");
            addOptionMethod = ToolbarTypes.GetMethod(popupMenuDrawableType, "AddOption");
            addSeparatorMethod = ToolbarTypes.GetMethod(popupMenuDrawableType, "AddSeparator");
            destroyMethod = ToolbarTypes.GetMethod(popupMenuDrawableType, "Destroy");
            onAnyOptionClickedEvent = ToolbarTypes.GetEvent(popupMenuDrawableType, "OnAnyOptionClicked");
        }

        public void Update()
        {
            updateMethod.Invoke(realPopupMenuDrawable, null);
        }

        public Vector2 Draw(Vector2 position)
        {
            return (Vector2)drawMethod.Invoke(realPopupMenuDrawable, new object[] { position });
        }

        /// <summary>
        /// Adds a new option to the popup menu.
        /// </summary>
        /// <param name="text">The text of the option.</param>
        /// <returns>A button that can be used to register clicks on the menu option.</returns>
        public IButton AddOption(string text)
        {
            object realButton = addOptionMethod.Invoke(realPopupMenuDrawable, new object[] { text });
            return new Button(realButton, new ToolbarTypes());
        }

        /// <summary>
        /// Adds a separator to the popup menu.
        /// </summary>
        public void AddSeparator()
        {
            addSeparatorMethod.Invoke(realPopupMenuDrawable, null);
        }

        /// <summary>
        /// Destroys this drawable. This must always be called before disposing of this drawable.
        /// </summary>
        public void Destroy()
        {
            destroyMethod.Invoke(realPopupMenuDrawable, null);
        }
    }

    #endregion

    #region private implementations

    public partial class ToolbarManager : IToolbarManager
    {
        private static bool? toolbarAvailable = null;
        private static IToolbarManager instance_;

        private object realToolbarManager;
        private MethodInfo addMethod;
        private Dictionary<object, IButton> buttons = new Dictionary<object, IButton>();
        private ToolbarTypes types = new ToolbarTypes();

        private ToolbarManager(object realToolbarManager)
        {
            this.realToolbarManager = realToolbarManager;

            addMethod = ToolbarTypes.GetMethod(types.iToolbarManagerType, "add");
        }

        public IButton Add(string ns, string id)
        {
            object realButton = addMethod.Invoke(realToolbarManager, new object[] { ns, id });
            IButton button = new Button(realButton, types);
            buttons.Add(realButton, button);
            return button;
        }
    }

    internal class Button : IButton
    {
        private object realButton;
        private ToolbarTypes types;
        private Delegate realClickHandler;
        private Delegate realMouseEnterHandler;
        private Delegate realMouseLeaveHandler;

        internal Button(object realButton, ToolbarTypes types)
        {
            this.realButton = realButton;
            this.types = types;

            realClickHandler = AttachEventHandler(types.button.onClickEvent, "clicked", realButton);
            realMouseEnterHandler = AttachEventHandler(types.button.onMouseEnterEvent, "mouseEntered", realButton);
            realMouseLeaveHandler = AttachEventHandler(types.button.onMouseLeaveEvent, "mouseLeft", realButton);
        }

        private Delegate AttachEventHandler(EventInfo @event, string methodName, object realButton)
        {
            MethodInfo method = GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            var d = Delegate.CreateDelegate(@event.EventHandlerType, this, method);
            @event.AddEventHandler(realButton, d);
            return d;
        }

        public string Text
        {
            set
            {
                types.button.textProperty.SetValue(realButton, value, null);
            }
            get
            {
                return (string)types.button.textProperty.GetValue(realButton, null);
            }
        }

        public Color TextColor
        {
            set
            {
                types.button.textColorProperty.SetValue(realButton, value, null);
            }
            get
            {
                return (Color)types.button.textColorProperty.GetValue(realButton, null);
            }
        }

        public string TexturePath
        {
            set
            {
                types.button.texturePathProperty.SetValue(realButton, value, null);
            }
            get
            {
                return (string)types.button.texturePathProperty.GetValue(realButton, null);
            }
        }

        public string ToolTip
        {
            set
            {
                types.button.toolTipProperty.SetValue(realButton, value, null);
            }
            get
            {
                return (string)types.button.toolTipProperty.GetValue(realButton, null);
            }
        }

        public bool Visible
        {
            set
            {
                types.button.visibleProperty.SetValue(realButton, value, null);
            }
            get
            {
                return (bool)types.button.visibleProperty.GetValue(realButton, null);
            }
        }

        public IVisibility Visibility
        {
            set
            {
                object functionVisibility = null;
                if (value != null)
                {
                    functionVisibility = Activator.CreateInstance(types.functionVisibilityType, new object[] { new Func<bool>(() => value.Visible) });
                }
                types.button.visibilityProperty.SetValue(realButton, functionVisibility, null);
                visibility_ = value;
            }
            get
            {
                return visibility_;
            }
        }
        private IVisibility visibility_;

        public bool EffectivelyVisible
        {
            get
            {
                return (bool)types.button.effectivelyVisibleProperty.GetValue(realButton, null);
            }
        }

        public bool Enabled
        {
            set
            {
                types.button.enabledProperty.SetValue(realButton, value, null);
            }
            get
            {
                return (bool)types.button.enabledProperty.GetValue(realButton, null);
            }
        }

        public bool Important
        {
            set
            {
                types.button.importantProperty.SetValue(realButton, value, null);
            }
            get
            {
                return (bool)types.button.importantProperty.GetValue(realButton, null);
            }
        }

        public IDrawable Drawable
        {
            set
            {
                object functionDrawable = null;
                if (value != null)
                {
                    functionDrawable = Activator.CreateInstance(types.functionDrawableType, new object[] {
						new Action(() => value.Update()),
						new Func<Vector2, Vector2>((pos) => value.Draw(pos))
					});
                }
                types.button.drawableProperty.SetValue(realButton, functionDrawable, null);
                drawable_ = value;
            }
            get
            {
                return drawable_;
            }
        }
        private IDrawable drawable_;

        public event ClickHandler OnClick;

        private void Clicked(object realEvent)
        {
            OnClick?.Invoke(new ClickEvent(realEvent, this));
        }

        public event MouseEnterHandler OnMouseEnter;

        private void MouseEntered(object realEvent)
        {
            OnMouseEnter?.Invoke(new MouseEnterEvent(this));
        }

        public event MouseLeaveHandler OnMouseLeave;

        private void MouseLeft(object realEvent)
        {
            OnMouseLeave?.Invoke(new MouseLeaveEvent(this));
        }

        public void Destroy()
        {
            DetachEventHandler(types.button.onClickEvent, realClickHandler, realButton);
            DetachEventHandler(types.button.onMouseEnterEvent, realMouseEnterHandler, realButton);
            DetachEventHandler(types.button.onMouseLeaveEvent, realMouseLeaveHandler, realButton);

            types.button.destroyMethod.Invoke(realButton, null);
        }

        private void DetachEventHandler(EventInfo @event, Delegate d, object realButton)
        {
            @event.RemoveEventHandler(realButton, d);
        }
    }

    public partial class ClickEvent : EventArgs
    {
        internal ClickEvent(object realEvent, IButton button)
        {
            Type type = realEvent.GetType();
            Button = button;
            MouseButton = (int)type.GetField("MouseButton", BindingFlags.Public | BindingFlags.Instance).GetValue(realEvent);
        }
    }

    public abstract partial class MouseMoveEvent : EventArgs
    {
        internal MouseMoveEvent(IButton button)
        {
            this.button = button;
        }
    }

    public partial class MouseEnterEvent : MouseMoveEvent
    {
        internal MouseEnterEvent(IButton button)
            : base(button)
        {
        }
    }

    public partial class MouseLeaveEvent : MouseMoveEvent
    {
        internal MouseLeaveEvent(IButton button)
            : base(button)
        {
        }
    }

    internal class ToolbarTypes
    {
        internal readonly Type iToolbarManagerType;
        internal readonly Type functionVisibilityType;
        internal readonly Type functionDrawableType;
        internal readonly ButtonTypes button;

        internal ToolbarTypes()
        {
            iToolbarManagerType = GetType("Toolbar.IToolbarManager");
            functionVisibilityType = GetType("Toolbar.FunctionVisibility");
            functionDrawableType = GetType("Toolbar.FunctionDrawable");

            Type iButtonType = GetType("Toolbar.IButton");
            button = new ButtonTypes(iButtonType);
        }

        internal static Type GetType(string name)
        {
            Type type = null;
			AssemblyLoader.loadedAssemblies.TypeOperation(t =>
			{
				if (t.FullName == name)
				{
					type = t;
				}
			});
			return type;
        }

        internal static PropertyInfo GetProperty(Type type, string name)
        {
            return type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        }

        internal static PropertyInfo GetStaticProperty(Type type, string name)
        {
            return type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        }

        internal static EventInfo GetEvent(Type type, string name)
        {
            return type.GetEvent(name, BindingFlags.Public | BindingFlags.Instance);
        }

        internal static MethodInfo GetMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        }
    }

    internal class ButtonTypes
    {
        internal readonly Type iButtonType;
        internal readonly PropertyInfo textProperty;
        internal readonly PropertyInfo textColorProperty;
        internal readonly PropertyInfo texturePathProperty;
        internal readonly PropertyInfo toolTipProperty;
        internal readonly PropertyInfo visibleProperty;
        internal readonly PropertyInfo visibilityProperty;
        internal readonly PropertyInfo effectivelyVisibleProperty;
        internal readonly PropertyInfo enabledProperty;
        internal readonly PropertyInfo importantProperty;
        internal readonly PropertyInfo drawableProperty;
        internal readonly EventInfo onClickEvent;
        internal readonly EventInfo onMouseEnterEvent;
        internal readonly EventInfo onMouseLeaveEvent;
        internal readonly MethodInfo destroyMethod;

        internal ButtonTypes(Type iButtonType)
        {
            this.iButtonType = iButtonType;

            textProperty = ToolbarTypes.GetProperty(iButtonType, "Text");
            textColorProperty = ToolbarTypes.GetProperty(iButtonType, "TextColor");
            texturePathProperty = ToolbarTypes.GetProperty(iButtonType, "TexturePath");
            toolTipProperty = ToolbarTypes.GetProperty(iButtonType, "ToolTip");
            visibleProperty = ToolbarTypes.GetProperty(iButtonType, "Visible");
            visibilityProperty = ToolbarTypes.GetProperty(iButtonType, "Visibility");
            effectivelyVisibleProperty = ToolbarTypes.GetProperty(iButtonType, "EffectivelyVisible");
            enabledProperty = ToolbarTypes.GetProperty(iButtonType, "Enabled");
            importantProperty = ToolbarTypes.GetProperty(iButtonType, "Important");
            drawableProperty = ToolbarTypes.GetProperty(iButtonType, "Drawable");
            onClickEvent = ToolbarTypes.GetEvent(iButtonType, "OnClick");
            onMouseEnterEvent = ToolbarTypes.GetEvent(iButtonType, "OnMouseEnter");
            onMouseLeaveEvent = ToolbarTypes.GetEvent(iButtonType, "OnMouseLeave");
            destroyMethod = ToolbarTypes.GetMethod(iButtonType, "Destroy");
        }
    }

    #endregion
}
