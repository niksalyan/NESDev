using Acornima.Ast;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection.Emit;
using System.Text;

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
    public string DisplayType => Kind.ToString() + "." + Type.ToString() + (IsArray ? "[" + Length + "]" : "");

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
        int length = 1
        )
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
    
    private List<Instruction> _instructions = [];
    private List<Variable> _variables = [];


    public void Add(OpCode opCode, object? operand = null)
    {
        _instructions.Add(new Instruction(opCode, operand));
    }


    public byte[] ToBytecode()
    {
        List<byte> bytes = new List<byte>();
        foreach (var instruction in _instructions)
        {
            bytes.Add((byte)instruction.OpCode);
            if (instruction.Operand != null)
            {
                if (instruction.Operand is byte b)
                {
                    bytes.Add(b);
                }
                else if (instruction.Operand is ushort s)
                {
                    bytes.Add((byte)(s & 0xFF));
                    bytes.Add((byte)(s >> 8));
                }
                else if (instruction.Operand is sbyte sb)
                {
                    bytes.Add((byte)sb);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unsupported operand type: {instruction.Operand.GetType()}");
                }
            }
        }
        return bytes.ToArray();
    }

    // ============================================================
    // PPU helpers
    // ============================================================

    public void PpuAddress(
        ushort address)
    {
        // IMPORTANT:
        //
        // PPUADDR ($2006) requires two writes:
        //   first = high byte
        //   second = low byte
        //
        // Reading PPUSTATUS ($2002) resets the write toggle.
        //
        // Without this, changing from $3F00 to $2000 can cause
        // the address to be interpreted incorrectly.

        // LDA $2002
        Add(OpCode.LdaAbsolute, 0x2002);
        Add(OpCode.LdaImmediate, (byte)(address >> 8));
        Add(OpCode.StaAbsolute, 0x2006);
        Add(OpCode.LdaImmediate, (byte)(address & 0xFF));
        Add(OpCode.StaAbsolute, 0x2006);
    }

    public void WritePpu(byte value)
    {
        Add(OpCode.LdaImmediate, value);
        Add(OpCode.StaAbsolute, 0x2007);
    }

    public Variable GetVariable(string name)
    {
        var variable = _variables.FirstOrDefault(x => x.Name == name);

        if (variable == null)
            throw new InvalidOperationException(
                $"Variable '{name}' is not declared.");

        return variable;
    }

    public Variable DeclareVariable(
    string name,
    VariableType type,
    VariableDeclarationKind kind,
    bool isArray = false,
    int length = 1)
    {
        var variable = _variables.FirstOrDefault(x => x.Name == name);
        if (variable != null)
        {
            return variable;
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
        int address = 0;
        foreach (var variable in _variables)
        {
            variable.Address = address;
            address += variable.GetSize();
        }
    }

}
