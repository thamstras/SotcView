using NicoLib;
using Silk.NET.Maths;
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
    public struct DrawCmd
    {
        public int start;
        public int count;
        public PrimitiveType prim;
        public bool twoSided;
    }

    public struct Vertex
    {
        public float x, y, z;
        public float nx, ny, nz, nw;
        public float u, v;
        public float r, g, b, a;
    }

    public class SubMesh
    {
        public int MaterialIndex { get; set; }
        public List<Vertex> Vertices { get; set; } = new List<Vertex>();
        public List<DrawCmd> DrawCmds { get; set; } = new List<DrawCmd>();
    }

    public class Material
    {
        public string Name { get; set; }
        //public Vector4 TintColor { get; set; }
        public int TextureIndex { get; set; }
    }

    public class StaticMesh : IDisposable
    {
        public List<string> Textures { get; set; } = new List<string>();
        public List<Material> Materials { get; set; } = new List<Material>();
        public List<SubMesh> SubMeshes { get; set; } = new List<SubMesh>();

        private readonly GL _gl;

        private bool built = false;
        private OGL.Buffer<float>? VBO = null;
        private OGL.VertexArray? VAO = null;
        private List<OGL.Texture?> oglTextures = new List<OGL.Texture?>();
        private static OGL.Texture? dummyTexture = null;
        public Box3D<float> Bounds { get; private set; }

        public StaticMesh(GL gl)
        {
            _gl = gl;
        }

        public void Build()
        {
            if (built) Dispose();

            if (SubMeshes.Count == 0)
                return;

            Vector3D<float> minPos, maxPos;

            var firstVert = SubMeshes.First(sm => sm.Vertices.Count != 0).Vertices[0];
            minPos = new Vector3D<float>(firstVert.x, firstVert.y, firstVert.z);
            maxPos = new Vector3D<float>(firstVert.x, firstVert.y, firstVert.z);

            var vertCount = SubMeshes.Sum(sm => sm.Vertices.Count);
            List<float> vertexData = new List<float>(13 * vertCount);
            var allVerts = SubMeshes.SelectMany(sm => sm.Vertices);
            foreach (var vert in allVerts)
            {
                vertexData.Add(vert.x);
                vertexData.Add(vert.y);
                vertexData.Add(vert.z);
                vertexData.Add(vert.nx);
                vertexData.Add(vert.ny);
                vertexData.Add(vert.nz);
                vertexData.Add(vert.nw);
                vertexData.Add(vert.u);
                vertexData.Add(vert.v);
                vertexData.Add(vert.r);
                vertexData.Add(vert.g);
                vertexData.Add(vert.b);
                vertexData.Add(vert.a);

                var vpos = new Vector3D<float>(vert.x, vert.y, vert.z);
                minPos = Vector3D.Min(minPos, vpos);
                maxPos = Vector3D.Max(maxPos, vpos);
            }

            Bounds = new Box3D<float>(minPos, maxPos);

            VAO = new OGL.VertexArray(_gl);
            VBO = new OGL.Buffer<float>(_gl, BufferTargetARB.ArrayBuffer, CollectionsMarshal.AsSpan(vertexData), VertexBufferObjectUsage.StaticDraw);

            VAO.BindVertexBuffer(0, VBO, 0, 13 * sizeof(float));

            VAO.SetAttribBinding(0, 0); // Position
            VAO.SetAttribFormat(0, 3, VertexAttribType.Float, false, 0 * sizeof(float));

            VAO.SetAttribBinding(1, 0); // Normal
            VAO.SetAttribFormat(1, 4, VertexAttribType.Float, false, 3 * sizeof(float));

            VAO.SetAttribBinding(2, 0); // UV
            VAO.SetAttribFormat(2, 2, VertexAttribType.Float, false, 7 * sizeof(float));

            VAO.SetAttribBinding(3, 0); // Color
            VAO.SetAttribFormat(3, 4, VertexAttribType.Float, false, 9 * sizeof(float));

            built = true;
        }

        internal void LinkTextures(Dictionary<string, (Nto, Image)> texDict)
        {
            oglTextures = new(Textures.Count);
            for (int i = 0; i < Textures.Count; i++)
            {
                if (texDict.TryGetValue(Textures[i], out var pair))
                {
                    oglTextures.Add(pair.Item2.Texture);
                }
                else
                {
                    oglTextures.Add(null);
                }
            }

            if (dummyTexture == null)
                CreateDummyTexture();
        }

        private void CreateDummyTexture()
        {
            byte[] bytes = [0xff, 0xff, 0xff, 0x80];
            dummyTexture = new OGL.Texture(_gl, TextureTarget.Texture2D);
            dummyTexture.AllocStorage(1, 1, SizedInternalFormat.Rgba8);
            dummyTexture.Upload(1, 1, PixelFormat.Rgba, bytes);
        }

        // NOTE: assumes shader + uniforms are set beforehand
        public void Draw(OGL.ShaderProgram shader)
        {
            if (!built) return;

            VAO.Use();
            int sectionOffset = 0;
            int runningOffset = 0;
            foreach (var subMesh in SubMeshes)
            {
                Material mat = Materials[subMesh.MaterialIndex];
                if (mat.TextureIndex < oglTextures.Count
                    && oglTextures[mat.TextureIndex] != null)
                    oglTextures[mat.TextureIndex].Bind(0);
                else
                    dummyTexture?.Bind(0);

                //_gl.Uniform4(shader.GetUniformLocation("tint"), mat.TintColor);

                foreach (var drawCmd in subMesh.DrawCmds)
                {
                    //if (!drawCmd.twoSided)
                    //    continue;
                    //    _gl.DepthMask(false);
                    //    _gl.Disable(EnableCap.CullFace);
                    // TODO: This should really be a glMultiDrawArrays call...
                    // TODO: THAT should really be a glMultiDrawArraysIndirect call...
                    _gl.DrawArrays(drawCmd.prim, sectionOffset + drawCmd.start, (uint)drawCmd.count);
                    runningOffset += drawCmd.count;
                    //if (drawCmd.twoSided)
                    //    _gl.DepthMask(true);
                    //    _gl.Enable(EnableCap.CullFace);
                }
                sectionOffset = runningOffset;
            }
        }

        public void Dispose()
        {
            if (built)
            {
                VBO.Dispose();
                VAO.Dispose();
                built = false;
            }
        }
    }

    internal static class StaticMeshExtensions
    {
        public static StaticMesh FromNMO(GL gl, Nmo nmo)
        {
            StaticMesh mesh = new StaticMesh(gl);
            foreach (var tex in nmo.Textures)
            {
                mesh.Textures.Add(tex.Name);
            }

            foreach (var surf in nmo.Surfaces)
            {
                mesh.Materials.Add(new Material()
                {
                    Name = surf.Name,
                    TextureIndex = (int)surf.Hdr1[0].Three,
                    //TintColor = new Vector4(surf.MaybeTint[0] / 255.0f, surf.MaybeTint[1] / 255.0f, surf.MaybeTint[2] / 255.0f, surf.MaybeTint[3] / 255.0f)
                });
            }

            for (int i = 0; i < nmo.Meshes.Count; i++)
            {
                Nmo.ChunkVIF chunk = nmo.Meshes[i];
                //Nmo.ChunkSURF surf = nmo.Surfaces[(int)chunk.surf];
                List<Nmo.TriStrip> geom = nmo.ReadVifPacket(chunk);

                // TODO: Merge SubMeshes with the same texture
                // TODO: glDrawMultiArrays
                SubMesh subMesh = new SubMesh();
                subMesh.MaterialIndex = (int)chunk.surf;
                foreach (var strip in geom)
                {
                    var start = subMesh.Vertices.Count;
                    var count = strip.VertCount;
                    foreach (var vert in strip.Verts)
                        subMesh.Vertices.Add(Convert(vert));
                    subMesh.DrawCmds.Add(new DrawCmd()
                    {
                        start = start,
                        count = count,
                        // todo: this is still wrong and i don't know what it should be
                        prim = strip.PrimativeType == Nmo.Primative.PRIMATIVE_TRIANGLE_STRIP ? PrimitiveType.TriangleStrip : PrimitiveType.Triangles,
                        twoSided = strip.PrimativeType == Nmo.Primative.PRIMATIVE_TRIANGLES
                    });
                }
                mesh.SubMeshes.Add(subMesh);
            }

            mesh.Build();
            return mesh;
        }

        static Vertex Convert(Nmo.Vertex v)
        {
            return new Vertex
            {
                x = v.Position.X,
                y = v.Position.Y,
                z = v.Position.Z,
                nx = v.Normal.X,
                ny = v.Normal.Y,
                nz = v.Normal.Z,
                nw = v.Normal.W,
                u = v.UV.X,
                v = v.UV.Y,
                r = v.Color.X,
                g = v.Color.Y,
                b = v.Color.Z,
                a = v.Color.W
            };
        }
    }
}
