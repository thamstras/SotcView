//using SBuf = System.Tuple<byte[], int>;

using System.Text;

namespace Dormin
{
    namespace xff
    {
        public class offsets
        {
            public int off1;
            public int symstrpos;
            public int symstr;
            public int sec;
            public int sym;
            public int off2;
            public int secstrpos;
            public int secstr;
        }

        public class section
        {
            public required string name;
            public int len;
            public int off;
        }

        public class xff
        {
            public int size;
            public int entry;
            public required offsets offsets;
            public required section[] sections;
        }

        public static class func
        {

            //public static SBuf sbufplus(SBuf sbuf, int pos) => Tuple.Create(sbuf.Item1, sbuf.Item2 + pos);

            //public static int sbufpos(SBuf sbuf) => sbuf.Item2;

            //public static void sbufblit(SBuf sbuf, int src_pos, byte[] dst, int dst_pos, int len)
            //{
            //    (var src, var pos) = sbuf;
            //    src_pos = src_pos + pos;
            //    Array.Copy(src, src_pos, dst, dst_pos, len);
            //}

            //public static void sbuferr(SBuf sbuf, int pos, string msg)
            //{
            //    var p = sbufpos(sbuf);
            //    var s = $"{p:08x}({p + pos:08x}): {msg}";
            //    throw new ApplicationException(s);
            //}

            public static UInt32 r32(Span<byte> b, int pos)
            {
                return BitConverter.ToUInt32(b[pos..]);
            }

            public static UInt16 r16(Span<byte> b, int pos)
            {
                return BitConverter.ToUInt16(b[pos..]);
            }

            public static byte r8(Span<byte> b, int pos)
            {
                return b[pos];
            }

            public static Int32 rint(Span<byte> b, int pos)
            {
                return BitConverter.ToInt32(b[pos..]);
            }

            public static float rfloat(Span<byte> b, int pos)
            {
                return BitConverter.ToSingle(b[pos..]);
            }

            public static string rcstrtabent(byte[] b, int pos, int at)
            {
                var begpos = pos + at;
                var endpos = Array.FindIndex(b, begpos, v => v == '\0');
                var len = endpos - begpos;
                Span<byte> s = b.AsSpan(begpos, len);
                return Encoding.ASCII.GetString(s);
            }

            public static offsets roffsets(byte[] b, int pos)
            {
                int ri(int n) => rint(b, pos + n * 4);
                return new offsets
                {
                    off1 = ri(0),
                    symstrpos = ri(1),
                    symstr = ri(2),
                    sec = ri(3),
                    sym = ri(4),
                    off2 = ri(5),
                    secstrpos = ri(6),
                    secstr = ri(7)
                };
            }

            public static int[] rstrpos(byte[] b, int pos, int count)
            {
                int r(int n) => rint(b, pos + n * 4);
                return Enumerable.Range(0, count).Select(r).ToArray();
            }

            public static section rsection(byte[] b, offsets offs, int[] secstrpos, int index)
            {
                var secpos = offs.sec + index * 8 * 4;
                var len = rint(b, secpos + 2 * 4);
                var off = rint(b, secpos + 7 * 4);
                var name = rcstrtabent(b, offs.secstr, secstrpos[index]);
                return new section
                {
                    name = name,
                    len = len,
                    off = off
                };
            }

            public static (xff, byte[]) rxff(BinaryReader br)
            {
                byte[] buf = br.ReadBytes(0x50);
                if (buf[0] != 'x' || buf[1] != 'f' || buf[2] != 'f' ||
                    !(buf[3] == '\0' || buf[3] == '2'))
                {
                    throw new ApplicationException("Not an xff");
                }

                Int32 size = rint(buf, 5*4);
                Int32 entry = rint(buf, 19*4);
                Int32 seccount = rint(buf, 16*4);

                Array.Resize(ref buf, size);
                br.Read(buf, 0x50, size - 0x50);

                var offsets = roffsets(buf, 0x50);
                var secstrpos = rstrpos(buf, offsets.secstrpos, seccount);
                var sections = Enumerable.Range(0, seccount).Select(n => rsection(buf, offsets, secstrpos, n)).ToArray();
                var xff = new xff
                {
                    size = size,
                    entry = entry,
                    offsets = offsets,
                    sections = sections
                };
                return (xff, buf);
            }
        }
    }
}
