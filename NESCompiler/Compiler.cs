using Acornima;
using Acornima.Ast;
using com.clusterrr.Famicom.Containers;
using System.Diagnostics;

namespace NESCompiler;

public class Compiler : AstVisitor
{
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

        int waitVBlank = 0; // Not sure yet
        int _prgP = 0; // This too

        _prg.Add(OpCode.BitAbsolute, 0x2002);
        _prg.Add(OpCode.Bpl, (sbyte)(waitVBlank - (_prgP + 2)));

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
        _prg.WritePpu((byte)243); // Ball tile/sprite
        _prg.WritePpu((byte)128); // Box tile/sprite

        _prg.Add(OpCode.LdaAbsolute, 0x2002);

        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2005);
        _prg.Add(OpCode.StaAbsolute, 0x2005);

        _prg.Add(OpCode.LdaImmediate, 0x08);
        _prg.Add(OpCode.StaAbsolute, 0x2001);

        int loopAddress = 0; // Need to figure out how to do this
        ushort cpuAddress = (ushort)(0x8000 + loopAddress);

        _prg.Add(OpCode.JmpAbsolute, cpuAddress);


        // This will be done automatically on bytecode generation _prg.ToBytecode();
        // _prg.WriteVector(0x7FFA, 0x8000);
        // _prg.WriteVector(0x7FFC, 0x8000); //
        // _prg.WriteVector(0x7FFE, 0x8000);
    }


    public string GetText(Acornima.Range range)
    {
        return _src.Substring(range.Start, range.Length);
    }

}