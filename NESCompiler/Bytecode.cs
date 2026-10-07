using Acornima.Ast;
using System.ComponentModel;
using System.Diagnostics;
using dotNES;

namespace NESCompiler;

public enum OpCode : byte
{
    Brk = 0x00,

    OraIndirectX = 0x01,
    OraZeroPage = 0x05,
    AslZeroPage = 0x06,
    Php = 0x08,
    OraImmediate = 0x09,
    AslAccumulator = 0x0A,
    OraAbsolute = 0x0D,
    AslAbsolute = 0x0E,

    Bpl = 0x10,
    OraIndirectY = 0x11,
    OraZeroPageX = 0x15,
    AslZeroPageX = 0x16,
    Clc = 0x18,
    OraAbsoluteY = 0x19,
    OraAbsoluteX = 0x1D,
    AslAbsoluteX = 0x1E,

    Jsr = 0x20,
    AndIndirectX = 0x21,
    BitZeroPage = 0x24,
    AndZeroPage = 0x25,
    RolZeroPage = 0x26,
    Plp = 0x28,
    AndImmediate = 0x29,
    RolAccumulator = 0x2A,
    BitAbsolute = 0x2C,
    AndAbsolute = 0x2D,
    RolAbsolute = 0x2E,

    Bmi = 0x30,
    AndIndirectY = 0x31,
    AndZeroPageX = 0x35,
    RolZeroPageX = 0x36,
    Sec = 0x38,
    AndAbsoluteY = 0x39,
    AndAbsoluteX = 0x3D,
    RolAbsoluteX = 0x3E,

    Rti = 0x40,
    EorIndirectX = 0x41,
    EorZeroPage = 0x45,
    LsrZeroPage = 0x46,
    Pha = 0x48,
    EorImmediate = 0x49,
    LsrAccumulator = 0x4A,
    JmpAbsolute = 0x4C,
    EorAbsolute = 0x4D,
    LsrAbsolute = 0x4E,

    Bvc = 0x50,
    EorIndirectY = 0x51,
    EorZeroPageX = 0x55,
    LsrZeroPageX = 0x56,
    Cli = 0x58,
    EorAbsoluteY = 0x59,
    EorAbsoluteX = 0x5D,
    LsrAbsoluteX = 0x5E,

    Rts = 0x60,
    AdcIndirectX = 0x61,
    AdcZeroPage = 0x65,
    RorZeroPage = 0x66,
    Pla = 0x68,
    AdcImmediate = 0x69,
    RorAccumulator = 0x6A,
    JmpIndirect = 0x6C,
    AdcAbsolute = 0x6D,
    RorAbsolute = 0x6E,

    Bvs = 0x70,
    AdcIndirectY = 0x71,
    AdcZeroPageX = 0x75,
    RorZeroPageX = 0x76,
    Sei = 0x78,
    AdcAbsoluteY = 0x79,
    AdcAbsoluteX = 0x7D,
    RorAbsoluteX = 0x7E,

    StaIndirectX = 0x81,
    StyZeroPage = 0x84,
    StaZeroPage = 0x85,
    StxZeroPage = 0x86,
    Dey = 0x88,
    Txa = 0x8A,
    StyAbsolute = 0x8C,
    StaAbsolute = 0x8D,
    StxAbsolute = 0x8E,

    Bcc = 0x90,
    StaIndirectY = 0x91,
    StyZeroPageX = 0x94,
    StaZeroPageX = 0x95,
    StxZeroPageY = 0x96,
    Tya = 0x98,
    StaAbsoluteY = 0x99,
    Txs = 0x9A,
    StaAbsoluteX = 0x9D,

    LdyImmediate = 0xA0,
    LdaIndirectX = 0xA1,
    LdxImmediate = 0xA2,
    LdyZeroPage = 0xA4,
    LdaZeroPage = 0xA5,
    LdxZeroPage = 0xA6,
    Tay = 0xA8,
    LdaImmediate = 0xA9,
    Tax = 0xAA,
    LdyAbsolute = 0xAC,
    LdaAbsolute = 0xAD,
    LdxAbsolute = 0xAE,

    Bcs = 0xB0,
    LdaIndirectY = 0xB1,
    LdyZeroPageX = 0xB4,
    LdaZeroPageX = 0xB5,
    LdxZeroPageY = 0xB6,
    Clv = 0xB8,
    LdaAbsoluteY = 0xB9,
    Tsx = 0xBA,
    LdyAbsoluteX = 0xBC,
    LdaAbsoluteX = 0xBD,
    LdxAbsoluteY = 0xBE,

