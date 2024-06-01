using NicoLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Assimp;

namespace Viewer
{
    internal class Exporter
    {

        public struct FormatDescription
        {
            public string ID { get; set; }
            public string FileExtension { get; set; }
            public string Description { get; set; }
        }

        private readonly AssimpContext _context;
        private List<FormatDescription>? _formats;

        public Exporter()
        {
            _context = new AssimpContext();
        }

        public IReadOnlyList<FormatDescription> GetExportFormats()
        {
            if (_formats == null)
            {
                _formats = new List<FormatDescription>();
                var formats = _context.GetSupportedExportFormats();
                foreach ( var format in formats )
                {
                    _formats.Add(new FormatDescription()
                    {
                        ID = format.FormatId,
                        FileExtension = format.FileExtension,
                        Description = format.Description
                    });
                }
            }

            return _formats;
        }

        public void ExportTexture(Nto nto, string outFilePath)
        {
            Bitmap bitmap = new Bitmap(nto.Width, nto.Height, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, nto.Width, nto.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var dstLength = Math.Min(nto.PixelData.Length, data.Stride * data.Height);
            Marshal.Copy(nto.PixelData, 0, data.Scan0, dstLength);
            bitmap.Save(outFilePath, ImageFormat.Png);
        }

        public void ExportModel(Nmo nmo, string outFilePath, string formatIdentifier, string textureFolder)
        {
            Scene scene = new Scene();
            
            foreach (var mat in nmo.Surfaces)
            {
                var aiMat = new Material();
                aiMat.Name = mat.Name;
                aiMat.ShadingMode = ShadingMode.Flat;
                var diffTex = new TextureSlot();
                diffTex.TextureType = TextureType.Diffuse;
                var tex = nmo.Textures[(int)mat.Hdr1[0].Three];
                diffTex.FilePath = Path.Combine(textureFolder, $"{tex.Name}.png");
                aiMat.TextureDiffuse = diffTex;
                scene.Materials.Add(aiMat);
            }
            
            foreach (var subMesh in nmo.Meshes)
            {
                var aiMesh = new Mesh();
                aiMesh.MaterialIndex = (int)subMesh.surf;
                List<Nmo.TriStrip> geom = nmo.ReadVifPacket(subMesh);
                foreach (var strip in geom)
                {
                    int stripBase = aiMesh.VertexCount;
                    foreach (var vert in strip.Verts)
                    {
                        aiMesh.Vertices.Add(new Vector3D(vert.Position.X, vert.Position.Y, vert.Position.Z));
                        aiMesh.TextureCoordinateChannels[0].Add(new Vector3D(vert.UV.X, vert.UV.Y, 0.0f));
                        aiMesh.VertexColorChannels[0].Add(new Color4D(vert.Color.X, vert.Color.Y, vert.Color.Z, vert.Color.W));
                    }
                    for (int i = 0; i < strip.Verts.Count - 2; i++)
                    {
                        aiMesh.Faces.Add(new Face([stripBase + i, stripBase + i + 1, stripBase + i + 2]));
                    }
                }
                scene.Meshes.Add(aiMesh);
            }

            var node = new Node();
            node.Transform = Matrix4x4.Identity;
            for (int i = 0; i < scene.MeshCount; i++)
                node.MeshIndices.Add(i);
            scene.RootNode = node;

            _context.ExportFile(scene, outFilePath, formatIdentifier);
        }
    }
}
