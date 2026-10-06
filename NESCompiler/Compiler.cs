using Acornima;
using Acornima.Ast;
using com.clusterrr.Famicom.Containers;
using System.Diagnostics;

namespace NESCompiler;

public class Compiler : AstVisitor
{
    private List<Variable> _variables;
    public List<Variable> Variables => _variables;

    private List<Instruction> _instructions;
    public List<Instruction> Instructions => _instructions;

    private static readonly Parser _parser = new Parser(new ParserOptions()
    {
        AllowTopLevelUsing = true
    });
    private string _src;
    private Bytecode _prg;

    public byte[] Compile(string source)
    {
        _src = source;
        _prg = new Bytecode();
        

        Node ast = _parser.ParseScript(_src);
        Visit(ast);

        BuildProgram();

        Debug.WriteLine($"PRG size: {_prg.ToBytecode().Length} bytes");

        _variables = _prg.Variables;
        _instructions = _prg.Instructions;

        var nes = new NesFile
        {
            Mapper = 0,
            PRG = _prg.ToBytecode(),
            CHR = Tileset.Default
        };
        return nes.ToBytes();
    }



    // ============================================================
    // 6502 PROGRAM
    // ============================================================

    private void BuildProgram()
    {
        _prg.Add(OpCode.Sei);
        _prg.Add(OpCode.Cld);

        // This is the instruction we want BPL to jump back to.
        int waitVBlank = _prg.Instructions.Count;

        _prg.Add(OpCode.BitAbsolute, 0x2002);

        // Target instruction index.
        _prg.Add(OpCode.Bpl, waitVBlank);

        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2000);

        _prg.PpuAddress(0x3F00);

        _prg.WritePpu(0x0F);
        _prg.WritePpu(0x30);
        _prg.WritePpu(0x20);
        _prg.WritePpu(0x10);

        _prg.PpuAddress(0x2090);

        _prg.WritePpu((byte)'H' - 32);
        _prg.WritePpu((byte)'E' - 32);
        _prg.WritePpu((byte)'L' - 32);
        _prg.WritePpu((byte)'L' - 32);
        _prg.WritePpu((byte)'O' - 32);

        _prg.WritePpu(0);
        _prg.WritePpu(243);
        _prg.WritePpu(128);

        _prg.Add(OpCode.LdaAbsolute, 0x2002);

        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2005);
        _prg.Add(OpCode.StaAbsolute, 0x2005);

        _prg.Add(OpCode.LdaImmediate, 0x08);
        _prg.Add(OpCode.StaAbsolute, 0x2001);

        // Jump back to the first instruction.
        _prg.Add(OpCode.JmpAbsolute, 0x8000);
    }


    public string GetText(Acornima.Range range)
    {
        return _src.Substring(range.Start, range.Length);
    }

}