using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.Graphics
{
    internal class Scene
    {
        private readonly GL _gl;
        private readonly IWindow _window;

        internal readonly Framebuffer framebuffer;
        internal readonly Camera camera;

        internal readonly Dictionary<string, Image> loadedTextures;

        public Scene(GL gl, IWindow window)
        {
            _gl = gl;
            _window = window;
            camera = new Camera();
            framebuffer = new Framebuffer(_gl, 1240, 720, 4);
            loadedTextures = new Dictionary<string, Image>();
        }

        public void Resize(uint width, uint height)
        {
            framebuffer.Resize(width, height);
        }
    }
}
