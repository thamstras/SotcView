using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dormin
{
    namespace nmo
    {
        public class subhdr
        {
            public int off;
            public int count;
        }

        public class surf
        {
            public int tricount;
            public int strcount;
            public string name;
            public (byte[], float, uint, uint)[] hdr1;
            public (uint, float, float, uint[]) hdr2;
            public uint[] hdr3;
        }

        public class surf1
        {
            public int size;
            public int surf;
            public int offs;
            public int tri_count;
            public int strip_count;
        }

        public class tex
        {
            public string texname;
            public (int, int, string)[] nto;
            public int[] int5;
            public int[] half1;
            public int w;
            public int h;
        }

        public class geom
        {
            public float[] vertexa;
            public (float, float, float, int)[] skin;
            public float[] normala;
            public float[] uva;
            public byte[] colora;
            public (int[], surf, uint)[] surfaces;
        }

        public static class func
        {
            public static surf1 rsurf1(Memory<byte> sbuf)
            {
                return new surf1
                {
                    size = xff.func.rint(sbuf.Span, 0),
                    surf = xff.func.rint(sbuf.Span, 8),
                    offs = xff.func.rint(sbuf.Span, 16),
                    tri_count = xff.func.rint(sbuf.Span, 20),
                    strip_count = xff.func.rint(sbuf.Span, 24),
                };
            }

            public static float mag(float i, float j, float k, float s) => i * i + j * j + k * k + s * s;

            public static int verts_in_surf1(surf1 surf1, Memory<byte> sbuf)
            {
                sbuf = sbuf.Slice(surf1.offs);
                var r8 = (int p) => xff.func.r8(sbuf.Span, p);
                Func<int, int, int> r = null, r1 = null;
                r1 = (int verts, int pos) =>
                {
                    var a = r8(pos);
                    var b = r8(pos + 1);
                    var c = r8(pos + 2);
                    var d = r8(pos + 3);
                    pos += 4;
                    var skip = (int pos) => r(pos, verts);
                    return d switch
                    {
                        0x05 when c == 0 => skip(pos),
                        0x17 when c == 0 => skip(pos),
                        0x65 => r(verts, pos + 4 * c),
                        0x68 when a == 1 => r(verts + c, pos + 12 * c),
                        0x68 => skip(pos + 12 * c),
                        0x6c => skip(pos + 16 * c),
                        0x6d => skip(pos + 8 * c),
                        0x6e => skip(pos + 4 * c),
                        0x00 when a == 0 && b == 0 && c == 0 => verts,
                        _ => throw new ApplicationException($"geom (a={a} b={b} c={c} d={d})"),
                    };
                };
                r = (int verts, int pos) =>
                {
                    if (pos == surf1.size) return verts;
                    else return r1(verts, pos);
                };
                return r(0, 12);
            }

            private static void app(byte count, int pos, int index, int pos_incr, int index_incr, Action<int, int> f)
            {

                Action<int, int, int> g = null;
                g = (int p, int i, int count) =>
                {
                    if (count == 0) return;
                    else
                    {
                        f(p, i);
                        g(p + pos_incr, i + index_incr, count - 1);
                    }
                };
                g(pos, index * index_incr, count);
            }

            public static (int, int[]) rgeom1(int start_index, surf1 surf1, geom geom, Memory<byte> sbuf)
            {
                sbuf = sbuf.Slice(surf1.offs);
                var r8 = (int p) => xff.func.r8(sbuf.Span, p);
                var r16s = (int p) => xff.func.r16s(sbuf.Span, p);
                var r16 = (int p) => xff.func.r16(sbuf.Span, p);
                var rfloat = (int p) => xff.func.rfloat(sbuf.Span, p);
                Func<int[], int, int, int, (int, int[])> r = null;
                Func<int[], int, int, int, (int, int[])> r1 = null;
                r1 = (int[] counts, int index, int prev_count, int pos) =>
                {
                    var a = r8(pos);        // IMMEDIATE LOW
                    var b = r8(pos + 1);    // IMMEDIATE HIGH
                    var c = r8(pos + 2);    // NUM
                    var d = r8(pos + 3);    // CMD
                    pos += 4;
                    var skip = (int pos) => r(counts, index, prev_count, pos);
                    var skip2 = (int n) => skip(pos + c * n);
                    (int, int[]) ret;
                    switch (d)
                    {
                        // STMOD
                        case 0x5 when c == 0: ret = skip(pos); break;
                        // MSCNT
                        case 0x17 when c == 0: ret = skip(pos); break;
                        // UNPACK V2-16
                        case 0x65:
                            {
                                app(c, pos, index, 4, 2, (int pos, int index) =>
                                {
                                    var u = r16s(pos + 0);
                                    var v = r16s(pos + 2);
                                    var uf = u / 4096.0f;
                                    var uv = v / 4096.0f;
                                    geom.uva[index + 0] = uf;
                                    geom.uva[index + 1] = uv;
                                });
                                ret = skip2(4);
                            }
                            break;
                        // UNPACK V3-32
                        case 0x68 when a == 1:
                            {
                                index = index + prev_count;
                                app(c, pos, index, 12, 3, (int pi, int vi) =>
                                {
                                    geom.vertexa[vi + 0] = rfloat(pi + 0);
                                    geom.vertexa[vi + 1] = rfloat(pi + 4);
                                    geom.vertexa[vi + 2] = rfloat(pi + 8);
                                });
                                ret = r(counts.Prepend(c).ToArray(), index, c, pos + c * 12);
                            }
                            break;
                        case 0x68 when a == 2:
                            {
                                app(c, pos, index, 12, 3, (int pi, int vi) =>
                                {
                                    geom.vertexa[vi + 0] = rfloat(pi + 0);
                                    geom.vertexa[vi + 1] = rfloat(pi + 4);
                                    geom.vertexa[vi + 2] = rfloat(pi + 8);
                                });
                                ret = skip2(12);
                            }
                            break;
                        case 0x68 when a == 6:
                            {
                                // no-op app call omitted
                                ret = skip2(12);
                            }
                            break;
                        // UNPACK V4-32
                        case 0x6c when a == 0:
                            ret = skip(pos + 16 * c);
                            break;
                        case 0x6c:
                            {
                                app(c, pos, index, 16, 1, (int pi, int index) =>
                                {
                                    var a = rfloat(pi + 0);
                                    var b = rfloat(pi + 4);
                                    var c = rfloat(pi + 8);
                                    var d = xff.func.rint(sbuf.Span, pi + 12);
                                    geom.skin[index] = (a, b, c, d);
                                });
                                ret = skip2(16);
                            }
                            break;
                        // UNPACK v4-16
                        case 0x6d when a == 2:
                            {
                                app(c, pos, index, 8, 3, (int pi, int vi) =>
                                {
                                    var x = r16s(pi + 0);
                                    var y = r16s(pi + 2);
                                    var z = r16s(pi + 4);
                                    geom.normala[vi + 0] = (float)x / 4096.0f;
                                    geom.normala[vi + 1] = (float)y / 4096.0f;
                                    geom.normala[vi + 2] = (float)z / 4096.0f;
                                });
                                ret = skip2(8);
                            }
                            break;
                        case 0x6d when a == 3:
                            {
                                // no-op app call omitted;
                                ret = skip2(8);
                            }
                            break;
                        case 0x6d:
                            ret = skip2(8);
                            break;
                        // UNPACK v4-8
                        case 0x6e:
                            xff.func.sbufblt(sbuf.Span, dst: geom.colora, src_pos: pos, dst_pos: index * 4, len: c * 4);
                            ret = skip2(4);
                            break;
                        case 0 when a == 0 && b == 0 && c == 0:
                            ret = (index + prev_count, counts);
                            break;
                        default: throw new ApplicationException($"geom (a={a} b={b} c={c} d={d})");
                    }
                    return ret;
                };
                r = (int[] counts, int index, int prev_count, int pos) =>
                {
                    if (pos == surf1.size)
                        return (index + prev_count, counts);
                    else
                        return r1(counts, index, prev_count, pos);
                };
                return r([], start_index, 0, 12);
            }

            public static tex rtext(int n, Memory<byte> sectbuf, Memory<byte> sbuf)
            {
                var sig = Encoding.ASCII.GetBytes("TEX\0");
                if (!sbuf[0..sig.Length].ToArray().SequenceEqual(sig))
                    throw new Exception("invalid tex signature");
                var int5 = Enumerable.Range(0, 5).Select(n => Dormin.xff.func.r32(sbuf.Span, 4 + n * 4)).ToArray();
                var half2_1 = Enumerable.Range(0, 2).Select(n => Dormin.xff.func.r32(sbuf.Span, 24 + n * 2)).ToArray();
                var w = Dormin.xff.func.r16(sbuf.Span, 28);
                var h = Dormin.xff.func.r16(sbuf.Span, 30);
                var nameoff = int5[0];
                var name = Dormin.xff.func.rcstrtabent(sectbuf.Span, nameoff, 0);
                //var nto TODO find and load the texture from "${name}.nto"
                return new tex
                {
                    texname = name,
                    //nto = nto,
                    int5 = int5,
                    half1 = half2_1,
                    w = w,
                    h = h
                };
            }

            public static surf rsrf(int n, Memory<byte> sectbuf, Memory<byte> sbuf)
            {
                var sig = Encoding.ASCII.GetBytes("SRF\0");
                if (!sbuf[0..sig.Length].ToArray().SequenceEqual(sig))
                    throw new Exception("invalid srf signature");
                var tricount = Dormin.xff.func.rint(sbuf.Span, 4);
                var stripcount = Dormin.xff.func.rint(sbuf.Span, 8);
                var nameoff = Dormin.xff.func.rint(sbuf.Span, 12);
                var hdr1 = Enumerable.Range(0, 3).Select(n1 =>
                {
                    var subbuf = sbuf.Slice(16 + n1 * 16);
                    var zero = Enumerable.Range(0, 4).Select(n => xff.func.r8(subbuf.Span, n1)).ToArray();
                    var one = xff.func.rfloat(subbuf.Span, 4);
                    var two = xff.func.r32(subbuf.Span, 8);
                    var three = xff.func.r32(subbuf.Span, 12);
                    return (zero, one, two, three);
                }).ToArray();
                var hdr2f = () =>
                {
                    var subbuf = sbuf.Slice(16 + 3 * 16);
                    var zero = xff.func.r32(subbuf.Span, 0);
                    var one = xff.func.rfloat(subbuf.Span, 4);
                    var two = xff.func.rfloat(subbuf.Span, 8);
                    var three = Enumerable.Range(0, 5).Select(n1 => xff.func.r32(subbuf.Span, 12 + (n1 * 4))).ToArray();
                    return (zero, one, two, three);
                };
                var hdr2 = hdr2f();
                var hdr3f = () =>
                {
                    var subbuf = sbuf.Slice(16 + 3 * 16 + 32);
                    return Enumerable.Range(0, 48).Select(n => xff.func.r32(subbuf.Span, n * 4)).ToArray();
                };
                var hdr3 = hdr3f();
                var name = xff.func.rcstrtabent(sectbuf, nameoff, 0);
                return new surf
                {
                    tricount = tricount,
                    strcount = stripcount,
                    name = name,
                    hdr1 = hdr1,
                    hdr2 = hdr2,
                    hdr3 = hdr3
                };
            }

            public static geom r(xff.xff xff, byte[] buf)
            {
                // TODO: I think we can push Memory back into rxff...
                var xffbuf = new Memory<byte>(buf);
                if (xff.sections.Length != 2)
                    throw new ApplicationException("number of xff sections is not 2");

                var sectpos = xff.sections[1].off;
                var sectbuf = xffbuf.Slice(sectpos);
                var nmobuf = sectbuf.Slice(xff.entry);
                var sig = Encoding.ASCII.GetBytes("NMO\0");
                if (!nmobuf[0..sig.Length].ToArray().SequenceEqual(sig))
                    throw new ApplicationException("invalid nmo signature");

                var hdrs = Enumerable.Range(0, 5).Select(n =>
                {
                    var pos = 0x30 + n * 16;
                    var off = Dormin.xff.func.rint(nmobuf.Span, pos);
                    var count = Dormin.xff.func.rint(nmobuf.Span, pos + 4);
                    return new subhdr
                    {
                        off = off,
                        count = count,
                    };
                }).ToArray();

                var texts = Enumerable.Range(0, hdrs[1].count).Select(n =>
                {
                    var sbuf = sectbuf.Slice(hdrs[1].off + n * 32);
                    return rtext(n, sectbuf, sbuf);
                }).ToArray();

                var surfs = Enumerable.Range(0, hdrs[2].count).Select(n =>
                {
                    var sbuf = sectbuf.Slice(hdrs[1].off + n * 288);
                    return rsrf(n, sectbuf, sbuf);
                }).ToArray();

                // TODO: this recursive function is just a loop?
                Func<int, int, int> calc = null;
                calc = (int num_verts, int n) =>
                {
                    if (n == hdrs[3].count) return num_verts;
                    var subbuf = sectbuf.Slice(hdrs[3].off + n * 32);
                    var surf1 = rsurf1(subbuf);
                    var here_verts = verts_in_surf1(surf1, sectbuf);
                    return calc(num_verts + here_verts, n + 1);
                };
                var num_verts = calc(0, 0);

                // TODO: Also we probably shouldn't read all the surf1s twice...
                var surf1s = Enumerable.Range(0, hdrs[3].count).Select(n => rsurf1(sectbuf.Slice(hdrs[3].off + n * 32))).ToArray();

                geom geom = new geom
                {
                    vertexa = new float[num_verts * 3],
                    normala = new float[num_verts * 3],
                    colora = new byte[num_verts * 4],
                    uva = new float[num_verts * 2],
                    skin = new (float, float, float, int)[num_verts]
                };
                var surfaces = surf1s.Aggregate((0, new List<(int[], surf, uint)>()), ((int last_index, List<(int[], surf, uint)> countss) a, surf1 surf1) =>
                {
                    (var index, var counts) = rgeom1(a.last_index, surf1, geom, sectbuf);
                    var surf = surfs[surf1.surf];
                    (_, _, _, var texindex) = surf.hdr1[1];
                    var text = texts[texindex];
                    a.countss.Add((counts.Reverse().ToArray(), surf, texindex));
                    return (index, a.countss);
                });
                geom.surfaces = surfaces.Item2.ToArray();
                return geom;
            }

            public static void rnmo(BinaryReader reader)
            {
                (var x, var buf) = xff.func.rxff(reader);
                r(x, buf);
            }
        }
    }
}
