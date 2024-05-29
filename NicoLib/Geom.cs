using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoLib
{

    public class Geom
    {
        public abstract class Vert
        {

        }

        public enum PrimitiveType
        {
            Points,
            Lines,
            LineStrip,
            Triangles,
            TriangleStrip
        }

        public enum TriangleWindingOrder
        {
            CounterClockwise,
            Clockwise
        }

        public class DrawList<T> where T : Vert
        {
            List<T> vertices;
            PrimitiveType primitive;
            TriangleWindingOrder winding;
            List<ushort> elementCounts;
        }
    }
}
