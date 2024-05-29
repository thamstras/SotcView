using System.Drawing;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using ImGuiNET;
using Viewer.ImGUI;
//using Silk.NET.GLFW;
using NicoLib;
using NicoLib.PS2;
using Viewer.OGL;

namespace Viewer
{
    /*
     
    TODO: Need to fill in all the RAII-esque OGL wrappers.
    Also need to write my own ImGui backend directly referencing ImGui.NET.
    The provided one works, but is no good for serious use.
     
     */
    class Program : IDisposable
    {
        IWindow _Window;
        GL _gl;
        IInputContext _Input;
        ImGUIAdapter _ImGui;
        bool _requestOpen = true;
        bool _requestExit = false;

        Xff? container = null;
        Nmo? model = null;

        ShaderProgram? theShader = null;
        View? view = null;
        StaticMesh? staticMesh = null;
        List<StaticMesh> extraMeshes = new List<StaticMesh>();

        public Program()
        {
            var winOpts = WindowOptions.Default;
            winOpts.Title = "SoTC Viewer";
            _Window = Window.Create(winOpts);
            _Window.Load += OnLoad;
            _Window.Update += OnUpdate;
            _Window.Render += OnRender;
            _Window.FramebufferResize += OnFramebufferResize;
            _Window.Closing += OnClose;
        }

        public void Run()
        {
            _Window.Run();
        }

        private void OnLoad()
        {
            _gl = _Window.CreateOpenGL();
            _Input = _Window.CreateInput();

            _Input.Keyboards[0].KeyDown += OnKeyDown;
            _Input.Mice[0].MouseMove += OnMouseMove;
            _Input.Mice[0].MouseDown += OnMouseDown;
            _Input.Mice[0].MouseUp += OnMouseUp;

            _gl.Enable(EnableCap.DepthTest);
            //_gl.Enable(EnableCap.CullFace);
            _gl.Enable(EnableCap.Multisample);
            _gl.Enable(EnableCap.LineSmooth);
            _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.One);
            _gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            _gl.DepthFunc(DepthFunction.Lequal);

            _ImGui = new ImGUIAdapter(_gl, _Window, _Input, io =>
            {
                io.ConfigWindowsMoveFromTitleBarOnly = true;
                io.ConfigWindowsResizeFromEdges = true;

                ImGuiStylePtr style = ImGui.GetStyle();
                style.FrameBorderSize = 1.0f;
                style.WindowRounding = 7.0f;

                io.Fonts.AddFontFromFileTTF(".\\Resources\\Fonts\\Aldrich-Regular.ttf", 12.0f);
            });

            var vertShader = File.ReadAllText(".\\Resources\\Shaders\\unlit_vcol.vert.glsl");
            var fragShader = File.ReadAllText(".\\Resources\\Shaders\\unlit_vcol.frag.glsl");
            theShader = new ShaderProgram(_gl, vertShader, fragShader);

            view = new View();
            view.Resize(_Window.FramebufferSize.X, _Window.FramebufferSize.Y);
        }

        private void OnMouseUp(IMouse mouse, MouseButton button)
        {
            view!.HandleMouseButton(mouse, button, false, _Input.Keyboards[0].IsKeyPressed(Key.ShiftLeft) | _Input.Keyboards[0].IsKeyPressed(Key.ShiftRight));
        }

        private void OnMouseDown(IMouse mouse, MouseButton button)
        {
            view!.HandleMouseButton(mouse, button, true, _Input.Keyboards[0].IsKeyPressed(Key.ShiftLeft) | _Input.Keyboards[0].IsKeyPressed(Key.ShiftRight));
        }

        private void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
        {
            view!.Mouse(position.X, position.Y);
        }

