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
using System.Numerics;
using Viewer.Graphics;
using Image = Viewer.Graphics.Image;

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

        ShaderProgram? theShader = null;
        View? view = null;
        List<(Nmo, StaticMesh)> loadedMeshes = new List<(Nmo, StaticMesh)>();
        Box3D<float> meshBounds;
        //GLStats stats;

        Dictionary<string, (Nto, Image)> loadedImages = new Dictionary<string, (Nto, Image)>();
        //float tviewScale = 1.0f;

        public Program()
        {
            var winOpts = WindowOptions.Default;
            winOpts.Title = "SoTC Viewer";
            winOpts.Samples = 4;
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
            _gl.Enable(EnableCap.Blend);
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

            //var vertShader = File.ReadAllText(".\\Resources\\Shaders\\unlit_vcol.vert.glsl");
            //var fragShader = File.ReadAllText(".\\Resources\\Shaders\\unlit_vcol.frag.glsl");
            var vertShader = File.ReadAllText(".\\Resources\\Shaders\\unlit_vcol_tex.vert.glsl");
            var fragShader = File.ReadAllText(".\\Resources\\Shaders\\unlit_vcol_tex.frag.glsl");
            theShader = new ShaderProgram(_gl, vertShader, fragShader);

            view = new View();
            view.Resize(_Window.FramebufferSize.X, _Window.FramebufferSize.Y);

            //stats = new GLStats(_gl);
        }

        private void OnMouseUp(IMouse mouse, MouseButton button)
        {
            if (ImGui.GetIO().WantCaptureMouse)
                return;
            view!.HandleMouseButton(mouse, button, false, _Input.Keyboards[0].IsKeyPressed(Key.ShiftLeft) | _Input.Keyboards[0].IsKeyPressed(Key.ShiftRight));
        }

        private void OnMouseDown(IMouse mouse, MouseButton button)
        {
            if (ImGui.GetIO().WantCaptureMouse)
                return;
            view!.HandleMouseButton(mouse, button, true, _Input.Keyboards[0].IsKeyPressed(Key.ShiftLeft) | _Input.Keyboards[0].IsKeyPressed(Key.ShiftRight));
        }

        private void OnMouseMove(IMouse mouse, Vector2 position)
        {
            if (ImGui.GetIO().WantCaptureMouse)
                return;
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
                OpenFile();
                //TestFile();
                CalcMeshBounds();
                view.Init(meshBounds.Min.ToSystem(), meshBounds.Max.ToSystem());
            }

            _ImGui.NewFrame();

            //ImGui.ShowDemoWindow();
            //ImGui.ShowDebugLogWindow();

            view!.Update((float)delta);

            DrawMainMenu();
            if (ImGui.Begin("Debug View"))
            {
                ImGui.Text("Model");
                ImGui.Indent();
                ImGui.Text($"Min:    {meshBounds.Min}");
                ImGui.Text($"Max:    {meshBounds.Max}");
                ImGui.Text($"Center: {meshBounds.Center}");
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
                ImGui.Text($"FPS: {1.0 / delta}");
                //ImGui.Text($"Prims: {stats.LastValue}");
            }
            ImGui.End();

            DrawTextureList();
            DrawModelList();

            //if (ImGui.Begin("NOTICE", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
            //{
            //    ImGui.Text("No Index Loaded!");
            //    ImGui.Text("No DAT File Loaded!");
            //}
        }

        private void OnRender(double delta)
        {
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            //stats.BeginFrame();

            view!.Render();

            if (loadedMeshes.Count != 0)
            {
                var modelLoc = theShader.GetUniformLocation("model");
                var viewLoc = theShader.GetUniformLocation("view");
                var projLoc = theShader.GetUniformLocation("projection");
                theShader.UseShader();
                unsafe
                {
                    var modelMtx = view.GState.Model;
                    _gl.UniformMatrix4(modelLoc, 1, false, (float*)&modelMtx);
                    var viewMtx = view.GState.View;
                    _gl.UniformMatrix4(viewLoc, 1, false, (float*)&viewMtx);
                    var projMtx = view.GState.Projection;
                    _gl.UniformMatrix4(projLoc, 1, false, (float*)&projMtx);
                }
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.Uniform1(theShader.GetUniformLocation("tex_diffuse"), 0);
                foreach ((_, var mesh) in loadedMeshes)
                {
                    mesh.Draw(theShader);
                }

            }

            //stats.EndFrame();

            _ImGui.Render();
        }

        private void OnFramebufferResize(Vector2D<int> size)
        {
            _gl.Viewport(size);
            view!.Resize(size.X, size.Y);
        }

        private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
        {
            if (ImGui.GetIO().WantCaptureKeyboard)
                return;

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
            foreach ((_, var mesh) in loadedMeshes)
                mesh.Dispose();
            foreach ((_, (_, var img)) in loadedImages)
                img.Dispose();
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

        void OpenFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.RestoreDirectory = true;
                ofd.AddToRecent = false;
                ofd.Multiselect = true;
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    foreach (var filePath in ofd.FileNames)
                    {
                        var ext = Path.GetExtension(filePath);
                        if (ext == ".nmo")
                        {
                            LoadNMO(filePath);
                        }
                        else if (ext == ".nto")
                        {
                            LoadNTO(filePath);
                        }
                    }
                }
            }
        }

        private void LoadNTO(string filePath)
        {
            try
            {
                var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var xff = Xff.Read(fs);
                if (xff == null)
                    return;
                var nto = Nto.FromXff(xff);
                if (nto == null)
                    return;
                var img = new Image(_gl, nto.Name, nto.Width, nto.Height, nto.PixelData);
                loadedImages.Add(Path.GetFileNameWithoutExtension(filePath), (nto, img));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fault Reading {filePath} : {ex.Message}");
                return;
            }

        }

        private void LoadNMO(string filePath)
        {
            try
            {
                var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var xff = Xff.Read(fs);
                if (xff == null)
                    return;
                var nmo = Nmo.FromXff(xff);
                if (nmo == null)
                    return;
                var sm = StaticMeshExtensions.FromNMO(_gl, nmo);
                if (sm == null)
                    return;
                loadedMeshes.Add((nmo, sm));

                foreach(var tName in sm.Textures.Except(loadedImages.Keys))
                {
                    var newPath = Path.Combine(Path.GetDirectoryName(filePath), "..", "nto", $"{tName}.nto");
                    if (File.Exists(newPath))
                        LoadNTO(newPath);
                }
                sm.LinkTextures(loadedImages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fault Reading {filePath} : {ex.Message}");
                return;
            }
        }

        void CalcMeshBounds()
        {
            if (loadedMeshes.Count == 0)
            {
                meshBounds = new Box3D<float>();
                return;
            }

            Vector3D<float> min = loadedMeshes[0].Item2.Bounds.Min;
            Vector3D<float> max = loadedMeshes[0].Item2.Bounds.Max;
            for (int i = 1; i < loadedMeshes.Count; i++)
            {
                min = Vector3D.Min(min, loadedMeshes[i].Item2.Bounds.Min);
                max = Vector3D.Max(max, loadedMeshes[i].Item2.Bounds.Max);
            }

            meshBounds = new Box3D<float>(min, max);
        }

        bool showTextureViewer = false;
        Image? textureViewerImage = null;
        float textureViewerZoom = 1.0f;

        void DrawTextureList()
        {
            Widgets.Window("Textures", () =>
            {
                ImGui.Text($"Loaded textures: {loadedImages.Count}");
                foreach ((string name, (Nto nto, Image img)) in loadedImages)
                {
                    Widgets.TreeNode(name, () =>
                    {
                        ImGui.Text($"Width:  {img.Width}");
                        ImGui.Text($"Height: {img.Height}");
                        if (ImGui.Button("Preview"))
                        {
                            textureViewerImage = img;
                            textureViewerZoom = 1.0f;
                            showTextureViewer = true;
                        }
                    });
                }
            });

            if (showTextureViewer && textureViewerImage != null)
            {
                Widgets.Window("Texture Viewer", ref showTextureViewer, () =>
                {
                    ImGui.Text(textureViewerImage.Name);
                    unsafe
                    {
                        fixed (float* pf = &textureViewerZoom)
                        {
                            ImGui.DragScalar("Scale", ImGuiDataType.Float, (nint)pf);
                        }
                    }
                    ImGui.Image((nint)textureViewerImage.Texture._handle,
                        new Vector2(textureViewerImage.Width * textureViewerZoom, textureViewerImage.Height * textureViewerZoom));
                });
            }
        }

        bool showModelDetail = false;
        Nmo? selectedModel = null;

        void DrawModelList()
        {
            Widgets.Window("Models", () =>
            {
                ImGui.Text($"Loaded Models: {loadedMeshes.Count}");
                foreach ((Nmo nmo, StaticMesh sm) in loadedMeshes)
                {
                    Widgets.TreeNode(nmo.ModelName, () =>
                    {
                        ImGui.Text($"TEX count: {nmo.Textures.Count}");
                        ImGui.Text($"SURF count: {nmo.Surfaces.Count}");
                        ImGui.Text($"DMA count: {nmo.Meshes.Count}");
                        if (ImGui.Button("Detail"))
                        {
                            selectedModel = nmo;
                            showModelDetail = true;
                        }
                    });
                }
            });

            if (showModelDetail && selectedModel != null)
            {
                Widgets.Window("Model Detail", ref showModelDetail, () =>
                {
                    ImGui.Text($"Current: {selectedModel.ModelName}");
                    Widgets.TreeNode("Header", () =>
                    {
                        var hdr = selectedModel.Header;
                        ImGui.Text($"{hdr.Ident:X4} {hdr.Unk_04:X4} {hdr.Unk_08:X4} {hdr.Unk_0C:X4}");
                        ImGui.Text($"{hdr.Unk_10:X4} {hdr.Unk_14:X4} {hdr.Unk_18:X4} {hdr.Unk_1C:X4}");
                        ImGui.Text($"{hdr.Unk_20:F4} {hdr.Unk_24:F4} {hdr.Unk_28:F4} {hdr.Unk_2C:F4}");
                    });
                    Widgets.TreeNode("Chunk Info", () =>
                    {
                        var chnk = selectedModel.ChunkInfo;
                        ImGui.Text($"BOX  {chnk[0].Offset:X4} {chnk[0].Count:G4} {chnk[0].Unk_08:X4} {chnk[0].Unk_0C:X4}");
                        ImGui.Text($"TEX  {chnk[1].Offset:X4} {chnk[1].Count:G4} {chnk[1].Unk_08:X4} {chnk[1].Unk_0C:X4}");
                        ImGui.Text($"SURF {chnk[2].Offset:X4} {chnk[2].Count:G4} {chnk[2].Unk_08:X4} {chnk[2].Unk_0C:X4}");
                        ImGui.Text($"VIF  {chnk[3].Offset:X4} {chnk[3].Count:G4} {chnk[3].Unk_08:X4} {chnk[3].Unk_0C:X4}");
                        ImGui.Text($"NAME {chnk[4].Offset:X4} {chnk[4].Count:G4} {chnk[4].Unk_08:X4} {chnk[4].Unk_0C:X4}");
                    });
                    Widgets.TreeNode("Unknown Chunks", () =>
                    {
                        var chunk = selectedModel.Boxes[0];
                        ImGui.Text("Note: count of this chunk type is currently unknown, only first shown.");
                        ImGui.Text($"{chunk.X1:F4} {chunk.Y1:F4} {chunk.Z1:F4} {chunk.W1:F4}");
                        ImGui.Text($"{chunk.X2:F4} {chunk.Y2:F4} {chunk.Z2:F4} {chunk.W2:F4}");
                        ImGui.Text($"{chunk.Left:G4} {chunk.Right:G4} {chunk.C:G4} {chunk.D:G4}");
                    });
                    Widgets.TreeNode("TEX Chunks", () =>
                    {
                        for (int i = 0; i < selectedModel.Textures.Count; i++)
                        {
                            Nmo.ChunkTEX tex = selectedModel.Textures[i];
                            Widgets.TreeNode($"{i} [{tex.Name}]", () =>
                            {
                                ImGui.Text($"Nums: {tex.Nums[0]} {tex.Nums[1]} {tex.Nums[2]} {tex.Nums[3]}");
                                ImGui.Text($"Shorts: {tex.Shorts[0]} {tex.Shorts[1]}");
                                ImGui.Text($"Width: {tex.Width} Height: {tex.Height}");
                            });
                        }
                    });
                    Widgets.TreeNode("SURF Chunks", () =>
                    {
                        for (int i = 0; i < selectedModel.Surfaces.Count; i++)
                        {
                            Nmo.ChunkSURF surf = selectedModel.Surfaces[i];
                            Widgets.TreeNode($"{i} [{surf.Name}]", () =>
                            {
                                ImGui.Text($"Tris: {surf.Tricount} Strips: {surf.Stripcount}");
                                ImGui.Text($"Layers:");
                                ImGui.Indent();
                                ImGui.Text($"{surf.MaybeTint[0]:X} {surf.MaybeTint[1]:X} {surf.MaybeTint[2]:X} {surf.MaybeTint[3]:X}  {surf.MeshFormat:X} {surf.UnkFlag1:X} {surf.UnkFlag2:X} {surf.UnkFlag3:X} {surf.UnkInt}");
                                ImGui.Text($"{surf.Hdr1[0].Zero[0]:X} {surf.Hdr1[0].Zero[1]:X} {surf.Hdr1[0].Zero[2]:X} {surf.Hdr1[0].Zero[3]:X}  {surf.Hdr1[0].One} {surf.Hdr1[0].Two:X} {surf.Hdr1[0].Three}");
                                ImGui.Text($"{surf.Hdr1[1].Zero[0]:X} {surf.Hdr1[1].Zero[1]:X} {surf.Hdr1[1].Zero[2]:X} {surf.Hdr1[1].Zero[3]:X}  {surf.Hdr1[1].One} {surf.Hdr1[1].Two:X} {surf.Hdr1[1].Three}");
                                ImGui.Unindent();
                                ImGui.Text($"Two:");
                                ImGui.Indent();
                                ImGui.Text($"{surf.Hdr2.Zero} {surf.Hdr2.One} {surf.Hdr2.Two} {surf.Hdr2.Three[0]}");
                                ImGui.Text($"{surf.Hdr2.Three[1]} {surf.Hdr2.Three[2]} {surf.Hdr2.Three[3]} {surf.Hdr2.Three[4]}");
                                ImGui.Unindent();
                            });
                        }
                    });
                });
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
