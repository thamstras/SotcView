// NOTE: The code in this file is lifted (with modifications) from the OpenKH project.


namespace NicoLib.PS2
{
    /// <summary>
    /// EE_Users_Manual
    /// 6.4 VIFcode Reference
    /// </summary>
    public class VifProcessor
    {
        public enum State
        {
            End,
            Run,
            Interrupt,
            Microprogram,
        }

        public enum MaskType
        {
            Write,
            Row,
            Col,
            Skip
        }

        private struct VIFn_Cycle
        {
            public readonly byte CL;
            public readonly byte WL;

            public bool IsSkippingWrite => CL >= WL;
            public bool IsFillingWrite => CL < WL;

            public VIFn_Cycle(ushort immediate)
            {
                CL = (byte)(immediate & 0xFF);
                WL = (byte)((immediate >> 8) & 0xFF);
            }
        }

        private struct VIFn_Mask
        {
            public VIFn_Mask(uint value)
            {
                Masks = new Vector4Mask[]
                {
                    new Vector4Mask((value >> 0) & 0xff),
                    new Vector4Mask((value >> 8) & 0xff),
                    new Vector4Mask((value >> 16) & 0xff),
                    new Vector4Mask((value >> 24) & 0xff),
                };
            }

            public Vector4Mask[] Masks { get; }
        }

        private struct Vector4Mask
        {
            public Vector4Mask(uint value)
            {
                Mask = (byte)value;
            }

            public byte Mask { get; }

            public MaskType X => (MaskType)((Mask >> 0) & 3);
            public MaskType Y => (MaskType)((Mask >> 2) & 3);
            public MaskType Z => (MaskType)((Mask >> 4) & 3);
            public MaskType W => (MaskType)((Mask >> 6) & 3);
        }

        private struct Opcode
        {
            private readonly uint _opcode;

            public ushort Immediate => (ushort)(_opcode & 0xffff);
            public byte Num => (byte)((_opcode >> 16) & 0xff);
            public byte Cmd => (byte)((_opcode >> 24) & 0x7f);
            public bool Interrupt => _opcode >= 0x80000000;

            public bool IsUnpack => (Cmd & CmdMaskUnpack) == CmdMaskUnpack;
            public uint UnpackAddress => _opcode & 0x1ff;
            public bool UnpackIsUnsigned => (_opcode & 0x400) == 0;
            public bool UnpackAddsTops => (_opcode & 0x800) != 0;
            public uint UnpackVl => (_opcode >> 24) & 3;
            public uint UnpackVn => (_opcode >> 26) & 3;
            public bool UnpackMask => ((_opcode >> 28) & 1) != 0;

            public Opcode(uint opcode)
            {
                _opcode = opcode;
            }

            public static Opcode Read(byte[] code, int pc)
            {
                var opcode = (uint)(
                    code[pc + 0] |
                    (code[pc + 1] << 8) |
                    (code[pc + 2] << 16) |
                    (code[pc + 3] << 24));

                return new Opcode(opcode);
            }
        }

        private const int OpcodeAlignment = 0x4;
        private const int VertexAlignment = 0x10;

        // Used to adjust the data alignment in the VIF packet
        private const byte CmdNop = 0b0000000;

        // Writes the value of the immediate to VIFn_CYCLE register
        private const byte CmdStcycl = 0b0000001;

        private const byte CmdOffset = 0b0000010;
        private const byte CmdBase = 0b0000011;
        private const byte CmdITop = 0b0000100;
        private const byte CmdSTMod = 0b0000101;
        private const byte CmdMskPath3 = 0b0000110;
        private const byte CmdMark = 0b0000111;

        private const byte CmdFlushE = 0b0010000;
        private const byte CmdFlush = 0b0010001;
        private const byte CmdFlushA = 0b0010011;

        // Activates the microprogram
        private const byte CmdMscal = 0b0010100;

        // Activates the microprogram
        private const byte CmdMscnt = 0b0010111;

        private const byte CmdMscalf = 0b0010101;

        // Sets the data mask pattern
        private const byte CmdStMask = 0b0100000;

        // Sets the filling data for row registers
        private const byte CmdStRow = 0b0110000;

        // Sets the filling data for column registers
        private const byte CmdStCol = 0b0110001;

        private const byte CmdMpg = 0b1001010;

        private const byte CmdDirect = 0b1010000;
        private const byte CmdDirectHl = 0b1010001;

