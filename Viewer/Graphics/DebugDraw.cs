using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.Graphics
{
    internal struct V_P3C4
    {
        public float X, Y, Z;
        public float R, G, B, A;

        public V_P3C4(Vector3 pos, Vector4 col)
        {
            X = pos.X;
            Y = pos.Y;
            Z = pos.Z;
            R = col.X;
            G = col.Y;
            B = col.Z;
            A = col.W;
        }
    }

    internal class DebugDraw
    {
        private readonly GL _gl;
        private readonly List<V_P3C4> _verts;
        private readonly List<ushort> _indices;
        private readonly List<DrawCmd> _drawCmds;
        private readonly OGL.Buffer<V_P3C4> _arrayBuffer;
        private readonly OGL.Buffer<ushort> _elementBuffer;
        private readonly OGL.VertexArray _VAO;

        public DebugDraw(GL gl)
        {
            _gl = gl;
            _verts = new List<V_P3C4>();
            _indices = new List<ushort>();
            _drawCmds = new List<DrawCmd>();
            _arrayBuffer = new OGL.Buffer<V_P3C4>(gl, BufferTargetARB.ArrayBuffer, CollectionsMarshal.AsSpan(_verts), VertexBufferObjectUsage.StreamDraw);
            _elementBuffer = new OGL.Buffer<ushort>(gl, BufferTargetARB.ElementArrayBuffer, CollectionsMarshal.AsSpan(_indices), VertexBufferObjectUsage.StreamDraw);
            _VAO = new OGL.VertexArray(gl);
            _VAO.BindVertexBuffer(0, _arrayBuffer, 0, 7 * sizeof(float));
            _VAO.SetAttribBinding(0, 0);
            _VAO.SetAttribFormat(0, 3, VertexAttribType.Float, false, 0 * sizeof(float));
            _VAO.SetAttribBinding(3, 0);
            _VAO.SetAttribFormat(3, 4, VertexAttribType.Float, false, 3 * sizeof(float));
            _VAO.BindElementBuffer(_elementBuffer);
        }

        public void DrawCube(Vector3 position, Vector3 size, Vector4 color)
        {
            Vector3 halfSize = size / 2.0f;
            int baseIdx = _verts.Count;
            DrawCmd cmd = new()
            {
                start = _indices.Count,
                count = 24,
                prim = PrimitiveType.Lines,
                twoSided = false
            };
            _verts.Add(new V_P3C4(position + new Vector3(halfSize.X, halfSize.Y, halfSize.Z), color));      // 0
            _verts.Add(new V_P3C4(position + new Vector3(halfSize.X, halfSize.Y, -halfSize.Z), color));     // 1
            _verts.Add(new V_P3C4(position + new Vector3(halfSize.X, -halfSize.Y, halfSize.Z), color));     // 2
            _verts.Add(new V_P3C4(position + new Vector3(halfSize.X, -halfSize.Y, -halfSize.Z), color));    // 3
            _verts.Add(new V_P3C4(position + new Vector3(-halfSize.X, halfSize.Y, halfSize.Z), color));     // 4
            _verts.Add(new V_P3C4(position + new Vector3(-halfSize.X, halfSize.Y, -halfSize.Z), color));    // 5
            _verts.Add(new V_P3C4(position + new Vector3(-halfSize.X, -halfSize.Y, halfSize.Z), color));    // 6
            _verts.Add(new V_P3C4(position + new Vector3(-halfSize.X, -halfSize.Y, -halfSize.Z), color));   // 7

            // Top Square
            _indices.Add((ushort)(baseIdx + 0)); _indices.Add((ushort)(baseIdx + 1));
            _indices.Add((ushort)(baseIdx + 1)); _indices.Add((ushort)(baseIdx + 5));
            _indices.Add((ushort)(baseIdx + 5)); _indices.Add((ushort)(baseIdx + 4));
            _indices.Add((ushort)(baseIdx + 4)); _indices.Add((ushort)(baseIdx + 0));
            
            // Bottom Square
            _indices.Add((ushort)(baseIdx + 2)); _indices.Add((ushort)(baseIdx + 3));
            _indices.Add((ushort)(baseIdx + 3)); _indices.Add((ushort)(baseIdx + 7));
            _indices.Add((ushort)(baseIdx + 7)); _indices.Add((ushort)(baseIdx + 6));
            _indices.Add((ushort)(baseIdx + 6)); _indices.Add((ushort)(baseIdx + 2));

            // Verticals
            _indices.Add((ushort)(baseIdx + 0)); _indices.Add((ushort)(baseIdx + 2));
            _indices.Add((ushort)(baseIdx + 1)); _indices.Add((ushort)(baseIdx + 3));
            _indices.Add((ushort)(baseIdx + 4)); _indices.Add((ushort)(baseIdx + 6));
            _indices.Add((ushort)(baseIdx + 5)); _indices.Add((ushort)(baseIdx + 7));

            _drawCmds.Add(cmd);
        }

        public void Render()
        {
            _arrayBuffer.BufferData(CollectionsMarshal.AsSpan(_verts), VertexBufferObjectUsage.StreamDraw);
            _elementBuffer.BufferData(CollectionsMarshal.AsSpan(_indices), VertexBufferObjectUsage.StreamDraw);
            _VAO.Use();
            foreach (var cmd in _drawCmds)
            {
                unsafe
                {
                    _gl.DrawElements(cmd.prim, (uint)cmd.count, DrawElementsType.UnsignedShort, (void*)(cmd.start * sizeof(ushort)));
                }
                //_gl.DrawArrays(cmd.prim, cmd.start, (uint)cmd.count);
            }

            _verts.Clear();
            _indices.Clear();
            _drawCmds.Clear();
        }
    }
}
