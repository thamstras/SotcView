using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.OGL
{
    internal class Buffer<TDataType> : IDisposable where TDataType : unmanaged
    {
        private readonly GL _gl;
        private readonly BufferTargetARB _target;
        internal readonly uint _handle;

        public Buffer(GL gl, BufferTargetARB target, ReadOnlySpan<TDataType> data, VertexBufferObjectUsage usage)
        {
            _gl = gl;
            _target = target;

            _handle = _gl.CreateBuffer();
            BufferData(data, usage);
        }

        public void Bind()
        {
            _gl.BindBuffer(_target, _handle);
        }

        public void BufferData(ReadOnlySpan<TDataType> data, VertexBufferObjectUsage usage)
        {
            _gl.NamedBufferData<TDataType>(_handle, data, usage);
        }

        public void BufferSubData(int offset, ReadOnlySpan<TDataType> data)
        {
            _gl.NamedBufferSubData<TDataType>(_handle, offset, data);
        }

        public void Dispose()
        {
            _gl.DeleteBuffer(_handle);
        }
    }
}