        // Transfer data to the VU Mem
        private const byte CmdMaskUnpack = 0b1100000;

        private Func<uint>[] _readSigned;
        private Func<uint>[] _readUnsigned;
        private Action<Func<uint>>[] _unpacker;

        private Vector4Mask DefaultMask = new Vector4Mask(0);
        private readonly byte[] _code;
        private readonly byte[] _mem = new byte[16 * 1024];
        private readonly uint[] _vifnCol;
        private readonly uint[] _vifnRow;
        private int _programCounter;
        private VIFn_Cycle _vifnCycle;
        private VIFn_Mask _vifnMask;
        private int _destinationAddress;
        private int _unpackMaskIndex;
        private bool _enableMask;

        public VifProcessor(byte[] code)
        {
            _code = code;
            _programCounter = 0;
            _vifnMask = new VIFn_Mask(0);
            _vifnCol = new uint[4];
            _vifnRow = new uint[4];

            _readSigned = new Func<uint>[]
            {
                    ReadInt32,
                    ReadInt16,
                    ReadInt8,
                    Read45,
            };
            _readUnsigned = new Func<uint>[]
            {
                    ReadUInt32,
                    ReadUInt16,
                    ReadUInt8,
                    Read45
            };
            _unpacker = new Action<Func<uint>>[]
            {
                UnpackSingle,
                UnpackVector2,
                UnpackVector3,
                UnpackVector4,
            };
        }

        public byte[] Memory => _mem;

        public int Vif1_Tops { get; set; }

        private Vector4Mask NextMask()
        {
            if (_enableMask)
                return _vifnMask.Masks[(_unpackMaskIndex++) & 3];

            return DefaultMask;
        }

        public State Run()
        {
            while (true)
            {
                var state = Step();
                if (state == State.End ||
                    state == State.Microprogram)
                    return state;
            }
        }

        private State Step()
        {
            if (_programCounter >= _code.Length)
                return State.End;

            var opcode = new Opcode(ReadUInt32());

            if (opcode.IsUnpack)
            {
                Unpack(opcode);
            }
            else
            {
                switch (opcode.Cmd)
                {
                    case CmdNop:
                        //Console.WriteLine($"{_programCounter - 4:X4} NOP");
                        break;
                    case CmdStcycl:
                        // KH2 is not really using it.. so we are going to to the same.
                        //Console.WriteLine($"{_programCounter - 4:X4} STCYCL {opcode.Immediate:X4}");
                        _vifnCycle = new VIFn_Cycle(opcode.Immediate);
                        break;
                    case CmdOffset:
                        //Console.WriteLine($"{_programCounter - 4:X4} OFFSET {opcode.Immediate:X4}");
                        break;
                    case CmdBase:
                        //Console.WriteLine($"{_programCounter - 4:X4} BASE {opcode.Immediate:X4}");
                        break;
                    case CmdITop:
                        //Console.WriteLine($"{_programCounter - 4:X4} ITOP {opcode.Immediate:X4}");
                        break;
                    case CmdSTMod:
                        //Console.WriteLine($"{_programCounter - 4:X4} STMOD {opcode.Immediate:X4}");
                        break;
                    case CmdMskPath3:
                        //Console.WriteLine($"{_programCounter - 4:X4} MSKPATH3 {opcode.Immediate != 0}");
                        break;
                    case CmdMark:
                        //Console.WriteLine($"{_programCounter - 4:X4} MARK {opcode.Immediate:X4}");
                        break;
                    case CmdFlushE:
                        //Console.WriteLine($"{_programCounter - 4:X4} FLUSHE");
                        break;
                    case CmdFlush:
                        //Console.WriteLine($"{_programCounter - 4:X4} FLUSH");
                        break;
                    case CmdFlushA:
                        //Console.WriteLine($"{_programCounter - 4:X4} FLUSHL");
                        break;
                    case CmdMscal:
                        //Console.WriteLine($"{_programCounter - 4:X4} MSCAL {opcode.Immediate:X4}");
                        // opcode.Immediate needs to be used as execution address for the microprogram.
                        return State.Microprogram;
                    case CmdMscnt:
                        //Console.WriteLine($"{_programCounter - 4:X4} MSCNT");
                        // The difference with Mscal is that the execution address will be the
                        // most recent end of the previous microcode execution.
                        return State.Microprogram;
                    case CmdMscalf:
                        //Console.WriteLine($"{_programCounter - 4:X4} MSCALF {opcode.Immediate:X4}");
                        return State.Microprogram;
                    case CmdStMask:
                        //Console.WriteLine($"{_programCounter - 4:X4} STMASK ");
                        _vifnMask = new VIFn_Mask(ReadUInt32());
                        break;
                    case CmdStRow:
                        //Console.WriteLine($"{_programCounter - 4:X4} STROW ");
                        _vifnRow[0] = ReadUInt32();
                        _vifnRow[1] = ReadUInt32();
                        _vifnRow[2] = ReadUInt32();
                        _vifnRow[3] = ReadUInt32();
                        break;
                    case CmdStCol:
                        //Console.WriteLine($"{_programCounter - 4:X4} STCOL ");
                        _vifnCol[0] = ReadUInt32();
                        _vifnCol[1] = ReadUInt32();
                        _vifnCol[2] = ReadUInt32();
                        _vifnCol[3] = ReadUInt32();
                        break;
                    case CmdMpg:
                        //Console.WriteLine($"{_programCounter - 4:X} MPG {opcode.Num:X2} to {opcode.Immediate:X4}");
                        throw new NotImplementedException();    // This is a little complex and I don't want to write it if I don't have to
                    case CmdDirect:
                        //Console.WriteLine($"{_programCounter - 4:X} DIRECT");
                        throw new NotImplementedException();    // This is a little complex and I don't want to write it if I don't have to
                    case CmdDirectHl:
                        //Console.WriteLine($"{_programCounter - 4:X} DIRECTHL");
                        throw new NotImplementedException();    // This is a little complex and I don't want to write it if I don't have to
                    default:
                        throw new Exception($"VIF1 cmd {opcode.Cmd:X02}@{_programCounter - 4:X4} not implemented!");
                }
            }

            return State.Run;
        }

