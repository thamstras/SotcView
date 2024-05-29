using NicoLib.PS2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Xe.BinaryMapper;

namespace NicoLib
{
    public class Nmo
    {
        public class FileHeader
        {
            [Data] public UInt32 Ident { get; set; }
            [Data] public UInt32 Unk_04 { get; set; }
            [Data] public UInt32 Unk_08 { get; set; }
            [Data] public UInt32 Unk_0C { get; set; }
            [Data] public UInt32 Unk_10 { get; set; }
            [Data] public UInt32 Unk_14 { get; set; }
            [Data] public UInt32 Unk_18 { get; set; }
            [Data] public UInt32 Unk_1C { get; set; }
            [Data] public float Unk_20 { get; set; }
            [Data] public float Unk_24 { get; set; }
            [Data] public float Unk_28 { get; set; }
            [Data] public float Unk_2C { get; set; }
        }

        public class ChunkDef
        {
            [Data] public UInt32 Offset { get; set; }
            [Data] public UInt32 Count { get; set; }
            [Data] public UInt32 Unk_08 { get; set; }
            [Data] public UInt32 Unk_0C { get; set; }
        }

        public class ChunkZero
        {
            [Data] public float X1 { get; set; }
            [Data] public float Y1 { get; set; }
            [Data] public float Z1 { get; set; }
            [Data] public float W1 { get; set; }
            [Data] public float X2 { get; set; }
            [Data] public float Y2 { get; set; }
            [Data] public float Z2 { get; set; }
            [Data] public float W2 { get; set; }
            [Data] public int A { get; set; }
            [Data] public int B { get; set; }
            [Data] public int C { get; set; }
            [Data] public int D { get; set; }
        }

        public class ChunkTEX
        {
            [Data] public UInt32 Sig { get; set; }
            [Data] public UInt32 Name_off { get; set; }
            [Data(Count = 4)] public UInt32[] Nums { get; set; }
            [Data(Count = 2)] public UInt16[] Shorts { get; set; }
            [Data] public UInt16 Width { get; set; }
            [Data] public UInt16 Height { get; set; }

            public string Name { get; set; }
        }

        public class ChunkSURF
        {
            public class Header1
            {
                [Data(Count = 4)] public byte[] Zero { get; set; }
                [Data] public float One { get; set; }
                [Data] public UInt32 Two { get; set; }
                [Data] public UInt32 Three { get; set; }
            }

            public class Header2
            {
                [Data] public UInt32 Zero { get; set; }
                [Data] public float One { get; set; }
                [Data] public float Two { get; set; }
                [Data(Count = 5)] public UInt32[] Three { get; set; }
            }

            [Data] public UInt32 Sig { get; set; }
            [Data] public UInt32 Tricount { get; set; }
            [Data] public UInt32 Stripcount { get; set; }
            [Data] public UInt32 Name_off { get; set; }
            [Data(Count = 3)] public Header1[] Hdr1 { get; set; }
            [Data] public Header2 Hdr2 { get; set; }
            [Data(Count = 48)] public UInt32[] Hdr3 { get; set; }

            public string Name { get; set; }
        }

        public class ChunkVIF
        {
            [Data] public UInt32 size { get; set; }
            [Data] public UInt32 unk_04 { get; set; }
            [Data] public UInt32 surf { get; set; }
            [Data] public UInt32 unk_0c { get; set; }
            [Data] public UInt32 offset { get; set; }
            [Data] public UInt32 tri_count { get; set; }
            [Data] public UInt32 strip_count { get; set; }
            [Data] public UInt32 unk_1c { get; set; }

            public byte[] Data { get; set; }
        }

        enum NmoChunkType
        {
            CHUNK_UNKNOWN = 0,
            CHUNK_TEX = 1,
            CHUNK_SURF = 2,
            CHUNK_VIF = 3,
            CHUNK_NAME = 4
        }

        public FileHeader Header { get; set; }
        public List<ChunkDef> ChunkInfo { get; set; }
        public ChunkZero UnknownChunk { get; set; }
        public List<ChunkTEX> Textures { get; set; }
        public List<ChunkSURF> Materials { get; set; }
        public List<ChunkVIF> Meshes { get; set; }
        public string ModelName { get; set; }

        public Nmo(FileHeader header, List<ChunkDef> chunkInfo, ChunkZero unknownChunk, List<ChunkTEX> textures, List<ChunkSURF> materials, List<ChunkVIF> meshes, string modelName)
        {
            Header = header;
            ChunkInfo = chunkInfo;
            UnknownChunk = unknownChunk;
            Textures = textures;
            Materials = materials;
            Meshes = meshes;
            ModelName = modelName;
        }

