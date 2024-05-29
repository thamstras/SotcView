using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.Graphics
{
    internal class Framebuffer : IDisposable
    {
        GL _gl;

        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public uint SampleCount { get; init; }

        [MemberNotNullWhen(true, nameof(multisampleBuffer), nameof(multisampleDepth), nameof(multisampleTexture))]
        public bool IsMultisampled { get => SampleCount > 1; }

        OGL.Framebuffer mainBuffer;
        OGL.Renderbuffer mainDepth;
        OGL.Texture mainTexture;

        OGL.Framebuffer? multisampleBuffer;
        OGL.Renderbuffer? multisampleDepth;
        OGL.Texture? multisampleTexture;

        public Framebuffer(GL gl, uint width, uint height, uint sampleCount)
        {
            _gl = gl;
            Width = width;
            Height = height;
            SampleCount = sampleCount;

            mainBuffer = new OGL.Framebuffer(_gl);
            mainDepth = new OGL.Renderbuffer(_gl);
            mainTexture = new OGL.Texture(_gl, TextureTarget.Texture2D);

            mainTexture.AllocStorage(width, height, SizedInternalFormat.Rgb8);
            mainTexture.Param(TextureParameterName.TextureMinFilter, GLEnum.Linear);
            mainTexture.Param(TextureParameterName.TextureMagFilter, GLEnum.Linear);
            mainDepth.AllocStorage(InternalFormat.Depth24Stencil8, Width, Height);

            mainBuffer.Attatch(FramebufferAttachment.ColorAttachment0, mainTexture);
            mainBuffer.Attatch(FramebufferAttachment.DepthStencilAttachment, mainDepth);

            if (!mainBuffer.IsComplete())
                throw new ApplicationException("Framebuffer is not complete!");

            if (IsMultisampled)
            {
                multisampleTexture = new OGL.Texture(_gl, TextureTarget.Texture2DMultisample);
                multisampleTexture.AllocStorageMultisampled(Width, Height, SizedInternalFormat.Rgb8, SampleCount);
                multisampleTexture.Param(TextureParameterName.TextureMinFilter, GLEnum.Linear);
                multisampleTexture.Param(TextureParameterName.TextureMagFilter, GLEnum.Linear);

                multisampleDepth = new OGL.Renderbuffer(_gl);
                multisampleDepth.AllocStorageMultisampled(InternalFormat.Depth24Stencil8, Width, Height, SampleCount);

                multisampleBuffer = new OGL.Framebuffer(_gl);
                multisampleBuffer.Attatch(FramebufferAttachment.ColorAttachment0, multisampleTexture);
                multisampleBuffer.Attatch(FramebufferAttachment.DepthStencilAttachment, multisampleDepth);

                if (!multisampleBuffer.IsComplete())
                    throw new ApplicationException("Multisample Framebuffer is not complete!");
            }
        }

        public void Dispose()
        {
            mainBuffer.Dispose();
            mainTexture.Dispose();
            mainDepth.Dispose();
            if (IsMultisampled)
            {
                multisampleBuffer.Dispose();
                multisampleTexture.Dispose();
                multisampleDepth.Dispose();
            }
        }

        public void Use()
        {
            if (IsMultisampled)
                multisampleBuffer.Bind(FramebufferTarget.DrawFramebuffer);
            else
                mainBuffer.Bind(FramebufferTarget.DrawFramebuffer);

            _gl.Viewport(0, 0, Width, Height);
        }

        public OGL.Texture Resolve()
        {
            if (IsMultisampled)
                _gl.BlitNamedFramebuffer(multisampleBuffer._handle, mainBuffer._handle, 0, 0, (int)Width, (int)Height, 0, 0, (int)Width, (int)Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

            return mainTexture;
        }

        public void Resize(uint width, uint height)
        {
            // TODO: OOPS. We're using immutable texture storage...
        }
    }
}