    CpyImmediate = 0xC0,
    CmpIndirectX = 0xC1,
    CpyZeroPage = 0xC4,
    CmpZeroPage = 0xC5,
    DecZeroPage = 0xC6,
    Iny = 0xC8,
    CmpImmediate = 0xC9,
    Dex = 0xCA,
    CpyAbsolute = 0xCC,
    CmpAbsolute = 0xCD,
    DecAbsolute = 0xCE,

    Bne = 0xD0,
    CmpIndirectY = 0xD1,
    CmpZeroPageX = 0xD5,
    DecZeroPageX = 0xD6,
    Cld = 0xD8,
    CmpAbsoluteY = 0xD9,
    CmpAbsoluteX = 0xDD,
    DecAbsoluteX = 0xDE,

    CpxImmediate = 0xE0,
    SbcIndirectX = 0xE1,
    CpxZeroPage = 0xE4,
    SbcZeroPage = 0xE5,
    IncZeroPage = 0xE6,
    Inx = 0xE8,
    SbcImmediate = 0xE9,
    Nop = 0xEA,
    CpxAbsolute = 0xEC,
    SbcAbsolute = 0xED,
    IncAbsolute = 0xEE,

    Beq = 0xF0,
    SbcIndirectY = 0xF1,
    SbcZeroPageX = 0xF5,
    IncZeroPageX = 0xF6,
    Sed = 0xF8,
    SbcAbsoluteY = 0xF9,
    SbcAbsoluteX = 0xFD,
    IncAbsoluteX = 0xFE
}

public class Instruction
{
    [DisplayName("#")]
    public int Index { get; set; }

    public OpCode OpCode { get; }

    public object? Operand { get; }

    public int Address { get; set; }

    public Instruction(
        OpCode opCode,
        object? operand = null)
    {
        OpCode = opCode;
        Operand = operand;
    }
}

public enum VariableType
{
    None,
    Byte,
    Int16,
    Int32
}

public class Variable
{
    public string Name { get; }

    [DisplayName("Type")]
    public string DisplayType =>
        Kind.ToString() +
        "." +
        Type.ToString() +
        (IsArray ? "[" + Length + "]" : "");


    public byte Value => Emulator.instance.CPU.ReadMemory((ushort)Address);

    [Browsable(false)]
    public VariableType Type { get; }

    [Browsable(false)]
    public VariableDeclarationKind Kind { get; }

    public int Address { get; set; }

    public int Size => GetSize();

    public bool IsArray = false;

    public int Length;

    public Variable(
        string name,
        VariableType type,
        VariableDeclarationKind kind,
        bool isArray = false,
        int length = 1)
    {
        Name = name;
        Type = type;
        Kind = kind;
        IsArray = isArray;
        Length = length;
    }

    public int GetElementSize()
    {
        switch (Type)
        {
            case VariableType.Byte:
                return 1;

            case VariableType.Int16:
                return 2;

            case VariableType.Int32:
                return 4;

            default:
                return 0;
        }
    }

    public int GetSize()
    {
        return GetElementSize() * Length;
    }
}

public class Bytecode
{
    public const int PrgSize = 32 * 1024;

    // CPU address where PRG starts.
    public const int PrgCpuAddress = 0x8000;

    // Last 6 bytes of the 32 KB PRG are the vectors.
    public const int VectorOffset = 0x7FFA;

    private readonly List<Instruction> _instructions = [];

    private readonly List<Variable> _variables = [];

    public List<Variable> Variables =>
        _variables;

    public List<Instruction> Instructions =>
        _instructions;

    public void Add(
        OpCode opCode,
        object? operand = null)
    {
        _instructions.Add(
            new Instruction(opCode, operand));
    }

    // ============================================================
    // Final bytecode generation
    // ============================================================

    public byte[] ToBytecode()
    {
        // Pass 1:
        // Generate the bytecode and assign instruction addresses.
        PrepareBytecode(assignAddresses: true);

        // Pass 2:
        // Generate the final bytecode using the addresses calculated
        // during pass 1.
        return PrepareBytecode(assignAddresses: false);
    }