        public static Nmo FromXff(Xff xff)
        {
            Xff.SectionDef section = xff.Sections.FirstOrDefault(s => s.Name == ".rodata") ?? throw new ApplicationException("NMO .rodata section not found!");

            MemoryStream stream = new MemoryStream(section.Data, false);
            BinaryReader reader = new BinaryReader(stream);
            reader.Seek(xff.Header.Off_sign);

            FileHeader header = BinaryMapping.ReadObject<FileHeader>(stream);

            List<ChunkDef> chunkDefs = new List<ChunkDef>();
            for (int i = 0; i < 5; i++)
                chunkDefs.Add(BinaryMapping.ReadObject<ChunkDef>(stream));

            reader.Seek(chunkDefs[0].Offset);
            // Note: Count is unknown, so we only read the first one.
            ChunkZero chunkZero = BinaryMapping.ReadObject<ChunkZero>(stream);

            reader.Seek(chunkDefs[1].Offset);
            List<ChunkTEX> tex_list = new List<ChunkTEX>();
            for (int i = 0; i < chunkDefs[1].Count; i++)
                tex_list.Add(BinaryMapping.ReadObject<ChunkTEX>(stream));

            reader.Seek(chunkDefs[2].Offset);
            List<ChunkSURF> surfs = new List<ChunkSURF>();
            for (int i = 0; i < chunkDefs[2].Count; i++)
                surfs.Add(BinaryMapping.ReadObject<ChunkSURF>(stream));

            reader.Seek(chunkDefs[3].Offset);
            List<ChunkVIF> counts = new List<ChunkVIF>();
            for (int i = 0; i < chunkDefs[3].Count; i++)
                counts.Add(BinaryMapping.ReadObject<ChunkVIF>(stream));

            reader.Seek(chunkDefs[4].Offset);
            string modelName = reader.ReadCString();

            foreach (var tex in tex_list)
            {
                reader.Seek(tex.Name_off);
                tex.Name = reader.ReadCString();
            }

            foreach (var surf in surfs)
            {
                reader.Seek(surf.Name_off);
                surf.Name = reader.ReadCString();
            }

            foreach (var count in counts)
            {
                reader.Seek(count.offset);
                count.Data = reader.ReadBytes((int)count.size);
            }

            return new Nmo(header, chunkDefs, chunkZero, tex_list, surfs, counts, modelName);
        }

        public enum Primative : byte
        {
            PRIMATIVE_TRIANGLE_STRIP = 0x30,
            PRIMATIVE_TRIANGLE_STRIP_TWO_SIDED = 0x31
        }

        public enum MeshFormat : ushort
        {
            FORMAT_STATIC_EVEN = 0x0142,
            FORMAT_STATIC_ODD = 0x01BB,
            FORMAT_SKELE_EVEN = 0x02CA,
            FORMAT_SKELE_ODD = 0x0307
        }

        public struct Vertex
        {
            // No idea how this works yet
            public struct SkinRef
            {
                public float A { get; set; }
                public float B { get; set; }
                public float C { get; set; }
                public int D { get; set; }
            }

            public Vector3 Position { get; set; }
            public Vector4 Normal { get; set; }
            public Vector2 UV { get; set; }
            public Vector4 Color { get; set; }
            public SkinRef Skin { get; set; }
        }

        class VifHeader
        {
            [Data] public byte Vert_count { get; set; }
            [Data] public byte Unk_01 { get; set; }     // 0x80
            [Data] public ushort Unk_02 { get; set; }   // 0x0000
            [Data] public ushort Unk_04 { get; set; }   // 0x4000
            [Data] public byte Unk_06 { get; set; }   // 0x3E
            [Data] public Primative PrimativeType { get; set; }
            [Data] public uint Unk_08 { get; set; }     // 0x00000412
            [Data] public MeshFormat Format { get; set; }
            [Data] public ushort Unk_0E { get; set; }   // 0x00
        }

        // TODO: This should *definitly* be defined somewhere else
        //class MeshSection
        //{
        //    List<Vertex> vertices;
        //    GeomType type = GeomType.TriStrip;
        //    TriWinding winding = TriWinding.CCW;
        //}

        public class TriStrip
        {
            public List<Vertex> Verts { get; set; } = new List<Vertex>();
            public Primative PrimativeType { get; set; }
            public bool isEvenWiding { get; set; }   // TODO: not to do with face winding
            public int VertCount => Verts.Count;
            public int TriCount => VertCount - 2;
        }

