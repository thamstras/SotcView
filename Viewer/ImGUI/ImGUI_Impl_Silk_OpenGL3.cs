using ImGuiNET;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Viewer.ImGUI
{
    internal class ImGUI_Impl_Silk_OpenGL3 : IDisposable
    {
        private static readonly string backend_render_name = "ImGui_Impl_Silk_OpenGL3";
        // We don't need the user data pointer, but we still want to set it to a non-zero value.
        private static readonly uint backend_render_tag = 0x4B4C4953;   // "SILK"

        private readonly GL _gl;

        // BEGIN BACKEND DATA
        private uint GlVersion;
        private string GlslVersionString;
        private uint FontTexture;
        private uint ShaderHandle;
        private int AttribLocationTex;
        private int AttribLocationProjMtx;
        private uint AttribLocationVtxPos;
        private uint AttribLocationVtxUV;
        private uint AttribLocationVtxColor;
        private uint VboHandle;
        private uint ElementsHandle;
        private int VertexBufferSize;
        private int IndexBufferSize;
        private bool HasClipOrigin;
        private bool UseBufferSubData;
        // END BACKEND DATA

        public ImGUI_Impl_Silk_OpenGL3(GL gl)
        {
            _gl = gl;

            ImGuiIOPtr io = ImGui.GetIO();
            System.Diagnostics.Debug.Assert(io.BackendRendererUserData == 0, "Already initialized a renderer backend!");

            io.BackendRendererUserData = (nint)backend_render_tag;
            IntPtr cstr = Marshal.StringToHGlobalAnsi(backend_render_name);
            unsafe
            {
                byte* pcstr = (byte*)cstr.ToPointer();
                io.NativePtr->BackendRendererName = pcstr;
            }

            int major = _gl.GetInteger(GetPName.MajorVersion);
            int minor = _gl.GetInteger(GetPName.MinorVersion);
            GlVersion = (uint)(major * 100 + minor * 10);

            string vendor = _gl.GetStringS(StringName.Vendor);
            if (vendor.StartsWith("Intel"))
                UseBufferSubData = true;

            if (GlVersion >= 320)
                io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            //io.BackendFlags |= ImGuiBackendFlags.RendererHasViewports;

            GlslVersionString = "#version 130\n";

            HasClipOrigin = (GlVersion >= 450);

            if (_gl.IsExtensionPresent("GL_ARB_clip_control"))
                HasClipOrigin = true;

            //if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
            //    InitPlatformInterface();
        }

        public void Dispose()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            System.Diagnostics.Debug.Assert(io.BackendRendererUserData != 0, "No renderer backend to shutdown, or already shutdown?");
            //ShutdownPlatformInterface();
            DestroyDeviceObjects();
            io.BackendRendererUserData = 0;
            unsafe
            {
                byte* ptr = io.NativePtr->BackendRendererName;
                io.NativePtr->BackendRendererName = (byte*)0;
                Marshal.FreeHGlobal((nint)ptr);
            }
        }

        public void NewFrame()
        {
            if (ShaderHandle == 0)
                CreateDeviceObjects();
        }

        private void SetupRenderState(ImDrawDataPtr draw_data, int fb_width, int fb_height, uint vertex_array_object)
        {
            // Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, polygon fill
            _gl.Enable(GLEnum.Blend);
            _gl.BlendEquation(GLEnum.FuncAdd);
            _gl.BlendFuncSeparate(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha, GLEnum.One, GLEnum.OneMinusSrcAlpha);
            _gl.Disable(GLEnum.CullFace);
            _gl.Disable(GLEnum.DepthTest);
            _gl.Disable(GLEnum.StencilTest);
            _gl.Enable(GLEnum.ScissorTest);
            if (GlVersion >= 310)
                _gl.Disable(GLEnum.PrimitiveRestart);
            _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);

            // Support for GL 4.5 rarely used glClipControl(GL_UPPER_LEFT)
            bool clip_origin_lower_left = true;
            if (HasClipOrigin)
            {
                int current_clip_origin = _gl.GetInteger(GLEnum.ClipOrigin);
                if (current_clip_origin == (int)GLEnum.UpperLeft)
                    clip_origin_lower_left = false;
            }

            // Setup viewport, orthographic projection matrix
            // Our visible imgui space lies from draw_data->DisplayPos (top left) to draw_data->DisplayPos+data_data->DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
            _gl.Viewport(0, 0, (uint)fb_width, (uint)fb_height);
            float L = draw_data.DisplayPos.X;
            float R = draw_data.DisplayPos.X + draw_data.DisplaySize.X;
            float T = draw_data.DisplayPos.Y;
            float B = draw_data.DisplayPos.Y + draw_data.DisplaySize.Y;

            // Swap top and bottom if origin is upper left
            if (!clip_origin_lower_left)
            {
                float tmp = T;
                T = B;
                B = tmp;
            }

            Span<float> orthoProjection = stackalloc float[] {
                2.0f / (R - L),     0.0f,               0.0f,  0.0f,
                0.0f,               2.0f / (T - B),     0.0f,  0.0f,
                0.0f,               0.0f,              -1.0f,  0.0f,
                (R + L) / (L - R),  (T + B) / (B - T),  0.0f,  1.0f,
            };

            _gl.UseProgram(ShaderHandle);
            _gl.Uniform1(AttribLocationTex, 0);
            _gl.UniformMatrix4(AttribLocationProjMtx, 1, false, orthoProjection);
            //_gl.CheckGlError("Projection");

            // We use combined texture/sampler state. Applications using GL 3.3 may set that otherwise.
            if (GlVersion >= 330)
                _gl.BindSampler(0, 0);

            _gl.BindVertexArray(vertex_array_object);

            // Bind vertex/index buffers and setup attributes for ImDrawVert
            _gl.BindBuffer(GLEnum.ArrayBuffer, VboHandle);
            _gl.BindBuffer(GLEnum.ElementArrayBuffer, ElementsHandle);
            _gl.EnableVertexAttribArray(AttribLocationVtxPos);
            _gl.EnableVertexAttribArray(AttribLocationVtxUV);
            _gl.EnableVertexAttribArray(AttribLocationVtxColor);
            _gl.VertexAttribPointer(AttribLocationVtxPos,   2, GLEnum.Float,        false, (uint)Marshal.SizeOf<ImDrawVert>(), Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.pos)));
            _gl.VertexAttribPointer(AttribLocationVtxUV,    2, GLEnum.Float,        false, (uint)Marshal.SizeOf<ImDrawVert>(), Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.uv)));
            _gl.VertexAttribPointer(AttribLocationVtxColor, 4, GLEnum.UnsignedByte, true,  (uint)Marshal.SizeOf<ImDrawVert>(), Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.col)));
        }

        private unsafe void UploadVertexData(ImDrawListPtr cmd_list)
        {
            // Upload vertex/index buffers
            // - On Intel windows drivers we got reports that regular glBufferData() led to accumulating leaks when using multi-viewports, so we started using orphaning + glBufferSubData(). (See https://github.com/ocornut/imgui/issues/4468)
            // - On NVIDIA drivers we got reports that using orphaning + glBufferSubData() led to glitches when using multi-viewports.
            // - OpenGL drivers are in a very sorry state in 2022, for now we are switching code path based on vendors.
            // TODO: The BufferData/SubBufferData calls in this function feel very sus.
            //       I am concerned that sizeof(ImDrawVert) is not guranteed to be equal to Marshal.SizeOf<ImDrawVert>().
            //       Also I am concerned about memory ownage. Obviously once the BufferData is complete then OGL has made a copy,
            //       but until then who owns the ImPtrVector/ImVector's data? Managed or Native?

            var vtx_buffer_size = cmd_list.VtxBuffer.Size * sizeof(ImDrawVert);
            var idx_buffer_size = cmd_list.IdxBuffer.Size * sizeof(ushort);

            if (UseBufferSubData)
            {
                if (VertexBufferSize < vtx_buffer_size)
                {
                    VertexBufferSize = vtx_buffer_size;
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)vtx_buffer_size, (void*)0, BufferUsageARB.StreamDraw);
                }
                if (IndexBufferSize < idx_buffer_size)
                {
                    IndexBufferSize = idx_buffer_size;
                    _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)idx_buffer_size, (void*)0, BufferUsageARB.StreamDraw);
                }

                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)vtx_buffer_size, (void*)cmd_list.VtxBuffer.Data);
                _gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, 0, (nuint)idx_buffer_size, (void*)cmd_list.IdxBuffer.Data);
            }
            else
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)vtx_buffer_size, (void*)cmd_list.VtxBuffer.Data, BufferUsageARB.StreamDraw);
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)idx_buffer_size, (void*)cmd_list.IdxBuffer.Data, BufferUsageARB.StreamDraw);
            }
        }

        public void RenderDrawdata(ImDrawDataPtr draw_data)
        {
            int fb_width = (int)(draw_data.DisplaySize.X * draw_data.FramebufferScale.X);
            int fb_height = (int)(draw_data.DisplaySize.Y * draw_data.FramebufferScale.Y);
            if (fb_width <= 0 || fb_height <= 0)
                return;

            // Backup GL State
            _gl.GetInteger(GLEnum.ActiveTexture, out int lastActiveTexture);
            _gl.ActiveTexture(GLEnum.Texture0);
            _gl.GetInteger(GLEnum.CurrentProgram, out int lastProgram);
            _gl.GetInteger(GLEnum.TextureBinding2D, out int lastTexture);
            int lastSampler = 0;
            if (GlVersion >= 330)
                _gl.GetInteger(GLEnum.SamplerBinding, out lastSampler);
            _gl.GetInteger(GLEnum.ArrayBufferBinding, out int lastArrayBuffer);
            _gl.GetInteger(GLEnum.VertexArrayBinding, out int lastVertexArrayObject);
            Span<int> lastPolygonMode = stackalloc int[2];
            _gl.GetInteger(GLEnum.PolygonMode, lastPolygonMode);
            Span<int> lastScissorBox = stackalloc int[4];
            _gl.GetInteger(GLEnum.ScissorBox, lastScissorBox);
            _gl.GetInteger(GLEnum.BlendSrcRgb, out int lastBlendSrcRgb);
            _gl.GetInteger(GLEnum.BlendDstRgb, out int lastBlendDstRgb);
            _gl.GetInteger(GLEnum.BlendSrcAlpha, out int lastBlendSrcAlpha);
            _gl.GetInteger(GLEnum.BlendDstAlpha, out int lastBlendDstAlpha);
            _gl.GetInteger(GLEnum.BlendEquationRgb, out int lastBlendEquationRgb);
            _gl.GetInteger(GLEnum.BlendEquationAlpha, out int lastBlendEquationAlpha);
            bool lastEnableBlend = _gl.IsEnabled(GLEnum.Blend);
            bool lastEnableCullFace = _gl.IsEnabled(GLEnum.CullFace);
            bool lastEnableDepthTest = _gl.IsEnabled(GLEnum.DepthTest);
            bool lastEnableStencilTest = _gl.IsEnabled(GLEnum.StencilTest);
            bool lastEnableScissorTest = _gl.IsEnabled(GLEnum.ScissorTest);
            bool lastEnablePrimitiveRestart = (GlVersion >= 310) ? _gl.IsEnabled(GLEnum.PrimitiveRestart) : false;

            // Setup desired GL state
            // Recreate the VAO every time (this is to easily allow multiple GL contexts to be rendered to. VAO are not shared among GL contexts)
            // The renderer would actually work without any VAO bound, but then our VertexAttrib calls would overwrite the default one currently bound.
            var vertex_array_object = _gl.GenVertexArray();
            SetupRenderState(draw_data, fb_width, fb_height, vertex_array_object);

            // Will project scissor/clipping rectangles into framebuffer space
            Vector2 clipOff = draw_data.DisplayPos;         // (0,0) unless using multi-viewports
            Vector2 clipScale = draw_data.FramebufferScale; // (1,1) unless using retina display which are often (2,2)

            // Render command lists
            for (int n = 0; n < draw_data.CmdListsCount; n++)
            {
                ImDrawListPtr cmd_list = draw_data.CmdLists[n];

                UploadVertexData(cmd_list);

                for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
                {
                    ImDrawCmdPtr cmdPtr = cmd_list.CmdBuffer[cmd_i];

                    if (cmdPtr.UserCallback != IntPtr.Zero)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        Vector4 clipRect;
                        clipRect.X = (cmdPtr.ClipRect.X - clipOff.X) * clipScale.X; // clip_min.x
                        clipRect.Y = (cmdPtr.ClipRect.Y - clipOff.Y) * clipScale.Y; // clip_min.y
                        clipRect.Z = (cmdPtr.ClipRect.Z - clipOff.X) * clipScale.X; // clip_max.x
                        clipRect.W = (cmdPtr.ClipRect.W - clipOff.Y) * clipScale.Y; // clip_max.y

                        if (clipRect.Z <= clipRect.X || clipRect.W < clipRect.Y)
                            continue;

                        // Apply scissor/clipping rectangle (Y is inverted in OpenGL)
                        _gl.Scissor((int)clipRect.X, (int)(fb_height - clipRect.W), (uint)(clipRect.Z - clipRect.X), (uint)(clipRect.W - clipRect.Y));

                        // Bind texture, Draw
                        _gl.BindTexture(GLEnum.Texture2D, (uint)cmdPtr.GetTexID());
                        unsafe
                        {
                            if (GlVersion > 320)
                                _gl.DrawElementsBaseVertex(GLEnum.Triangles, cmdPtr.ElemCount, GLEnum.UnsignedShort, (void*)(cmdPtr.IdxOffset * sizeof(ushort)), (int)cmdPtr.VtxOffset);
                            else
                                _gl.DrawElements(PrimitiveType.Triangles, cmdPtr.ElemCount, DrawElementsType.UnsignedShort, (void*)(cmdPtr.IdxOffset * sizeof(ushort)));
                        }
                    }
                }
            }

            // Destroy the temporary VAO
            _gl.DeleteVertexArray(vertex_array_object);

            // Restore modified GL state
            _gl.UseProgram((uint)lastProgram);
            _gl.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
            if (GlVersion >= 330)
                _gl.BindSampler(0, (uint)lastSampler);
            _gl.ActiveTexture((GLEnum)lastActiveTexture);
            _gl.BindVertexArray((uint)lastVertexArrayObject);
            _gl.BindBuffer(GLEnum.ArrayBuffer, (uint)lastArrayBuffer);
            _gl.BlendEquationSeparate((GLEnum)lastBlendEquationRgb, (GLEnum)lastBlendEquationAlpha);
            _gl.BlendFuncSeparate((GLEnum)lastBlendSrcRgb, (GLEnum)lastBlendDstRgb, (GLEnum)lastBlendSrcAlpha, (GLEnum)lastBlendDstAlpha);
            if (lastEnableBlend)
            {
                _gl.Enable(GLEnum.Blend);
            }
            else
            {
                _gl.Disable(GLEnum.Blend);
            }
            if (lastEnableCullFace)
            {
                _gl.Enable(GLEnum.CullFace);
            }
            else
            {
                _gl.Disable(GLEnum.CullFace);
            }
            if (lastEnableDepthTest)
            {
                _gl.Enable(GLEnum.DepthTest);
            }
            else
            {
                _gl.Disable(GLEnum.DepthTest);
            }
            if (lastEnableStencilTest)
            {
                _gl.Enable(GLEnum.StencilTest);
            }
            else
            {
                _gl.Disable(GLEnum.StencilTest);
            }
            if (lastEnableScissorTest)
            {
                _gl.Enable(GLEnum.ScissorTest);
            }
            else
            {
                _gl.Disable(GLEnum.ScissorTest);
            }
            if (GlVersion >= 310)
            {
                if (lastEnablePrimitiveRestart)
                {
                    _gl.Enable(GLEnum.PrimitiveRestart);
                }
                else
                {
                    _gl.Disable(GLEnum.PrimitiveRestart);
                }
            }
            _gl.PolygonMode(GLEnum.FrontAndBack, (GLEnum)lastPolygonMode[0]);
            _gl.Scissor(lastScissorBox[0], lastScissorBox[1], (uint)lastScissorBox[2], (uint)lastScissorBox[3]);
        }

        public unsafe bool CreateFontsTexture()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            
            io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height);

            _gl.GetInteger(GetPName.TextureBinding2D, out int last_texture);
            FontTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, FontTexture);
            // TODO: The warnings here will get implicitly fixed when we wrap the texture in an object.
            _gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.PixelStore(GLEnum.UnpackRowLength, 0);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            io.Fonts.SetTexID((nint)FontTexture);

            _gl.BindTexture(TextureTarget.Texture2D, (uint)last_texture);

            return true;
        }

        public void DestroyFontsTexture()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            if (FontTexture != 0)
            {
                _gl.DeleteTexture(FontTexture);
                io.Fonts.SetTexID(0);
                FontTexture = 0;
            }
        }

        private bool CheckShader(uint handle, string desc)
        {
            int status = _gl.GetShader(handle, ShaderParameterName.CompileStatus);
            int log_length = _gl.GetShader(handle, ShaderParameterName.InfoLogLength);
            if (status == 0)
                Console.Error.WriteLine("ERROR: ImGui_Impl_Silk_OpenGL3.CheckShader: Failed to compile {0}! Using GLSL: {1}", desc, GlslVersionString);
            if (log_length > 1)
            {
                string infoLog = _gl.GetShaderInfoLog(handle);
                Console.Error.WriteLine(infoLog);
            }
            return status == 1;
        }

        private bool CheckProgram(uint handle, string desc)
        {
            int status = _gl.GetProgram(handle, ProgramPropertyARB.LinkStatus);
            int log_length = _gl.GetProgram(handle, ProgramPropertyARB.InfoLogLength);
            if (status == 0)
                Console.Error.WriteLine("ERROR: ImGui_Impl_Silk_OpenGL3.CheckProgram: Failed to link {0}! Using GLSL: {1}", desc, GlslVersionString);
            if (log_length > 1)
            {
                string infoLog = _gl.GetProgramInfoLog(handle);
                Console.Error.WriteLine(infoLog);
            }
            return status == 1;
        }

        public bool CreateDeviceObjects()
        {
            int last_texture = _gl.GetInteger(GetPName.TextureBinding2D);
            int last_array_buffer = _gl.GetInteger(GetPName.ArrayBufferBinding);
            int last_vertex_array = _gl.GetInteger(GetPName.VertexArrayBinding);

            // TODO: using hardcoded glsl version
            int glsl_version = 130;

            string vertex_shader_glsl_130 =
@"#version 130
uniform mat4 ProjMtx;
in vec2 Position;
in vec2 UV;
in vec4 Color;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main()
{
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position.xy,0,1);
}";
            string fragment_shader_glsl_130 =
