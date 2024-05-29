using Silk.NET.Input;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer
{
    internal class Arcball
    {
        enum MouseMode { None, Move, Rotate, Zoom };

        public Vector3D<float> Center { get; set; }
        public float Distance { get; set; }
        public float Pitch { get; set; }
        public float Yaw { get; set; }

        MouseMode _currMode = MouseMode.None;
        Vector2D<float> _lastMousePos;

        public Arcball(Vector3D<float> center, float distance, float pitch, float yaw)
        {
            Center = center;
            Distance = distance;
            Pitch = pitch;
            Yaw = yaw;
        }

        public void OnMouseDown(IMouse mouse, MouseButton button)
        {

        }

        public void OnMouseUp(IMouse mouse, MouseButton button)
        {
            if (button == MouseButton.Left || button == MouseButton.Right)
            {
                _currMode = MouseMode.None;
            }
        }

        public void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
        {

        }
    }
}
