using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Viewer
{
    internal class View
    {
        // TODO: This is copied from the old ML code. It'll probably want to be split up
        // TODO: Looks like we probably want to pull most of this up into our root program class

        public enum EMType
        {
            None, Zoom, Rotate, Move
        }

        public Vector2D<float> Size { get; set; }
        public Vector3D<float> Rotation { get; set; }
        public float Zoom { get; set; }
        public Vector3D<float> Center { get; set; }
        public float RadialScale { get; set; }
        public List<IDraw> Objs { get; set; } = new List<IDraw>();
        public bool Persp { get; set; }
        public float LastTime { get; set; }
        public bool Animated { get; set; }
        //public Lazy<out_channel> DumpChan { get; set; }
        //public bool DoDump { get; set; }
        public float aincr { get; set; }
        public bool roteye { get; set; }
        public bool sphere { get; set; }
        public bool Help { get; set; }
        public Vector2D<float> XY { get; set; }
        public EMType MType { get; set; }
        public Vector3D<float> Translation { get; set; }
        public float Alpha { get; set; }
        public float Ambient { get; set; }
        public float Diffuse { get; set; }

        public List<IKeyReceiver> Controls { get; set; } = new List<IKeyReceiver>();

        public Graphics.GraphicsState GState { get; set; } = new Graphics.GraphicsState();

        public View()
        {
            Size = new Vector2D<float>(0.0f, 0.0f);
            Rotation = new Vector3D<float>(4.0f, 16.0f, 0.0f);
            Center = new Vector3D<float>(0.0f, 0.0f, 0.0f);
            RadialScale = 0.0f;
            Zoom = 1.2f;
            Persp = true;
            LastTime = 0.0f;
            Animated = false;
            aincr = 3.0f;
            roteye = true;
            sphere = false;
            Help = false;
            XY = new Vector2D<float>(0, 0);
            MType = EMType.None;
            Translation = new Vector3D<float>(0, 0, 0);
            Alpha = 0.04f;
            Ambient = 1.3f;
            Diffuse = 0.5f;
        }

        public (Vector3D<float>, float) CenterAndRadialScale(float minx, float maxx, float miny, float maxy, float minz, float maxz)
        {
            float xc = (maxx + minx) / 2.0f;
            float yc = (maxy + miny) / 2.0f;
            float zc = (maxz + minz) / 2.0f;
            float rs = maxx - minx;
            rs = MathF.Max(rs, maxy - miny);
            rs = MathF.Max(rs, maxz - minz);
            return (new Vector3D<float>(xc, yc, zc), rs);
        }

        public void DrawHelp()
        {
            if (ImGui.Begin("Help"))
            {
                // TODO: Make this a table?
                ImGui.Text("Keys (h toggles this screen):");
                List<KeyHelp> baseHelps =
                [
                    //new("e", "toggle eye/model rotation", roteye ? "eye" : "model"),
                    new("a", "toggle animation", Animated ? "on" : "off"),
                    new("o", "toggle bounding sphere", sphere ? "on" : "off"),
                    new("q, ESC", "quit", ""),
                    new("z, x, arrows", "rotate", Rotation.ToString()),
                    new("0, 9", "zoom", Zoom.ToString()),
                    new("<, >", "decrease/increase alpha", Alpha.ToString()),
                    new("3, 4", "decrease/increase ambient", Ambient.ToString()),
                    new("5, 6", "decrease/increase diffuse", Diffuse.ToString()),
                    new("", "translation", Translation.ToString())
                ];
                List<KeyHelp> lastHelps =
                [
                    new("", "", ""),
                    new("Move mouse while holding left button pressed to rotate model", "", ""),
                    new("Move mouse while holding right button pressed to zoom", "", ""),
                    new("Move mouse while holding left button and shift pressed to move model", "", "")
                ];
                var objHelps = Controls.SelectMany(c => c.GetHelp());
                var helps = baseHelps.Concat(objHelps).Concat(lastHelps);
                foreach (var help in helps)
                    ImGui.Text($"{help.Keys}: {help.Description} {help.State}");
            }
            ImGui.End();
        }

        public void Render()
        {
            /*
            GlClear.color (0.5, 0.5, 0.5) ~alpha:1.0;
            GlClear.clear [`color; `depth];
            GlDraw.color (0.0, 0.0, 0.0);
            GlFunc.alpha_func `greater view.alpha;
            */

            if (sphere)
            {
                // TODO: Sphere
                /*
                let cx, cy, cz = view.center in
                let cx = -.cx and cy = -.cy and cz = -.cz in
                GlDraw.line_width 1.0;
                GlMat.mode `modelview;
                GlMat.push ();
                GlMat.translate3 (cx, cy, cz);
                GlDraw.polygon_mode `back `line;
                GlDraw.polygon_mode `front `line;
                Gl.disable `texture_2d;
                GluQuadric.sphere ~radius:(0.7*.view.radial_scale) ~stacks:25 ~slices:25 ();
                GlMat.pop ();
                */
            }

            foreach (var draw in Objs)
                draw.Draw();

            if (Help)
                DrawHelp();

            //Glut.swapBuffers ();
        }

        public (Vector3D<float>, Vector3D<float>) GetEyeAndUp()
        {
            //if (!roteye)
            //    return (new Vector3D<float>(0.0f, 0.0f, 2.0f), new Vector3D<float>(0.0f, 1.0f, 0.0f));

            var rx = Scalar.DegreesToRadians(Rotation.X);
            var ry = Scalar.DegreesToRadians(Rotation.Y);
            var rz = Scalar.DegreesToRadians(Rotation.Z);
            var q = Quaternion<float>.CreateFromYawPitchRoll(ry, rx, rz);
            
            var v = Vector3D.Transform((Vector3D<float>.UnitZ * 2.0f), q);
            var u = Vector3D.Transform(Vector3D<float>.UnitY, q);
            return (v, u);
        }

        public void Setup(float w, float h)
        {
            Size = new Vector2D<float>(w, h);

            var rs = Zoom / RadialScale;
            (var eye, var up) = GetEyeAndUp();

            GState.Projection = Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45.0f), w / h, 0.1f, 100.0f);

            // TODO: Hackey bad arcball emulation. Good enough for now.
            GState.View = Matrix4X4.CreateLookAt(eye, Vector3D<float>.Zero, up) * Matrix4X4.CreateTranslation(Translation);

            // Translate by negative center to move model to the middle of the world.
            // Also X is flipped, yes, really.
            GState.Model = Matrix4X4.CreateTranslation(-Center) * Matrix4X4.CreateScale(-rs, rs, rs);
            //if (!roteye)
            //{
            //    GState.Model = GState.Model * Matrix4X4.CreateFromAxisAngle(Vector3D<float>.UnitX, Scalar.DegreesToRadians(Rotation.X));
            //    GState.Model = GState.Model * Matrix4X4.CreateFromAxisAngle(-Vector3D<float>.UnitY, Scalar.DegreesToRadians(Rotation.Y));
            //    GState.Model = GState.Model * Matrix4X4.CreateFromAxisAngle(Vector3D<float>.UnitZ, Scalar.DegreesToRadians(Rotation.Z));
            //}

            //var projection = Matrix4X4<float>.Identity;
            //projection = projection * Matrix4X4.CreateTranslation(Translation);
            //projection = projection * Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45.0f), w / h, 0.1f, 100.0f);
            //GState.Projection = projection;
            //var modelView = Matrix4X4<float>.Identity;
            //modelView = modelView * Matrix4X4.CreateLookAt(eye, Vector3D<float>.Zero, up);
            //if (!roteye)
            //{
            //    modelView = modelView * Matrix4X4.CreateFromAxisAngle(Vector3D<float>.UnitX, Scalar.DegreesToRadians(Rotation.X));
            //    modelView = modelView * Matrix4X4.CreateFromAxisAngle(-Vector3D<float>.UnitY, Scalar.DegreesToRadians(Rotation.Y));
            //    modelView = modelView * Matrix4X4.CreateFromAxisAngle(Vector3D<float>.UnitZ, Scalar.DegreesToRadians(Rotation.Z));
            //}
            //modelView = modelView * Matrix4X4.CreateScale(-rs, rs, rs);
            //// Translate by negative center to move model to the middle of the world.
            //modelView = modelView * Matrix4X4.CreateTranslation(-Center);
            //GState.View = modelView;
            //GState.Model = Matrix4X4<float>.Identity;



            //GState.Projection = Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45.0f), w / h, 0.1f, 10.0f);
            //GState.View = Matrix4X4.CreateLookAt<float>(eye, Center, up);
            //Matrix4X4<float> model = Matrix4X4<float>.Identity;
            //model *= Matrix4X4.CreateScale<float>(-rs, rs, rs);
            //if (!roteye)
            //{
            //    model *= Matrix4X4.CreateRotationX(Rotation.X);
            //    model *= Matrix4X4.CreateRotationY(Rotation.Y);
            //    model *= Matrix4X4.CreateRotationZ(Rotation.Z);
            //}
            ////model *= Matrix4X4.CreateTranslation(Center)
            //GState.Model = model;
        }

        public void Resize(float w, float h)
        {
            Size = new Vector2D<float>(w, h);
        }

        public void Update(float dt)
        {
            Setup(Size.X, Size.Y);
            LastTime += dt;
        }

        public void Keyboard(IKeyboard keyboard, Key key, bool isDown, bool shift)
        {
            switch (key)
            {
                case Key.Escape or Key.Q:
                    // TODO: Quit
                    break;
                case Key.Number9:
                    Zoom += 0.05f;
                    break;
                case Key.Number0:
                    Zoom -= 0.05f;
                    break;
                case Key.Z:
                    Rotation = Rotation with { Y = Rotation.Y + aincr };
                    break;
                case Key.X:
                    Rotation = Rotation with { Y = Rotation.Y - aincr };
                    break;
                case Key.Left:
                    Rotation = Rotation with { Z = Rotation.Z + aincr };
                    break;
                case Key.Right:
                    Rotation = Rotation with { Z = Rotation.Z - aincr };
                    break;
                case Key.Up:
                    Rotation = Rotation with { X = Rotation.X - aincr };
                    break;
                case Key.Down:
                    Rotation = Rotation with { X = Rotation.X + aincr };    
                    break;
                case Key.E:
                    roteye = !roteye;
                    break;
                case Key.O:
                    sphere = !sphere;
                    break;
                case Key.H:
                    Help = !Help;
                    break;
                // case Key.P: Skin.SetText(); break;
                case Key.Comma when shift: // <
                    Alpha = MathF.Max(Alpha - 0.01f, 0.0f);
                    break;
                case Key.Period when shift: // >
                    Alpha = MathF.Min(Alpha + 0.01f, 1.0f);
                    break;
                case Key.Number3:
                    Ambient -= 0.01f;
                    break;
                case Key.Number4:
                    Ambient += 0.01f;
                    break;
                case Key.Number5:
                    Diffuse -= 0.01f;
                    break;
                case Key.Number6:
                    Diffuse += 0.01f;
                    break;
                default:
                    Controls.ForEach(c => c.Key(key, isDown));
                    break;
            }
            Setup(Size.X, Size.Y);
        }

        public void Mouse(float x, float y)
        {
            var dx = x - XY.X;
            var dy = y - XY.Y;
            XY = new Vector2D<float>(x, y);
            switch (this.MType)
            {
                case EMType.Move:
                    {
                        var nx = Translation.X + (dx / 100.0f);
                        var ny = Translation.Y - (dy / 100.0f);
                        Translation = Translation with { X = nx, Y = ny };
                        Setup(Size.X, Size.Y);
                    }
                    break;
                case EMType.Rotate:
                    {
                        var nx = Rotation.X + dy;
                        var ny = Rotation.Y - dx;
                        Rotation = Rotation with { X = nx, Y = ny };
                        Setup(Size.X, Size.Y);
                    }
                    break;
                case EMType.Zoom:
                    {
                        Zoom += (dy / 50.0f);
                        Setup(Size.X, Size.Y);
                    }
                    break;
                case EMType.None:
                default:
                    break;
            }
        }

        public void HandleMouseButton(IMouse mouse, MouseButton button, bool isDown, bool shift)
        {
            if (button == MouseButton.Left)
            {
                if (isDown)
                {
                    XY = mouse.Position.ToGeneric();
                    if (shift)
                        MType = EMType.Move;
                    else
                        MType = EMType.Rotate;
                }
                else
                {
                    MType = EMType.None;
                }
            }
            else if (button == MouseButton.Right)
            {
                if (isDown)
                {
                    XY = mouse.Position.ToGeneric();
                    MType = EMType.Zoom;
                }
                else
                {
                    MType = EMType.None;
                }
            }
        }

        public void AddObj(IDraw obj)
        {
            Objs.Add(obj);
            if (obj is IKeyReceiver keyRec)
                Controls.Add(keyRec);
        }

        public void Init(Vector3 min, Vector3 max)
        {
            (var center, var scale) = CenterAndRadialScale(min.X, max.X, min.Y, max.Y, min.Z, max.Z);
            Center = center;
            RadialScale = scale;
        }
    }
}
