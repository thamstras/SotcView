using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.OGL
{
    internal class Texture : IDisposable
    {
        private readonly GL _gl;
        internal readonly uint _handle;
        private readonly TextureTarget _target;

        public Texture(GL gl, TextureTarget target)
        {
            _gl = gl;
            _target = target;
            _handle = _gl.CreateTexture(_target);

            _gl.TextureParameter(_handle, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
            _gl.TextureParameter(_handle, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
            _gl.TextureParameter(_handle, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            _gl.TextureParameter(_handle, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        }

        public void Dispose()
        {
            _gl.DeleteTexture(_handle);
        }

        public void Param(TextureParameterName param, GLEnum value)
        {
            _gl.TextureParameter(_handle, param, (int)value);
        }

        public void AllocStorage(uint width, uint height, SizedInternalFormat format)
        {
            _gl.TextureStorage2D(_handle, 1, format, width, height);
        }

        public void AllocStorageMultisampled(uint width, uint height, SizedInternalFormat format, uint samples)
        {
            _gl.TextureStorage2DMultisample(_handle, samples, format, width, height, true);
        }

        public void Upload(uint width, uint height, PixelFormat format, ReadOnlySpan<float> data)
        {
            _gl.TextureSubImage2D(_handle, 0, 0, 0, width, height, format, PixelType.Float, data);
        }

        public void Upload(uint width, uint height, PixelFormat format, ReadOnlySpan<byte> data)
        {
            _gl.TextureSubImage2D(_handle, 0, 0, 0, width, height, format, PixelType.UnsignedByte, data);
        }

        public void Upload(uint width, uint height, PixelFormat format, ReadOnlySpan<ushort> data)
        {
            _gl.TextureSubImage2D(_handle, 0, 0, 0, width, height, format, PixelType.UnsignedShort, data);
        }

        public void GenMips()
        {
            _gl.GenerateTextureMipmap(_handle);
        }

        public void Bind(uint textureUnit)
        {
            _gl.BindTextureUnit(textureUnit, _handle);
        }
    }
}