    public byte[] PrepareBytecode(
        bool assignAddresses = false)
    {
        using var stream = new MemoryStream(
            capacity: PrgSize);

        int index = 0;

        foreach (var instruction in _instructions)
        {
            if (assignAddresses)
            {
                instruction.Index = index;

                instruction.Address =
                    checked(
                        PrgCpuAddress +
                        (int)stream.Length);
            }

            WriteInstruction(
                stream,
                instruction,
                assignAddresses);

            index++;
        }

        // The program must fit before the vector table.
        if (stream.Length > VectorOffset)
        {
            throw new InvalidOperationException(
                $"NES program is too large. " +
                $"Program size: {stream.Length} bytes, " +
                $"maximum: {VectorOffset} bytes.");
        }

        // Fill unused PRG space with zeroes.
        while (stream.Length < VectorOffset)
        {
            stream.WriteByte(0x00);
        }

        // ========================================================
        // NES vectors
        //
        // PRG offset $7FFA = CPU $FFFA = NMI
        // PRG offset $7FFC = CPU $FFFC = RESET
        // PRG offset $7FFE = CPU $FFFE = IRQ/BRK
        // ========================================================

        ushort entryAddress =
            _instructions.Count > 0
                ? (ushort)_instructions[0].Address
                : (ushort)PrgCpuAddress;

        WriteUInt16(stream, entryAddress); // NMI
        WriteUInt16(stream, entryAddress); // RESET
        WriteUInt16(stream, entryAddress); // IRQ/BRK

        if (stream.Length != PrgSize)
        {
            throw new InvalidOperationException(
                $"Invalid NES PRG size: {stream.Length} bytes. " +
                $"Expected exactly {PrgSize} bytes.");
        }

        return stream.ToArray();
    }

    // ============================================================
    // Instruction encoder
    // ============================================================

