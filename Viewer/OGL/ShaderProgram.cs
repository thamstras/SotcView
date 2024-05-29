using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.OGL
{
    // TODO: This is lifted stright from SILK's ImGui module. I'm not thrilled with it but it'll do for now.
    internal class ShaderProgram : IDisposable
    {
        private readonly GL _gl;
        private (ShaderType Type, string Path)[] _files;
        internal readonly uint _handle;
        private bool _initialized = false;
        private readonly Dictionary<string, int> _uniformToLocation = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _attribLocation = new Dictionary<string, int>();

        public ShaderProgram(GL gl, string vertexShaderSource, string fragmentShaderSource)
        {
            _gl = gl;
            _files = new[]{
                (ShaderType.VertexShader, vertexShaderSource),
                (ShaderType.FragmentShader, fragmentShaderSource),
            };
            _handle = CreateProgram(_files);
        }

        public void UseShader()
        {
            _gl.UseProgram(_handle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetUniformLocation(string uniform)
        {
            if (_uniformToLocation.TryGetValue(uniform, out int location) == false)
            {
                location = _gl.GetUniformLocation(_handle, uniform);
                _uniformToLocation.Add(uniform, location);

                if (location == -1)
                {
                    Debug.Print($"The uniform '{uniform}' does not exist in the shader!");
                }
            }

            return location;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAttribLocation(string attrib)
        {
            if (_attribLocation.TryGetValue(attrib, out int location) == false)
            {
                location = _gl.GetAttribLocation(_handle, attrib);
                _attribLocation.Add(attrib, location);

                if (location == -1)
                {
                    Debug.Print($"The attrib '{attrib}' does not exist in the shader!");
                }
            }

            return location;
        }

        private uint CreateProgram(params (ShaderType Type, string source)[] shaderPaths)
        {
            var program = _gl.CreateProgram();

            Span<uint> shaders = stackalloc uint[shaderPaths.Length];
            for (int i = 0; i < shaderPaths.Length; i++)
            {
                shaders[i] = CompileShader(shaderPaths[i].Type, shaderPaths[i].source);
            }

            foreach (var shader in shaders)
                _gl.AttachShader(program, shader);

            _gl.LinkProgram(program);

            _gl.GetProgram(program, GLEnum.LinkStatus, out var success);
            if (success == 0)
            {
                string info = _gl.GetProgramInfoLog(program);
                Debug.WriteLine($"GL.LinkProgram had info log:\n{info}");
            }

            foreach (var shader in shaders)
            {
                _gl.DetachShader(program, shader);
                _gl.DeleteShader(shader);
            }

            _initialized = true;

            return program;
        }

        private uint CompileShader(ShaderType type, string source)
        {
            var shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);

            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var success);
            if (success == 0)
            {
                string info = _gl.GetShaderInfoLog(shader);
                Debug.WriteLine($"GL.CompileShader for shader [{type}] had info log:\n{info}");
            }

            return shader;
        }

        public void Dispose()
        {
            if (_initialized)
            {
                _gl.DeleteProgram(_handle);
                _initialized = false;
            }
        }
    }
}
