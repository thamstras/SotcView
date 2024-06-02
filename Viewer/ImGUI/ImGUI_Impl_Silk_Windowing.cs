using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using System.Runtime.InteropServices;
using System.Numerics;
using Silk.NET.Input;

namespace Viewer.ImGUI
{
    internal class ImGUI_Impl_Silk_Windowing : IDisposable
    {
        private static readonly string backend_platform_name = "ImGui_Impl_Silk_Windowing";
        // We don't need the user data pointer, but we still want to set it to a non-zero value.
        private static readonly uint backend_platform_tag = 0x4B4C4953;   // "SILK"

        private IWindow Window;
        //ClientApi
        private double Time;
        //MouseWindow
        private StandardCursor[] MouseCursors;
        Vector2 LastValidMousePos;
        //KeyOwnerWindows
        //InstalledCallbacks
        //WantUpdateMonitors
        private IInputContext InputContext;

        public ImGUI_Impl_Silk_Windowing(IWindow window, IInputContext input)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            System.Diagnostics.Debug.Assert(io.BackendPlatformUserData == 0, "Already initialized a platform backend!");

            io.BackendPlatformUserData = (nint)backend_platform_tag;
            IntPtr cstr = Marshal.StringToHGlobalAnsi(backend_platform_name);
            unsafe
            {
                byte* pcstr = (byte*)cstr.ToPointer();
                io.NativePtr->BackendPlatformName = pcstr;
            }

            Window = window;
            Time = 0.0;
            InputContext = input;
            //Silk.NET.Windowing.Monitor.GetMonitors(window);

            // TODO: Clipboard handling

            MouseCursors = new StandardCursor[(int)ImGuiMouseCursor.COUNT];
            for (int i = 0; i < MouseCursors.Length; i++) { MouseCursors[i] = StandardCursor.Default; }

            Window.FocusChanged += WindowFocusCallback;
            if (InputContext.Mice.Count > 0)
            {
                InputContext.Mice[0].MouseMove += CursorPosCallback;
                InputContext.Mice[0].MouseDown += MouseDownCallback;
                InputContext.Mice[0].MouseUp += MouseUpCallback;
                InputContext.Mice[0].Scroll += MouseScrollCallback;
                ConfigCursors(InputContext.Mice[0].Cursor);
            }
            if (InputContext.Keyboards.Count > 0)
            {
                InputContext.Keyboards[0].KeyDown += KeyDownCallback;
                InputContext.Keyboards[0].KeyUp += KeyUpCallback;
                InputContext.Keyboards[0].KeyChar += KeyCharCallback;
            }
            // TODO: InputContext.ConnectionChanged
        }

        private void ConfigCursors(ICursor cursor)
        {
            foreach (var imguiCursor in Enum.GetValues<ImGuiMouseCursor>())
            {
                if (imguiCursor == ImGuiMouseCursor.None || imguiCursor == ImGuiMouseCursor.COUNT) continue;
                var stdCursor = imguiCursor switch
                {
                    ImGuiMouseCursor.Arrow => StandardCursor.Arrow,
                    ImGuiMouseCursor.TextInput => StandardCursor.IBeam,
                    ImGuiMouseCursor.ResizeAll => StandardCursor.ResizeAll,
                    ImGuiMouseCursor.ResizeNS => StandardCursor.VResize,
                    ImGuiMouseCursor.ResizeEW => StandardCursor.HResize,
                    ImGuiMouseCursor.ResizeNESW => StandardCursor.NeswResize,
                    ImGuiMouseCursor.ResizeNWSE => StandardCursor.NwseResize,
                    ImGuiMouseCursor.Hand => StandardCursor.Hand,
                    ImGuiMouseCursor.NotAllowed => StandardCursor.NotAllowed,
                    _ => StandardCursor.Default,
                };
                if (cursor.IsSupported(stdCursor))
                    MouseCursors[(int)imguiCursor] = stdCursor;
            }
        }