        private void OnUpdate(double delta)
        {
            if (_requestExit)
            {
                _Window.Close();
                return;
            }

            if (_requestOpen)
            {
                _requestOpen = false;
                OpenFile(staticMesh != null);
                //TestFile();
                staticMesh = StaticMeshExtensions.FromNMO(_gl, model);
                view.Init(staticMesh.Bounds.Min.ToSystem(), staticMesh.Bounds.Max.ToSystem());
            }

            _ImGui.NewFrame();

            //ImGui.ShowDemoWindow();
            //ImGui.ShowDebugLogWindow();

            view!.Update((float)delta);

            if (staticMesh != null)
            {
                if (ImGui.Begin("Debug View"))
                {
                    ImGui.Text("Model");
                    ImGui.Indent();
                    ImGui.Text($"Min:    {staticMesh.Bounds.Min}");
                    ImGui.Text($"Max:    {staticMesh.Bounds.Max}");
                    ImGui.Text($"Center: {staticMesh.Bounds.Center}");
                    ImGui.Unindent();
                    ImGui.Text("View");
                    ImGui.Indent();
                    ImGui.Text($"Size:     {view.Size}");
                    ImGui.Text($"Rotation: {view.Rotation}");
                    ImGui.Text($"Zoom:     {view.Zoom}");
                    ImGui.Text($"Center:   {view.Center}");
                    ImGui.Text($"Scale:    {view.RadialScale}");
                    ImGui.Text($"XY:       {view.XY}");
                    ImGui.Text($"Transl:   {view.Translation}");
                    ImGui.Unindent();
                    ImGui.Text("GState");
                    ImGui.Indent();
                    ImGui.Text($"Model {view.GState.Model.Row1}");
                    ImGui.Text($"      {view.GState.Model.Row2}");
                    ImGui.Text($"      {view.GState.Model.Row3}");
                    ImGui.Text($"      {view.GState.Model.Row4}");
                    ImGui.Text("");
                    ImGui.Text($"View  {view.GState.View.Row1}");
                    ImGui.Text($"      {view.GState.View.Row2}");
                    ImGui.Text($"      {view.GState.View.Row3}");
                    ImGui.Text($"      {view.GState.View.Row4}");
                    ImGui.Text("");
                    ImGui.Text($"Proj  {view.GState.Projection.Row1}");
                    ImGui.Text($"      {view.GState.Projection.Row2}");
                    ImGui.Text($"      {view.GState.Projection.Row3}");
                    ImGui.Text($"      {view.GState.Projection.Row4}");
                    ImGui.Unindent();
                    ImGui.Text($"FPS: {1.0 / delta}");
                }
                ImGui.End();
            }

            //if (ImGui.Begin("NOTICE", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
            //{
            //    ImGui.Text("No Index Loaded!");
            //    ImGui.Text("No DAT File Loaded!");
            //}
        }

        private void OnRender(double delta)
        {
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            DrawMainMenu();

            view!.Render();

            if (staticMesh != null)
            {
                var modelLoc = theShader.GetUniformLocation("model");
                var viewLoc = theShader.GetUniformLocation("view");
                var projLoc = theShader.GetUniformLocation("projection");
                theShader.UseShader();
                unsafe
                {
                    // TODO: These matricies are total bollocks
                    var modelMtx = view.GState.Model;
                    //var modelMtx = Matrix4X4<float>.Identity;
                    //var modelMtx = Matrix4X4.CreateScale(0.1f);
                    _gl.UniformMatrix4(modelLoc, 1, false, (float*)&modelMtx);
                    var viewMtx = view.GState.View;
                    //var viewMtx = Matrix4X4.CreateLookAt(new Vector3D<float>(0.0f, 5.0f, -10.0f), Vector3D<float>.Zero, Vector3D<float>.UnitY);
                    _gl.UniformMatrix4(viewLoc, 1, false, (float*)&viewMtx);
                    var projMtx = view.GState.Projection;
                    //var projMtx = Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45.0f), _Window.Size.X / _Window.Size.Y, 0.1f, 100.0f);
                    _gl.UniformMatrix4(projLoc, 1, false, (float*)&projMtx);
                }
                staticMesh.Draw();
                foreach (var mesh in extraMeshes) mesh.Draw();

            }

            _ImGui.Render();
        }

        private void OnFramebufferResize(Vector2D<int> size)
        {
            _gl.Viewport(size);
            view!.Resize(size.X, size.Y);
        }

