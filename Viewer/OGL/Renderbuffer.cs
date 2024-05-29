using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.OGL
{
    internal class Renderbuffer : IDisposable
    {
        private readonly GL _gl;
        internal readonly uint _handle;

        public Renderbuffer(GL gl)
        {
            _gl = gl;
            _handle = _gl.CreateRenderbuffer();
        }

        public void Dispose()
        {
            _gl.DeleteRenderbuffer(_handle);
        }

        public void AllocStorage(InternalFormat format, uint width, uint height)
        {
            _gl.NamedRenderbufferStorage(_handle, format, width, height);
        }

        public void AllocStorageMultisampled(InternalFormat format, uint width, uint height, uint samples)
        {
            _gl.NamedRenderbufferStorageMultisample(_handle, samples, format, width, height);
        }
    }
}