@"#version 130
uniform sampler2D Texture;
in vec2 Frag_UV;
in vec4 Frag_Color;
out vec4 Out_Color;
void main()
{
    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
}";

            uint vert_handle = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vert_handle, vertex_shader_glsl_130);
            _gl.CompileShader(vert_handle);
            CheckShader(vert_handle, "vertex shader");

            uint frag_handle = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(frag_handle, fragment_shader_glsl_130);
            _gl.CompileShader(frag_handle);
            CheckShader(frag_handle, "fragment shader");

            ShaderHandle = _gl.CreateProgram();
            _gl.AttachShader(ShaderHandle, vert_handle);
            _gl.AttachShader(ShaderHandle, frag_handle);
            _gl.LinkProgram(ShaderHandle);
            CheckProgram(ShaderHandle, "shader program");

            _gl.DetachShader(ShaderHandle, vert_handle);
            _gl.DetachShader(ShaderHandle, frag_handle);
            _gl.DeleteShader(vert_handle);
            _gl.DeleteShader(frag_handle);

            AttribLocationTex = _gl.GetUniformLocation(ShaderHandle, "Texture");
            AttribLocationProjMtx = _gl.GetUniformLocation(ShaderHandle, "ProjMtx");
            AttribLocationVtxPos = (uint)_gl.GetAttribLocation(ShaderHandle, "Position");
            AttribLocationVtxUV = (uint)_gl.GetAttribLocation(ShaderHandle, "UV");
            AttribLocationVtxColor = (uint)_gl.GetAttribLocation(ShaderHandle, "Color");

            VboHandle = _gl.GenBuffer();
            ElementsHandle = _gl.GenBuffer();

            CreateFontsTexture();

            _gl.BindTexture(TextureTarget.Texture2D, (uint)last_texture);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)last_array_buffer);
            _gl.BindVertexArray((uint)last_vertex_array);

            return true;
        }

        public void DestroyDeviceObjects()
        {
            if (VboHandle != 0)
            {
                _gl.DeleteBuffer(VboHandle);
                VboHandle = 0;
            }
            if (ElementsHandle != 0)
            {
                _gl.DeleteBuffer(ElementsHandle);
                ElementsHandle = 0;
            }
            if (ShaderHandle != 0)
            {
                _gl.DeleteProgram(ShaderHandle);
                ShaderHandle = 0;
            }
            DestroyFontsTexture();
        }

        //private void RenderWindow(ImGuiViewportPtr viewport)
        //{
        //    if (!viewport.Flags.HasFlag(ImGuiViewportFlags.NoRendererClear))
        //    {
        //        _gl.ClearColor(0, 0, 0, 1);
        //        _gl.Clear(ClearBufferMask.ColorBufferBit);
        //    }
        //    RenderDrawdata(viewport.DrawData);
        //}

        //private void InitPlatformInterface()
        //{
        //    var platform_io = ImGui.GetPlatformIO();
        //    platform_io.Renderer_RenderWindow
        //}
    }
}