        private string GetUnpackFormatStr(Opcode opcode)
        {
            string prefix, suffix;
            switch (opcode.UnpackVn)
            {
                case 0: prefix = "S"; break;
                case 1: prefix = "V2"; break;
                case 2: prefix = "V3"; break;
                case 3: prefix = "V4"; break;
                default: throw new Exception($"VIF1 UNPACK INVALID VN: {opcode.UnpackVn}");
            }

            switch (opcode.UnpackVl)
            {
                case 0: suffix = "32"; break;
                case 1: suffix = "16"; break;
                case 2: suffix = "8"; break;
                case 3: suffix = "5"; break;    // NOTE: only valid on hardware when VN == 3;
                default: throw new Exception($"VIF1 UNPACK INVALID VN: {opcode.UnpackVl}");
            }

            return $"{prefix}-{suffix}";
        }

        private void Unpack(Opcode opcode)
        {
            _destinationAddress = (int)opcode.UnpackAddress;
            _destinationAddress *= VertexAlignment;
            var baseWriteAddress = _destinationAddress;
            _unpackMaskIndex = 0;
            _unpackV45Idx = 0;

            var reader = opcode.UnpackIsUnsigned ?
                _readUnsigned[opcode.UnpackVl] :
                _readSigned[opcode.UnpackVl];
            var unpacker = _unpacker[opcode.UnpackVn];
            _enableMask = opcode.UnpackMask;

            //Console.WriteLine($"{_programCounter - 4:X4} UNPACK {(opcode.UnpackMask ? "MASKED" : "UNMASKED")} {GetUnpackFormatStr(opcode)} {(opcode.UnpackAddsTops ? "TOPS" : "BASE")} {(opcode.UnpackIsUnsigned ? "UNSIGNED" : "SIGNED")} {opcode.Num} => {_destinationAddress:X4}");

            for (var i = 0; i < opcode.Num; i++)
            {
                if (_vifnCycle.CL != 1 || _vifnCycle.WL != 1)
                {
                    if (_vifnCycle.IsSkippingWrite)
                    {
                        if (opcode.UnpackAddsTops)
                            _destinationAddress = (int)((opcode.UnpackAddress + Vif1_Tops + _vifnCycle.CL * (i / _vifnCycle.WL) + (i % _vifnCycle.WL)) * 16);
                        else
                            _destinationAddress = (int)((opcode.UnpackAddress + _vifnCycle.CL * (i / _vifnCycle.WL) + (i % _vifnCycle.WL)) * 16);
                    }
                }

                unpacker(reader);
            }

            _programCounter = Helpers.Align(_programCounter, OpcodeAlignment);
        }