        // TODO: The vertex processing might be better off in a separate class
        public List<TriStrip> ReadVifPacket(ChunkVIF chunk)
        {
            List<TriStrip> strips = new List<TriStrip>();
            VifProcessor vif = new VifProcessor(chunk.Data);
            bool done = false;
            //while (vif.Run() == VifProcessor.State.Microprogram)
            do
            {
                var vifState = vif.Run();
                if (vifState == VifProcessor.State.End)
                    done = true;

                if (vif.Memory[0] == 0)
                    continue;

                using (MemoryStream ms = new MemoryStream(vif.Memory))
                using (BinaryReader bs = new BinaryReader(ms))
                {
                    TriStrip strip = new TriStrip();

                    VifHeader header = BinaryMapping.ReadObject<VifHeader>(ms);

                    if (header.Unk_01 != 0x80
                        || header.Unk_02 != 0x0000
                        || header.Unk_04 != 0x4000
                        || header.Unk_06 != 0x3E
                        //|| header.PrimativeType != Primative.PRIMATIVE_TRIANGLE_STRIP
                        || header.Unk_08 != 0x00000412
                        || header.Unk_0E != 0x0000)
                    {
                        Console.WriteLine("     | 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
                        Console.WriteLine("-----|------------------------------------------------");
                        //int addr = 0;
                        for (int addr = 0; addr < 0x10 + (0x32 * 0x30); addr += 0x10)
                        {
                            Console.WriteLine($"{addr:X4} | {vif.Memory[addr + 0]:X2} {vif.Memory[addr + 1]:X2} {vif.Memory[addr + 2]:X2} {vif.Memory[addr + 3]:X2} " +
                                $"{vif.Memory[addr + 4]:X2} {vif.Memory[addr + 5]:X2} {vif.Memory[addr + 6]:X2} {vif.Memory[addr + 7]:X2} " +
                                $"{vif.Memory[addr + 8]:X2} {vif.Memory[addr + 9]:X2} {vif.Memory[addr + 10]:X2} {vif.Memory[addr + 11]:X2} " +
                                $"{vif.Memory[addr + 12]:X2} {vif.Memory[addr + 13]:X2} {vif.Memory[addr + 14]:X2} {vif.Memory[addr + 15]:X2}");
                        }
                        throw new Exception($"Header fault expected: [XX 80 0000 4000 3E 30 00000412 XXXX 0000] but was [{header.Vert_count:X2} {header.Unk_01:X2} {header.Unk_04:X2} {header.Unk_06:X} {(byte)header.PrimativeType:X} {header.Unk_08:X8} {(ushort)header.Format:X4} {header.Unk_0E:X4}]");
                    }

                    if (header.PrimativeType != Primative.PRIMATIVE_TRIANGLE_STRIP
                        && header.PrimativeType != Primative.PRIMATIVE_TRIANGLE_STRIP_TWO_SIDED)
                        throw new NotImplementedException($"Encountered unknown Primative Type {(byte)header.PrimativeType:X}");

                    strip.PrimativeType = header.PrimativeType;

                    switch (header.Format)
                    {
                        case MeshFormat.FORMAT_STATIC_EVEN:
                        case MeshFormat.FORMAT_SKELE_EVEN:
                            strip.isEvenWiding = true;
                            break;
                        case MeshFormat.FORMAT_STATIC_ODD:
                        case MeshFormat.FORMAT_SKELE_ODD:
                            strip.isEvenWiding = false;
                            break;
                        default:
                            throw new NotImplementedException($"Unexpected format {(ushort)header.Format:X}");
                    }

                    for (int i = 0; i < header.Vert_count; i++)
                    {
                        Vertex vertex = new Vertex();
                        switch (header.Format)
                        {
                            case MeshFormat.FORMAT_STATIC_EVEN:
                            case MeshFormat.FORMAT_STATIC_ODD:
                                {
                                    float x = bs.ReadSingle(); float y = bs.ReadSingle(); float z = bs.ReadSingle(); _ = bs.ReadSingle();
                                    ushort u = (ushort)bs.ReadUInt32(); ushort v = (ushort)bs.ReadUInt32(); _ = bs.ReadUInt32(); _ = bs.ReadUInt32();
                                    byte r = (byte)bs.ReadUInt32(); byte g = (byte)bs.ReadUInt32(); byte b = (byte)bs.ReadUInt32(); byte a = (byte)bs.ReadUInt32();
                                    vertex.Position = new Vector3(x, y, z);
                                    vertex.UV = new Vector2((float)u / ushort.MaxValue, (float)v / ushort.MaxValue);
                                    // TODO: proper alpha handling
                                    vertex.Color = new Vector4((float)r / byte.MaxValue, (float)g / byte.MaxValue, (float)b / byte.MaxValue, (float)a / byte.MaxValue);
                                }
                                break;
                            case MeshFormat.FORMAT_SKELE_EVEN:
                            case MeshFormat.FORMAT_SKELE_ODD:
                                throw new NotImplementedException($"Not written this one yet...");
                            default:
                                throw new NotImplementedException($"Unexpected format {(ushort)header.Format:X}");
                        }
                        strip.Verts.Add(vertex);
                    }

                    strips.Add(strip);
                }
            }
            while (!done);

            //if (strips.Count != chunk.strip_count
            //    || strips.Sum(s => s.TriCount) != chunk.tri_count)
            //    throw new Exception($"PANIC! Wrong counts expected/actual strips/tris {chunk.strip_count}/{chunk.tri_count} : {strips.Count}/{strips.Sum(s => s.TriCount)}");

            return strips;
        }
    }
}