    private void WriteInstruction(
        Stream stream,
        Instruction instruction,
        bool assignAddresses)
    {
        stream.WriteByte(
            (byte)instruction.OpCode);

        switch (instruction.OpCode)
        {
            // ----------------------------------------------------
            // No operand
            // ----------------------------------------------------

            case OpCode.Php:
            case OpCode.Plp:
            case OpCode.Pha:
            case OpCode.Pla:
            case OpCode.Rti:
            case OpCode.Rts:
            case OpCode.Clc:
            case OpCode.Sec:
            case OpCode.Cli:
            case OpCode.Sei:
            case OpCode.Clv:
            case OpCode.Cld:
            case OpCode.Sed:
            case OpCode.Tay:
            case OpCode.Txa:
            case OpCode.Tya:
            case OpCode.Txs:
            case OpCode.Tsx:
            case OpCode.Iny:
            case OpCode.Dey:
            case OpCode.Inx:
            case OpCode.Dex:
            case OpCode.Nop:
            case OpCode.AslAccumulator:
                break;

            // ----------------------------------------------------
            // One-byte operands
            // ----------------------------------------------------

            case OpCode.OraImmediate:
            case OpCode.AslZeroPage:
            case OpCode.OraZeroPage:
            case OpCode.OraZeroPageX:
            case OpCode.AslZeroPageX:
            case OpCode.AndImmediate:
            case OpCode.BitZeroPage:
            case OpCode.AndZeroPage:
            case OpCode.AndZeroPageX:
            case OpCode.RolZeroPage:
            case OpCode.RolZeroPageX:
            case OpCode.EorImmediate:
            case OpCode.EorZeroPage:
            case OpCode.EorZeroPageX:
            case OpCode.LsrZeroPage:
            case OpCode.LsrZeroPageX:
            case OpCode.AdcImmediate:
            case OpCode.AdcZeroPage:
            case OpCode.AdcZeroPageX:
            case OpCode.RorZeroPage:
            case OpCode.RorZeroPageX:
            case OpCode.StyZeroPage:
            case OpCode.StaZeroPage:
            case OpCode.StxZeroPage:
            case OpCode.StyZeroPageX:
            case OpCode.StaZeroPageX:
            case OpCode.StxZeroPageY:
            case OpCode.LdaImmediate:
            case OpCode.LdyImmediate:
            case OpCode.LdaZeroPage:
            case OpCode.LdxImmediate:
            case OpCode.LdyZeroPage:
            case OpCode.LdxZeroPage:
            case OpCode.LdyZeroPageX:
            case OpCode.LdaZeroPageX:
            case OpCode.LdxZeroPageY:
            case OpCode.CpyImmediate:
            case OpCode.CpyZeroPage:
            case OpCode.CmpImmediate:
            case OpCode.CmpZeroPage:
            case OpCode.CmpZeroPageX:
            case OpCode.DecZeroPage:
            case OpCode.DecZeroPageX:
            case OpCode.CpxImmediate:
            case OpCode.CpxZeroPage:
            case OpCode.SbcImmediate:
            case OpCode.SbcZeroPage:
            case OpCode.SbcZeroPageX:
            case OpCode.IncZeroPage:
            case OpCode.IncZeroPageX:
            case OpCode.OraIndirectX:
            case OpCode.OraIndirectY:
            case OpCode.AndIndirectX:
            case OpCode.AndIndirectY:
            case OpCode.EorIndirectX:
            case OpCode.EorIndirectY:
            case OpCode.AdcIndirectX:
            case OpCode.AdcIndirectY:
            case OpCode.StaIndirectX:
            case OpCode.StaIndirectY:
            case OpCode.LdaIndirectX:
            case OpCode.LdaIndirectY:
            case OpCode.CmpIndirectX:
            case OpCode.CmpIndirectY:
            case OpCode.SbcIndirectX:
            case OpCode.SbcIndirectY:
                WriteByteOperand(
                    stream,
                    instruction.Operand);
                break;

            // ----------------------------------------------------
            // Relative branches
            // ----------------------------------------------------

            case OpCode.Bpl:
            case OpCode.Bmi:
            case OpCode.Bvc:
            case OpCode.Bvs:
            case OpCode.Bcc:
            case OpCode.Bcs:
            case OpCode.Bne:
            case OpCode.Beq:
                WriteBranchOperand(
                    stream,
                    instruction,
                    assignAddresses);
                break;

            // ----------------------------------------------------
            // Two-byte operands
            // ----------------------------------------------------
            case OpCode.OraAbsolute:
            case OpCode.OraAbsoluteX:
            case OpCode.OraAbsoluteY:
            case OpCode.AndAbsolute:
            case OpCode.AndAbsoluteX:
            case OpCode.AndAbsoluteY:
            case OpCode.BitAbsolute:
            case OpCode.RolAbsolute:
            case OpCode.RolAbsoluteX:
            case OpCode.EorAbsolute:
            case OpCode.EorAbsoluteX:
            case OpCode.EorAbsoluteY:
            case OpCode.LsrAbsolute:
            case OpCode.LsrAbsoluteX:
            case OpCode.AslAbsolute:
            case OpCode.AslAbsoluteX:
            case OpCode.AdcAbsolute:
            case OpCode.AdcAbsoluteX:
            case OpCode.AdcAbsoluteY:
            case OpCode.RorAbsolute:
            case OpCode.RorAbsoluteX:
            case OpCode.StyAbsolute:
            case OpCode.StaAbsolute:
            case OpCode.StaAbsoluteX:
            case OpCode.StaAbsoluteY:
            case OpCode.StxAbsolute:
            case OpCode.LdyAbsolute:
            case OpCode.LdyAbsoluteX:
            case OpCode.LdaAbsolute:
            case OpCode.LdaAbsoluteX:
            case OpCode.LdaAbsoluteY:
            case OpCode.LdxAbsolute:
            case OpCode.LdxAbsoluteY:
            case OpCode.CpyAbsolute:
            case OpCode.CmpAbsolute:
            case OpCode.CmpAbsoluteX:
            case OpCode.CmpAbsoluteY:
            case OpCode.DecAbsolute:
            case OpCode.DecAbsoluteX:
            case OpCode.CpxAbsolute:
            case OpCode.SbcAbsolute:
            case OpCode.SbcAbsoluteX:
            case OpCode.SbcAbsoluteY:
            case OpCode.IncAbsolute:
            case OpCode.IncAbsoluteX:
            case OpCode.Jsr:
            case OpCode.JmpIndirect:
                WriteUInt16Operand(
                    stream,
                    instruction.Operand);
                break;

            case OpCode.JmpAbsolute:

                int targetInstruction = (int)instruction.Operand;



                int targetAddress = Instructions[targetInstruction].Address;
                Debug.WriteLine($"Target instruction:{instruction.OpCode}  {targetInstruction} {targetAddress}");

                WriteUInt16Operand(
                    stream,
                    targetAddress);

                break;

            case OpCode.Brk:
                // BRK is a 2-byte instruction on 6502.
                // The second byte is ignored by the CPU.
                WriteByteOperand(
                    stream,
                    instruction.Operand ?? (byte)0);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported opcode: {instruction.OpCode}");
        }
    }

    // ============================================================
    // Branch encoding
    // ============================================================

