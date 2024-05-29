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
    }
}
