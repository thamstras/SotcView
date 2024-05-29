using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.ImGUI
{
    internal class ImGUIAdapter : IDisposable
    {
        // TODO: Make these interfaces so we can swap out (ie: opengl <-> vulkan)
        private ImGUI_Impl_Silk_OpenGL3 render;
        private ImGUI_Impl_Silk_Windowing platform;

        private IntPtr Context;

        public ImGUIAdapter(GL gl, IWindow window, IInputContext input, Action<ImGuiIOPtr> configure)
        {
            Context = ImGui.CreateContext();
            ImGui.SetCurrentContext(Context);
            
            ImGuiIOPtr io = ImGui.GetIO();
            ImGui.StyleColorsDark();
            ImGui.GetStyle().ColorButtonPosition = ImGuiDir.Right;
            
            configure(io);

            platform = new ImGUI_Impl_Silk_Windowing(window, input);
            render = new ImGUI_Impl_Silk_OpenGL3(gl);
        }

        // DESIGN NOTE: NewFrame MUST be called BEFORE any ImGui calls. Render MUST be called AFTER all ImGui calls.
        //     This is why we don't bind to the window's update/render events, because we can't guarantee ordering.

        public void NewFrame()
        {
            render.NewFrame();
            platform.NewFrame();
            ImGui.NewFrame();
        }

        public void Render()
        {
            ImGui.Render();
            render.RenderDrawdata(ImGui.GetDrawData());
        }

        public void Dispose()
        {
            render?.Dispose();
            platform?.Dispose();
        }
    }
}