        private uint ReadInt8() => (uint)(sbyte)_code[_programCounter++];
        private uint ReadUInt8() => _code[_programCounter++];
        private uint ReadInt16() => (uint)(short)(
            _code[_programCounter++] | (_code[_programCounter++] << 8));
        private uint ReadUInt16() => (ushort)(
            _code[_programCounter++] | (_code[_programCounter++] << 8));
        private uint ReadInt32() => (uint)(
            _code[_programCounter++] | (_code[_programCounter++] << 8) |
            (_code[_programCounter++] << 16) | (_code[_programCounter++] << 24));
        private uint ReadUInt32() => (uint)(
            _code[_programCounter++] | (_code[_programCounter++] << 8) |
            (_code[_programCounter++] << 16) | (_code[_programCounter++] << 24));

        // V4-5 is 4 values (RGBA) packed into 16 bits as 5-5-5-1. 
        // This takes some fiddling.
        private ushort _unpackV45Buffer;
        private int _unpackV45Idx = 0;
        private uint Read45()
        {
            if (_unpackV45Idx == 0)
                _unpackV45Buffer = (ushort)(_code[_programCounter++] | (_code[_programCounter++] << 8));

            uint val;
            switch (_unpackV45Idx)
            {
                case 0:
                    val = (uint)(_unpackV45Buffer & 0x001F) >> 0;
                    val <<= 3;
                    break;
                case 1:
                    val = (uint)(_unpackV45Buffer & 0x03E0) >> 5;
                    val <<= 3;
                    break;
                case 2:
                    val = (uint)(_unpackV45Buffer & 0x7C00) >> 10;
                    val <<= 3;
                    break;
                case 3:
                    val = (uint)(_unpackV45Buffer & 0x8000) >> 15;
                    val <<= 7;
                    break;
                default:
                    throw new Exception("UNPACK V4-5 DESYNC");
            }

            _unpackV45Idx = (_unpackV45Idx + 1) % 4;
            return val;
        }

        public void UnpackSingle(Func<uint> reader)
        {
            var currentMask = NextMask();
            var value = reader();

            if (currentMask.X == MaskType.Write) Write(value); Next();
            if (currentMask.Y == MaskType.Write) Write(value); Next();
            if (currentMask.Z == MaskType.Write) Write(value); Next();
            if (currentMask.W == MaskType.Write) Write(value); Next();
        }

        public void UnpackVector2(Func<uint> reader)
        {
            var currentMask = NextMask();
            var x = reader();
            var y = reader();

            if (currentMask.X == MaskType.Write) Write(x); Next();
            if (currentMask.Y == MaskType.Write) Write(y); Next();

            // While PS2 docs says that the following two values will
            // be indeterminate, PCSX2 seems to follow this exact
            // logic. Probably to emulate an undefined behaviour.
            // We are going to do the same, just in case.
            if (currentMask.Z == MaskType.Write) Write(x); Next();
            if (currentMask.W == MaskType.Write) Write(y); Next();
        }

        private void UnpackVector3(Func<uint> reader)
        {
            var currentMask = NextMask();

            if (currentMask.X == MaskType.Write) Write(reader()); Next();
            if (currentMask.Y == MaskType.Write) Write(reader()); Next();
            if (currentMask.Z == MaskType.Write) Write(reader()); Next();

            // According to PCSX2, the following logic emulates the
            // behaviour of the real hardware.. Time for some hacks!
            if (currentMask.W == MaskType.Write)
            {
                var oldProgramCounter = _programCounter;
                Write(reader()); // do not call Next() here!
                _programCounter = oldProgramCounter;
            }

            Next();
        }

        private void UnpackVector4(Func<uint> reader)
        {
            var currentMask = NextMask();

            if (currentMask.X == MaskType.Write) Write(reader()); Next();
            if (currentMask.Y == MaskType.Write) Write(reader()); Next();
            if (currentMask.Z == MaskType.Write) Write(reader()); Next();
            if (currentMask.W == MaskType.Write) Write(reader()); Next();
        }

        private void Write(uint value)
        {
            _mem[_destinationAddress + 0] = (byte)(value & 0xff);
            _mem[_destinationAddress + 1] = (byte)((value >> 8) & 0xff);
            _mem[_destinationAddress + 2] = (byte)((value >> 16) & 0xff);
            _mem[_destinationAddress + 3] = (byte)((value >> 24) & 0xff);
        }
        private void Next() => _destinationAddress += 4;
    }
}
