using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.Graphics
{
    internal class ModelSection
    {
        public int TextureIndex { get; set; }
        // public PrimativeType Primative { get; set; }
        public List<Vector3> Positions { get; set; }
        public List<Vector3> Normals { get; set; }
        public List<Vector3> UVs { get; set; }
        public List<Vector4> Colors { get; set; }
    }

    internal class Model
    {
        //public List<Image> Textures { get; set; }
        public List<ModelSection> Sections { get; set; }
    }
}
