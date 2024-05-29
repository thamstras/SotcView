using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.OGL
{
    internal class Framebuffer : IDisposable
    {
        private readonly GL _gl;
        internal readonly uint _handle;

        public Framebuffer(GL gl)
        {
            _gl = gl;
            _handle = _gl.CreateFramebuffer();
        }

        public void Dispose()
        {
            _gl.DeleteFramebuffer(_handle);
        }

        public void Attatch(FramebufferAttachment target, Texture texture)
        {
            _gl.NamedFramebufferTexture(_handle, target, texture._handle, 0);
        }

        public void Attatch(FramebufferAttachment target, Renderbuffer buffer)
        {
            _gl.NamedFramebufferRenderbuffer(_handle, target, RenderbufferTarget.Renderbuffer, buffer._handle);
        }

        public bool IsComplete()
        {
            if (_gl.CheckNamedFramebufferStatus(_handle, FramebufferTarget.DrawFramebuffer) == GLEnum.FramebufferComplete)
                return true;
            
            return false;
        }

        public void Bind(FramebufferTarget target)
        {
            _gl.BindFramebuffer(target, _handle);
        }
    }
}
