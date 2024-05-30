using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer
{
    internal static class Widgets
    {
        public static void TreeNode(string name, Action contents)
        {
            if (ImGui.TreeNode(name))
            {
                contents();
                ImGui.TreePop();
            }
        }

        public static void Window(string name, Action contents)
        {
            if (ImGui.Begin(name))
            {
                contents();
            }
            ImGui.End();
        }

        public static void Window(string name, ref bool isOpen, Action contents)
        {
            if (ImGui.Begin(name, ref isOpen))
            {
                contents();
            }
            ImGui.End();
        }
    }
}
