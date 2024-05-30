using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer.Graphics
{
    internal class GraphicsState
    {
        public Matrix4X4<float> Model { get; set; }
        public Matrix4X4<float> View { get; set; }
        public Matrix4X4<float> Projection { get; set; }

        public bool EnableTextures { get; set; } = true;
        public bool EnableBlend { get; set; } = true;
        
        public bool SoloTexture { get; set; } = false;
        public int SoloTextureID { get; set; } = -1;

        public bool SoloSurf { get; set; } = false;
        public int SoloSurfID { get; set; } = -1;
    }
}