    private void WriteBranchOperand(
        Stream stream,
        Instruction instruction,
        bool assignAddresses)
    {
        if (instruction.Operand == null)
        {
            throw new InvalidOperationException(
                $"Branch {instruction.OpCode} requires an operand.");
        }

        // Operand can be:
        //
        // 1. sbyte:
        //    Already-resolved relative offset.
        //
        // 2. int:
        //    Instruction index.
        //
        // For the compiler we use instruction indexes so that
        // pass 1 can resolve the actual relative distance.

        if (instruction.Operand is int targetInstruction)
        {
            if (targetInstruction < 0 ||
                targetInstruction >= _instructions.Count)
            {
                throw new InvalidOperationException(
                    $"Invalid branch target instruction: {targetInstruction}");
            }

            if (assignAddresses)
            {
                // Pass 1 only needs the size.
                stream.WriteByte(0);
                return;
            }

            int targetAddress =
                _instructions[targetInstruction].Address;

            int branchAddress =
                instruction.Address;

            int nextInstruction =
                branchAddress + 2;

            int offset =
                targetAddress - nextInstruction;

            if (offset < -128 || offset > 127)
            {
                throw new InvalidOperationException(
                    $"Branch target is out of range: " +
                    $"{instruction.OpCode} at ${branchAddress:X4} " +
                    $"to ${targetAddress:X4}.");
            }

            stream.WriteByte(
                unchecked((byte)(sbyte)offset));

            return;
        }

        if (instruction.Operand is sbyte relative)
        {
            stream.WriteByte(
                unchecked((byte)relative));

            return;
        }

        throw new InvalidOperationException(
            $"Invalid branch operand type: " +
            $"{instruction.Operand.GetType()}");
    }

    // ============================================================
    // Operand writers
    // ============================================================

    private static void WriteByteOperand(
        Stream stream,
        object? operand)
    {
        if (operand == null)
        {
            throw new InvalidOperationException(
                "Instruction requires a byte operand.");
        }

        stream.WriteByte(
            Convert.ToByte(operand));
    }

    private static void WriteUInt16Operand(
        Stream stream,
        object? operand)
    {
        if (operand == null)
        {
            throw new InvalidOperationException(
                "Instruction requires a 16-bit operand.");
        }

        WriteUInt16(
            stream,
            Convert.ToUInt16(operand));
    }

    private static void WriteUInt16(
        Stream stream,
        ushort value)
    {
        // 6502 is little-endian.
        stream.WriteByte(
            (byte)(value & 0xFF));

        stream.WriteByte(
            (byte)(value >> 8));
    }

    // ============================================================
    // PPU helpers
    // ============================================================

    public void PpuAddress(
        ushort address)
    {
        // Reading PPUSTATUS resets the PPUADDR write toggle.
        Add(OpCode.LdaAbsolute, 0x2002);

        // High byte.
        Add(
            OpCode.LdaImmediate,
            (byte)(address >> 8));

        Add(
            OpCode.StaAbsolute,
            0x2006);

        // Low byte.
        Add(
            OpCode.LdaImmediate,
            (byte)(address & 0xFF));

        Add(
            OpCode.StaAbsolute,
            0x2006);
    }

    public void WritePpu(
        byte value)
    {
        Add(
            OpCode.LdaImmediate,
            value);

        Add(
            OpCode.StaAbsolute,
            0x2007);
    }

    // ============================================================
    // Variables
    // ============================================================

    public Variable GetVariable(
        string name)
    {
        var variable =
            _variables.FirstOrDefault(
                x => x.Name == name);

        if (variable == null)
        {
            throw new InvalidOperationException(
                $"Variable '{name}' is not declared.");
        }

        return variable;
    }

    public Variable DeclareVariable(
        string name,
        VariableType type,
        VariableDeclarationKind kind,
        bool isArray = false,
        int length = 1)
    {
        var variable =
            _variables.FirstOrDefault(
                x => x.Name == name);

        if (variable != null)
        {
            throw new InvalidOperationException(
                $"Variable '{name}' has already been declared.");
        }

        variable = new Variable(
            name,
            type,
            kind,
            isArray,
            length);

        _variables.Add(variable);

        UpdateAddresses();

        return variable;
    }

    public void UpdateAddresses()
    {
        int zeroAddress = 0;
        int upperAddress = 512;

        foreach (var variable in _variables)
        {
            if (variable.Kind != VariableDeclarationKind.Const)
            {
                int vSize = variable.GetSize();
                if (vSize == 1)
                {
                    variable.Address = zeroAddress;
                    zeroAddress += vSize;
                } else
                {
                    variable.Address = upperAddress;
                    upperAddress += vSize;
                }
            }
            
        }
    }
}
