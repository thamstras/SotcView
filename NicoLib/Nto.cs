using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xe.BinaryMapper;

namespace NicoLib
{
    public class Nto
    {
        // TODO: Should probably move to storing the original header/pixels/pal in here and leave the decoding to the image conversion
        //       No rush though, doesn't look like there's much interesting in the rest of the header...
        public Nto(string name, int width, int height, byte[] pixelData)
        {
            Name = name;
            Width = width;
            Height = height;
            PixelData = pixelData;
        }

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public byte[] PixelData { get; }

        public class Header
        {
            [Data] public UInt32 Nto_sig { get; set; }
            [Data] public UInt32 Version { get; set; }
            [Data] public UInt32 Unk_08 { get; set; }
            [Data] public UInt32 Unk_0C { get; set; }
            [Data] public UInt32 Unk_10 { get; set; }
            [Data] public UInt32 PixelsOffset { get; set; }
            [Data] public UInt32 PalleteOffset { get; set; }
            [Data] public byte Kind { get; set; }
            [Data] public byte Mipmaps { get; set; }
            [Data] public byte WH { get; set; }
            [Data] public byte Swizz { get; set; }
            [Data(Count = 36)] public UInt32[] Extra { get; set; }
        }

        enum PixelMode
        {
            RGBA_32 = 0,
            CLUT_8 = 0x13,
            CLUT_4 = 0x14
        }

        private static PixelMode GetPixelMode(byte kind)
        {
            return kind switch
            {
                0 => PixelMode.RGBA_32,
                0x13 => PixelMode.CLUT_8,
                0x14 => PixelMode.CLUT_4,
                _ => throw new Exception($"Out of range pixel kind {kind:X}"),
            };
        }

        private static int CalcPixelDataSize(int width, int height, PixelMode mode)
        {
            int count = width * height;
            switch (mode)
            {
                case PixelMode.RGBA_32:
                    count *= 4;
                    break;
                case PixelMode.CLUT_8:
                    count *= 1;
                    break;
                case PixelMode.CLUT_4:
                    count /= 2;
                    break;
            }
            return count;
        }

        private static int CalcPalDataSize(PixelMode mode)
        {
            return mode switch
            {
                PixelMode.CLUT_8 => 255 * 4,
                PixelMode.CLUT_4 => 16 * 4,
                _ => 0,
            };
        }

        public static Nto FromXff(Xff xff)
        {
            Xff.SectionDef section = xff.Sections.FirstOrDefault(s => s.Name == ".rodata") ?? throw new ApplicationException("NTO .rodata section not found!");

            using MemoryStream stream = new MemoryStream(section.Data, false);
            using BinaryReader reader = new BinaryReader(stream);
            reader.Seek(xff.Header.Off_sign);

            Header header = BinaryMapping.ReadObject<Header>(stream);

            //var mipCount = header.Mipmaps >> 4;
            var WH = header.WH;
            var height = 1 << (WH & 0xF);
            var width = 1 << (WH >> 4);
            PixelMode pixelMode = GetPixelMode(header.Kind);
            int pixelByteCount = CalcPixelDataSize(width, height, pixelMode);
            int palByteCount = CalcPalDataSize(pixelMode);
            bool swizzled = header.Swizz != 0;

            reader.Seek(header.PixelsOffset);
            byte[] encodedPixels = reader.ReadBytes(pixelByteCount);

            byte[] pal;
            if (palByteCount > 0)
            {
                reader.Seek(header.PalleteOffset, SeekOrigin.Begin);
                pal = reader.ReadBytes(palByteCount);
            }
            else
            {
                pal = [];
            }

            byte[] pixelData = DecodePixels(encodedPixels, pal, width, height, pixelMode, swizzled);

            string name = reader.ReadCString();

            return new Nto(name, width, height, pixelData);
        }

        // TODO: I think I can get rid of the swizzle stuff if I UnTile the bytes before decode.
        private static byte[] DecodePixels(byte[] encodedPixels, byte[] pal, int width, int height, PixelMode pixelMode, bool swizzled)
        {
            byte[] result = new byte[width * height * 4];

            void CopyPal(byte palIdx, int dst)
            {
                var max = pixelMode == PixelMode.CLUT_8 ? 255 : 16;
                ArgumentOutOfRangeException.ThrowIfGreaterThan(palIdx, max);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(dst, result.Length);

                result[dst * 4 + 0] = pal[palIdx * 4 + 0];
                result[dst * 4 + 1] = pal[palIdx * 4 + 1];
                result[dst * 4 + 2] = pal[palIdx * 4 + 2];
                result[dst * 4 + 3] = pal[palIdx * 4 + 3];
            }

            switch (pixelMode)
            {
                case PixelMode.RGBA_32:
                    {
                        Array.Copy(encodedPixels, result, encodedPixels.Length);
                    }
                    break;
                case PixelMode.CLUT_8:
                    {
                        if (!swizzled)
                        {
                            for (int i = 0; i < width * height; i++)
                            {
                                byte idx = encodedPixels[i];
                                CopyPal(idx, i);
                            }
                        }
                        else
                        {
                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    int block_location = (y & (~0xf)) * width + (x & (~0xf)) * 2;
                                    int swap_selector = (((y + 2) >> 2) & 0x1) * 4;
                                    int posY = (((y & (~3)) >> 1) + (y & 1)) & 0x7;
                                    int column_location = posY * width * 2 + ((x + swap_selector) & 0x7) * 4;
                                    int byte_num = ((y >> 1) & 1) + ((x >> 2) & 2);
                                    byte idx = encodedPixels[block_location + column_location + byte_num];
                                    CopyPal(idx, y * width + x);
                                }
                            }
                        }
                    }
                    break;
                case PixelMode.CLUT_4:
                    {
                        if (!swizzled)
                        {
                            for (int i = 0; i < (width * height) / 2; i++)
                            {
                                byte d = encodedPixels[i];
                                byte hi = (byte)(d >> 4);
                                byte lo = (byte)(d & 0xf);
                                CopyPal(hi, i * 2);
                                CopyPal(lo, i * 2 + 1);
                            }
                        }
                        else
                        {
                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    int pageX = x & (~0x7f);
                                    int pageY = y & (~0x7f);

                                    int pages_horz = (width + 127) / 128;
                                    int pages_vert = (height + 127) / 128;

                                    int page_number = (pageY / 128) * pages_horz + (pageX / 128);

                                    int page32Y = (page_number / pages_vert) * 32;
                                    int page32X = (page_number % pages_vert) * 64;

                                    int page_location = page32Y * height * 2 + page32X * 4;

                                    int locX = x & 0x7f;
                                    int locY = y & 0x7f;

                                    int block_location = ((locX & (~0x1f)) >> 1) * height + (locY & (~0xf)) * 2;
                                    int swap_selector = (((y + 2) >> 2) & 0x1) * 4;
                                    int posY = (((y & (~3)) >> 1) + (y & 1)) & 0x7;

                                    int column_location = posY * height * 2 + ((x + swap_selector) & 0x7) * 4;

                                    int byte_num = (x >> 3) & 3; /* 0,1,2,3 */
                                    int bits_set = (y >> 1) & 1; /* 0,1            (lower/upper 4 bits) */

                                    byte idx = encodedPixels[page_location + block_location + column_location + byte_num];
                                    idx = (byte)((idx >> (bits_set * 4)) & 0xf);
                                    CopyPal(idx, y * width + x);
                                }
                            }
                        }
                    }
                    break;
            }

            return result;
        }
    }
}