        private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
        {
            if (key == Key.Escape)
            {
               _Window.Close();
            }
            else
            {
                var shift = keyboard.IsKeyPressed(Key.ShiftLeft) | keyboard.IsKeyPressed(Key.ShiftRight);
                view!.Keyboard(keyboard, key, true, shift);
            }
        }

        private void OnClose()
        {
            _ImGui?.Dispose();
            _Input?.Dispose();
            _gl?.Dispose();
        }

        public void Dispose()
        {
            _Window?.Dispose();
        }

        void DrawMainMenu()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open..."))
                    {
                        _requestOpen = true;
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Exit"))
                    {
                        _requestExit = true;
                    }

                    ImGui.EndMenu();
                }
                
                ImGui.EndMainMenuBar();
            }
        }

        void OpenFile(bool multi = false)
        {
            using (System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog())
            {
                ofd.RestoreDirectory = true;
                ofd.AddToRecent = false;
                if (multi)
                    ofd.Multiselect = true;
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (!multi)
                    {
                        string filePath = ofd.FileName;
                        var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                        try
                        {
                            container = Xff.Read(fs);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                        if (container == null)
                            return;
                        try
                        {
                            model = Nmo.FromXff(container);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                    else
                    {
                        foreach (var filePath in ofd.FileNames)
                        {
                            try
                            {
                                var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                                var xff = Xff.Read(fs);
                                if (xff == null)
                                    continue;
                                var nmo = Nmo.FromXff(xff);
                                if (nmo == null)
                                    continue;
                                var sm = StaticMeshExtensions.FromNMO(_gl, nmo);
                                if (sm == null)
                                    continue;
                                extraMeshes.Add(sm);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Fault Reading {filePath} : {ex.Message}");
                                continue;
                            }
                        }
                    }
                }
            }
        }

        //void TestFile()
        //{
        //    if (model == null)
        //        return;

        //    //model.Meshes[0].Data
        //    for (int c = 0; c < 3; c++)
        //    {
        //        var chunk = model.Meshes[c];
        //        VifProcessor vif = new VifProcessor(chunk.Data);
        //        Console.WriteLine($"Chain Info {chunk.size} {chunk.unk_04} {chunk.surf} {chunk.unk_0c} {chunk.offset} {chunk.tri_count} {chunk.strip_count} {chunk.unk_1c}");
        //        bool done = false;
        //        do
        //        {
        //            var state = vif.Run();

        //            if (state == VifProcessor.State.Microprogram)
        //            {
        //                Console.WriteLine("STEP");
        //            }
        //            else
        //            {
        //                Console.WriteLine("END");
        //                done = true;
        //                break;
        //            }

        //            Console.WriteLine("     | 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
        //            Console.WriteLine("-----|------------------------------------------------");
        //            int addr = 0;
        //            //for (int addr = 0; addr < 0x10 + (0x32 * 0x30); addr += 0x10)
        //            //{
        //                Console.WriteLine($"{addr:X4} | {vif.Memory[addr + 0]:X2} {vif.Memory[addr + 1]:X2} {vif.Memory[addr + 2]:X2} {vif.Memory[addr + 3]:X2} " +
        //                    $"{vif.Memory[addr + 4]:X2} {vif.Memory[addr + 5]:X2} {vif.Memory[addr + 6]:X2} {vif.Memory[addr + 7]:X2} " +
        //                    $"{vif.Memory[addr + 8]:X2} {vif.Memory[addr + 9]:X2} {vif.Memory[addr + 10]:X2} {vif.Memory[addr + 11]:X2} " +
        //                    $"{vif.Memory[addr + 12]:X2} {vif.Memory[addr + 13]:X2} {vif.Memory[addr + 14]:X2} {vif.Memory[addr + 15]:X2}");
        //            //}
        //        }
        //        while (!done);
        //        Console.WriteLine();
        //    }
        //}

        [STAThread]
        static void Main(string[] args)
        {
            Program? theProgram = null;
            try
            {
                theProgram = new Program();
                theProgram.Run();
            }
            finally
            {
                theProgram?.Dispose();
            }
        }
    }
}