        private void KeyCharCallback(IKeyboard arg1, char arg2)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.AddInputCharacterUTF16(arg2);
        }

        static ImGuiKey TranslateKey(Key key)
        {
            return key switch
            {
                Key.Space => ImGuiKey.Space,
                Key.Apostrophe => ImGuiKey.Apostrophe,
                Key.Comma => ImGuiKey.Comma,
                Key.Minus => ImGuiKey.Minus,
                Key.Period => ImGuiKey.Period,
                Key.Slash => ImGuiKey.Slash,
                >= Key.Number0 and <= Key.Number9 => ImGuiKey._0 + (key - Key.Number0),
                Key.Semicolon => ImGuiKey.Semicolon,
                Key.Equal => ImGuiKey.Equal,
                >= Key.A and <= Key.Z => ImGuiKey.A + (key - Key.A),
                Key.LeftBracket => ImGuiKey.LeftBracket,
                Key.BackSlash => ImGuiKey.Backslash,
                Key.RightBracket => ImGuiKey.RightBracket,
                Key.GraveAccent => ImGuiKey.GraveAccent,
                Key.Escape => ImGuiKey.Escape,
                Key.Enter => ImGuiKey.Enter,
                Key.Tab => ImGuiKey.Tab,
                Key.Backspace => ImGuiKey.Backspace,
                Key.Insert => ImGuiKey.Insert,
                Key.Delete => ImGuiKey.Delete,
                Key.Right => ImGuiKey.RightArrow,
                Key.Left => ImGuiKey.LeftArrow,
                Key.Down => ImGuiKey.DownArrow,
                Key.Up => ImGuiKey.UpArrow,
                Key.PageUp => ImGuiKey.PageUp,
                Key.PageDown => ImGuiKey.PageDown,
                Key.Home => ImGuiKey.Home,
                Key.End => ImGuiKey.End,
                Key.CapsLock => ImGuiKey.CapsLock,
                Key.ScrollLock => ImGuiKey.ScrollLock,
                Key.NumLock => ImGuiKey.NumLock,
                Key.PrintScreen => ImGuiKey.PrintScreen,
                Key.Pause => ImGuiKey.Pause,
                >= Key.F1 and <= Key.F24 => ImGuiKey.F1 + (key - Key.F1),
                >= Key.Keypad0 and <= Key.Keypad9 => ImGuiKey.Keypad0 + (key - Key.Keypad0),
                Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
                Key.KeypadDivide => ImGuiKey.KeypadDivide,
                Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
                Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
                Key.KeypadAdd => ImGuiKey.KeypadAdd,
                Key.KeypadEnter => ImGuiKey.KeypadEnter,
                Key.KeypadEqual => ImGuiKey.KeypadEqual,
                Key.ShiftLeft => ImGuiKey.LeftShift,
                Key.ControlLeft => ImGuiKey.LeftCtrl,
                Key.AltLeft => ImGuiKey.LeftAlt,
                Key.SuperLeft => ImGuiKey.LeftSuper,
                Key.ShiftRight => ImGuiKey.RightShift,
                Key.ControlRight => ImGuiKey.RightCtrl,
                Key.AltRight => ImGuiKey.RightAlt,
                Key.SuperRight => ImGuiKey.RightSuper,
                Key.Menu => ImGuiKey.Menu,
                _ => ImGuiKey.None,
            };
        }

        private void HandleModKey(ImGuiKey key, bool down)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            if (key == ImGuiKey.LeftAlt || key == ImGuiKey.RightAlt)
            {
                io.AddKeyEvent(ImGuiKey.ModAlt, down);
            }
            else if (key == ImGuiKey.LeftCtrl || key == ImGuiKey.RightCtrl)
            {
                io.AddKeyEvent(ImGuiKey.ModCtrl, down);
            }
            else if (key == ImGuiKey.LeftShift || key == ImGuiKey.RightShift)
            {
                io.AddKeyEvent(ImGuiKey.ModShift, down);
            }
            else if (key == ImGuiKey.LeftSuper || key == ImGuiKey.RightSuper)
            {
                io.AddKeyEvent(ImGuiKey.ModSuper, down);
            }
        }

        private void KeyUpCallback(IKeyboard keyboard, Key key, int scancode)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            ImGuiKey imGuiKey = TranslateKey(key);
            io.AddKeyEvent(imGuiKey, false);
            io.SetKeyEventNativeData(imGuiKey, (int)key, scancode);
            HandleModKey(imGuiKey, false);
        }

        private void KeyDownCallback(IKeyboard keyboard, Key key, int scancode)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            ImGuiKey imGuiKey = TranslateKey(key);
            io.AddKeyEvent(imGuiKey, true);
            io.SetKeyEventNativeData(imGuiKey, (int)key, scancode);
            HandleModKey(imGuiKey, true);
        }

        private void MouseScrollCallback(IMouse mouse, ScrollWheel scroll)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.AddMouseWheelEvent(scroll.X, scroll.Y);
        }

        private void MouseUpCallback(IMouse mouse, MouseButton button)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            int btn = (int)button;
            if (btn >= 0 && btn < (int)ImGuiMouseButton.COUNT)
                io.AddMouseButtonEvent(btn, false);
        }

        private void MouseDownCallback(IMouse mouse, MouseButton button)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            int btn = (int)button;
            if (btn >= 0 && btn < (int)ImGuiMouseButton.COUNT)
                io.AddMouseButtonEvent(btn, true);
        }

        private void WindowFocusCallback(bool focused)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.AddFocusEvent(focused);
        }

        private void CursorPosCallback(IMouse mouse, Vector2 pos)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.AddMousePosEvent(pos.X, pos.Y);
            LastValidMousePos = pos;
        }

        public void Dispose()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            System.Diagnostics.Debug.Assert(io.BackendPlatformUserData != 0, "No platform to shutdown, or already shutdown?");
            io.BackendPlatformUserData = 0;
            unsafe
            {
                byte* ptr = io.NativePtr->BackendPlatformName;
                io.NativePtr->BackendPlatformName = (byte*)0;
                Marshal.FreeHGlobal((nint)ptr);
            }

            Window.FocusChanged -= WindowFocusCallback;
            if (InputContext.Mice.Count > 0)
            {
                InputContext.Mice[0].MouseMove -= CursorPosCallback;
                InputContext.Mice[0].MouseDown -= MouseDownCallback;
                InputContext.Mice[0].MouseUp -= MouseUpCallback;
                InputContext.Mice[0].Scroll -= MouseScrollCallback;
            }
            if (InputContext.Keyboards.Count > 0)
            {
                InputContext.Keyboards[0].KeyDown -= KeyDownCallback;
                InputContext.Keyboards[0].KeyUp -= KeyUpCallback;
                InputContext.Keyboards[0].KeyChar -= KeyCharCallback;
            }
            // TODO: InputContext.ConnectionChanged
        }

        public void NewFrame()
        {
            ImGuiIOPtr io = ImGui.GetIO();

            // Setup display size
            var winSize = Window.Size;
            var fbufSize = Window.FramebufferSize;
            io.DisplaySize = new Vector2(winSize.X, winSize.Y);
            if (winSize.X > 0 && winSize.Y > 0)
            {
                io.DisplayFramebufferScale = new Vector2((float)fbufSize.X / winSize.X, (float)fbufSize.Y / winSize.Y);
            }
            
            // Setup timestep
            var currentTime = Window.Time;
            io.DeltaTime = Time > 0.0 ? (float)(currentTime - Time) : (float)(1.0f / Window.FramesPerSecond);
            Time = currentTime;

            // Currently broken
            //var imgui_cursor = ImGui.GetMouseCursor();
            //InputContext.Mice[0].Cursor.StandardCursor = MouseCursors[(int)imgui_cursor];
        }
    }
}
