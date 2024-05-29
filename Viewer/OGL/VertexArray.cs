using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.OGL
{
    internal class VertexArray : IDisposable
    {
        private readonly GL _gl;
        private readonly uint _handle;

        public VertexArray(GL gl)
        {
            _gl = gl;
            _handle = _gl.CreateVertexArray();
        }

        public void Dispose()
        {
            _gl.DeleteVertexArray(_handle);
        }

        public void BindVertexBuffer<T>(uint bindingIdx, Buffer<T> buffer, int offset, uint stride) where T : unmanaged
        {
            _gl.VertexArrayVertexBuffer(_handle, bindingIdx, buffer._handle, offset, stride);
        }

        public void BindElementBuffer<T>(Buffer<T> buffer) where T : unmanaged
        {
            _gl.VertexArrayElementBuffer(_handle, buffer._handle);
        }

        public void SetAttribFormat(uint idx, int size, VertexAttribType type, bool normalized, uint offset)
        {
            _gl.EnableVertexArrayAttrib(_handle, idx);
            _gl.VertexArrayAttribFormat(_handle, idx, size, type, normalized, offset);
        }

        public void SetAttribBinding(uint attribIdx, uint bindingIdx)
        {
            _gl.VertexArrayAttribBinding(_handle, attribIdx, bindingIdx);
        }

        public void Use()
        {
            _gl.BindVertexArray(_handle);
        }
    }
}
