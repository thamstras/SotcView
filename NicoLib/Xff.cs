using System;
using System.Text;
using Xe.BinaryMapper;

namespace NicoLib
{
    /// <summary>
    /// XFF Module thing. Team ICO's personal DLL Hell.
    /// </summary>
    /// <remarks>
    /// This is a prototype class that mainly just reads the stuff off of disk.
    /// In future this won't expose everything raw (certainly not the entire header with all it's offsets that are only relevent to this class' reader)
    /// </remarks>
    public class Xff
    {
        public class FileHeader
        {
            [Data] public UInt32 Ident { get; set;}
            [Data] public UInt32 Padding04 { get; set;}
            [Data] public UInt32 Padding08 { get; set; }
            [Data] public UInt32 Count0C { get; set; }
            [Data] public UInt32 Unk10 { get; set; }
            [Data] public UInt32 Xff_size { get; set; }
            [Data] public UInt32 Unk_18 { get; set; }
            [Data] public UInt32 Count_1c { get; set; }
            [Data] public UInt32 Unk_20 { get; set; }
            [Data] public UInt32 SymCount { get; set; }
            [Data] public UInt32 Unk_28 { get; set; }
            [Data] public UInt32 Unk_2c { get; set; }
            [Data] public UInt32 Unk_30 { get; set; }
            [Data] public UInt32 Unk_34 { get; set; }
            [Data] public UInt32 Count_38 { get; set; }
            [Data] public UInt32 Unk_3c { get; set; }
            [Data] public UInt32 SecCount { get; set; }
            [Data] public UInt32 Unk_44 { get; set; }
            [Data] public UInt32 Unk_48 { get; set; }
            [Data] public UInt32 Off_sign { get; set; } // Entry Point
            [Data] public UInt32 Off_50_count_1c { get; set; }
            [Data] public UInt32 Off_symbols1 { get; set; }
            [Data] public UInt32 Off_symstrtab { get; set; }
            [Data] public UInt32 Off_sections { get; set; }
            [Data] public UInt32 Off_symbols2 { get; set; }
            [Data] public UInt32 Off_64_count_38 { get; set; }
            [Data] public UInt32 Off_secnameoffs { get; set; }
            [Data] public UInt32 Off_secstrtab { get; set; }
        }

        public class SymbolDef
        {
            [Data] public UInt32 SymNameStrOffset { get; set; }
            [Data] public UInt32 Unk_04 { get; set; }
            [Data] public UInt32 Unk_08 { get; set; }
            [Data] public UInt32 Unk_0C { get; set; }

            public uint Symbol2 { get; set; }
            public string Name { get; set; }
        }

        public class SectionDef
        {
            [Data] public UInt32 Unk00 { get; set; }
            [Data] public UInt32 Unk04 { get; set; }
            [Data] public UInt32 Length { get; set; }
            [Data] public UInt32 Unk0C { get; set; }
            [Data] public UInt32 Unk10 { get; set; }
            [Data] public UInt32 Unk14 { get; set; }
            [Data] public UInt32 Unk18 { get; set; }
            [Data] public UInt32 Offset { get; set; }

            public string Name { get; set; }
            public byte[] Data { get; set; }
        }

        public FileHeader Header { get; set; }
        public List<SectionDef> Sections { get; set; }
        public List<SymbolDef> Symbols { get; set; }

        public Xff()
        {
            Header = new FileHeader();
            Header.Ident = 0x00666678;
            Sections = new List<SectionDef>();
            Symbols = new List<SymbolDef>();
        }

        private Xff(FileHeader header, List<SectionDef> sections, List<SymbolDef> symbols)
        {
            Header = header;
            Sections = sections;
            Symbols = symbols;
        }

        public static Xff Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);

            FileHeader header = BinaryMapping.ReadObject<FileHeader>(stream);
            
            reader.Seek(header.Off_secnameoffs);
            List<int> section_name_offsets = new List<int>();
            for (int i = 0; i < header.SecCount; i++) section_name_offsets.Add(reader.ReadInt32());

            reader.Seek(header.Off_sections);
            List<SectionDef> sections = new List<SectionDef>();
            for (int i = 0; i < header.SecCount; i++) sections.Add(BinaryMapping.ReadObject<SectionDef>(stream));
            for (int i = 0; i < header.SecCount; i++)
            {
                reader.Seek(header.Off_secstrtab + section_name_offsets[i]);
                sections[i].Name = reader.ReadCString();
            }

            reader.Seek(header.Off_symbols2);
            List<int> symbols2 = new List<int>();
            for (int i = 0; i < header.SymCount; i++) symbols2.Add(reader.ReadInt32());

            // FSeek(header.off_64_count_38);
            // THING hdr5[header.count_38];

            //reader.Seek(header.Off_50_count_1c);
            //List<int> data_count1c = new List<int>();
            //for (int i = 0; i < header.Count_1c; i++) data_count1c.Add(reader.ReadInt32());

            reader.Seek(header.Off_symbols1);
            List<SymbolDef> symbols1 = new List<SymbolDef>();
            for (int i = 0; i < header.SymCount; i++) symbols1.Add(BinaryMapping.ReadObject<SymbolDef>(stream));
            foreach (SymbolDef symbol in symbols1)
            {
                reader.Seek(header.Off_symstrtab + symbol.SymNameStrOffset);
                symbol.Name = reader.ReadCString();
            }

            foreach (SectionDef section in sections)
            {
                if (section.Offset == 0 || section.Length == 0)
                    continue;

                reader.Seek(section.Offset);
                section.Data = reader.ReadBytes((int)section.Length);
            }

            return new Xff(header, sections, symbols1);
        }
    }
}
