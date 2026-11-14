using System;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace Vocalith.RuntimeAutomation.InputSystemSupport
{
    [InputControlLayout(
        displayName = "Runtime Automation Keyboard",
        stateType = typeof(KeyboardState),
        canRunInBackground = true)]
    public sealed class RuntimeAutomationKeyboard : Keyboard
    {
    }

    [InputControlLayout(
        displayName = "Runtime Automation Mouse",
        stateType = typeof(MouseState),
        canRunInBackground = true)]
    public sealed class RuntimeAutomationMouse : Mouse
    {
    }

    public sealed class RuntimeAutomationInputDeviceLease : IDisposable
    {
        private Keyboard previousKeyboard;
        private Mouse previousMouse;
        private string keyboardLayoutName;
        private string mouseLayoutName;
        private bool disposed;

        public RuntimeAutomationInputDeviceLease(string namePrefix)
        {
            string prefix = string.IsNullOrWhiteSpace(namePrefix) ? "RuntimeAutomation" : namePrefix.Trim();
            previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
            previousMouse = UnityEngine.InputSystem.Mouse.current;
            keyboardLayoutName = prefix + "KeyboardLayout";
            mouseLayoutName = prefix + "MouseLayout";
            try
            {
                InputSystem.RegisterLayout<RuntimeAutomationKeyboard>(keyboardLayoutName);
                InputSystem.RegisterLayout<RuntimeAutomationMouse>(mouseLayoutName);
                Keyboard = (RuntimeAutomationKeyboard)InputSystem.AddDevice(keyboardLayoutName, prefix + "Keyboard");
                Mouse = (RuntimeAutomationMouse)InputSystem.AddDevice(mouseLayoutName, prefix + "Mouse");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public RuntimeAutomationKeyboard Keyboard { get; private set; }
        public RuntimeAutomationMouse Mouse { get; private set; }

        public JObject CaptureDiagnostics()
        {
            return new JObject
            {
                ["keyboard_added"] = Keyboard != null && Keyboard.added,
                ["keyboard_name"] = Keyboard?.name,
                ["keyboard_can_run_in_background"] = Keyboard?.canRunInBackground ?? false,
                ["keyboard_layout_background_override"] = Keyboard != null &&
                    InputSystem.LoadLayout(Keyboard.layout)?.canRunInBackground == true,
                ["mouse_added"] = Mouse != null && Mouse.added,
                ["mouse_name"] = Mouse?.name,
                ["mouse_can_run_in_background"] = Mouse?.canRunInBackground ?? false,
                ["mouse_layout_background_override"] = Mouse != null &&
                    InputSystem.LoadLayout(Mouse.layout)?.canRunInBackground == true,
                ["keyboard_current"] = ReferenceEquals(UnityEngine.InputSystem.Keyboard.current, Keyboard),
                ["mouse_current"] = ReferenceEquals(UnityEngine.InputSystem.Mouse.current, Mouse),
            };
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (Keyboard != null && Keyboard.added) InputSystem.RemoveDevice(Keyboard);
                if (Mouse != null && Mouse.added) InputSystem.RemoveDevice(Mouse);
                if (!string.IsNullOrWhiteSpace(keyboardLayoutName)) InputSystem.RemoveLayout(keyboardLayoutName);
                if (!string.IsNullOrWhiteSpace(mouseLayoutName)) InputSystem.RemoveLayout(mouseLayoutName);
            }
            finally
            {
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                Keyboard = null;
                Mouse = null;
                previousKeyboard = null;
                previousMouse = null;
                keyboardLayoutName = null;
                mouseLayoutName = null;
            }
        }
    }
}
