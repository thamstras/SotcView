using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoLib
{
    internal static class ReaderExtensions
    {
        public static void Seek(this BinaryReader reader, long offset, SeekOrigin origin = SeekOrigin.Begin)
        {
            reader.BaseStream.Seek(offset, origin);
        }

        public static string ReadCString(this BinaryReader reader)
        {
            return reader.ReadCString(Encoding.ASCII);
        }

        public static string ReadCString(this BinaryReader reader, Encoding enc)
        {
            List<byte> buf = [];
            byte c = 0;
            do
            {
                c = reader.ReadByte();
                if (c != '\0') buf.Add(c);
            }
            while (c != 0);
            return enc.GetString(buf.ToArray());
        }
    }
}
