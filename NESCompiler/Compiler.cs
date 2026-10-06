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
        _prg.Sei();
        _prg.Cld();

        int waitVBlank = _prg.P;

        _prg.Bit(0x2002);
        _prg.Bpl((sbyte)(waitVBlank - (_prg.P + 2)));

        _prg.LdaImmediate(0x00);
        _prg.Sta(0x2000);

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
        _prg.WritePpu((byte)'!' - 32);

        _prg.Lda(0x2002);

        _prg.LdaImmediate(0x00);
        _prg.Sta(0x2005);
        _prg.Sta(0x2005);

        _prg.LdaImmediate(0x08);
        _prg.Sta(0x2001);

        int loopAddress = _prg.P;
        ushort cpuAddress = (ushort)(0x8000 + loopAddress);

        _prg.Jmp(cpuAddress);

        // _prg.WriteVector(0x7FFA, 0x8000);
        _prg.WriteVector(0x7FFC, 0x8000);
        // _prg.WriteVector(0x7FFE, 0x8000);
    }


    public string GetText(Acornima.Range range)
    {
        return _src.Substring(range.Start, range.Length);
    }

}