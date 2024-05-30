using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viewer.OGL;

namespace Viewer.Graphics
{
    internal class Image : IDisposable
    {
        public string Name { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] PixelData { get; set; }

        private readonly GL _gl;
        public OGL.Texture Texture { get; private set; }

        public Image(GL gl, string name, int width, int height, byte[] pixelData)
        {
            _gl = gl;
            Name = name;
            Width = width;
            Height = height;
            PixelData = pixelData;

            Texture = new OGL.Texture(_gl, TextureTarget.Texture2D);
            Texture.AllocStorage((uint)width, (uint)height, SizedInternalFormat.Rgba8);
            Texture.Upload((uint)width, (uint)height, PixelFormat.Rgba, pixelData);
            //Texture.Param(TextureParameterName.TextureWrapS, GLEnum.Repeat);
            //Texture.Param(TextureParameterName.TextureWrapT, GLEnum.Repeat);
            //Texture.Param(TextureParameterName.TextureMagFilter, GLEnum.Linear);
            //Texture.Param(TextureParameterName.TextureMinFilter, GLEnum.LinearMipmapLinear);
            Texture.GenMips();
        }

        public void Dispose()
        {
            Texture.Dispose();
        }
    }
}
