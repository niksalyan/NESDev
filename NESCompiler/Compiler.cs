using Acornima;
using Acornima.Ast;
using com.clusterrr.Famicom.Containers;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NESCompiler;

public class Compiler : AstVisitor
{
    public List<Variable> Variables => _prg?.Variables ?? [];

    public List<Instruction> Instructions => _prg?.Instructions ?? [];

    private CompilerFunctions _compilerFunctions;


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
        _compilerFunctions = new CompilerFunctions(
            this,
            _prg);
        _prg.Variables.Clear();



        BuildProgram();

        Debug.WriteLine($"PRG size: {_prg.ToBytecode().Length} bytes");

        var nes = new NesFile
        {
            Mapper = 0,
            PRG = _prg.ToBytecode(),
            CHR = Tileset.Default
        };

        var rom = nes.ToBytes();

        File.WriteAllBytes("output.nes", rom);
        File.WriteAllText("output.txt", Tileset.ToArduinoArray(nes.PRG));

        return rom;
    }



    // ============================================================
    // 6502 PROGRAM
    // ============================================================


    private void BuildProgram()
    {
        // CPU initialization
        _prg.Add(OpCode.Sei);
        _prg.Add(OpCode.Cld);
        _prg.Add(OpCode.LdxImmediate, 0xFF);
        _prg.Add(OpCode.Txs);

        // Disable rendering and NMI
        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2000); // PPUCTRL
        _prg.Add(OpCode.StaAbsolute, 0x2001); // PPUMASK

        // Disable APU channels and DMC IRQs
        _prg.Add(OpCode.StaAbsolute, 0x4015);
        _prg.Add(OpCode.StaAbsolute, 0x4010);

        // Write $3F00 directly using PPUADDR ($2006)
        _prg.Add(OpCode.LdaImmediate, 0x3F);
        _prg.Add(OpCode.StaAbsolute, 0x2006); // Address high byte

        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2006); // Address low byte

        // Write blue (palette index $01) through PPUDATA ($2007)
        _prg.Add(OpCode.LdaImmediate, 0x01);
        _prg.Add(OpCode.StaAbsolute, 0x2007);

        // Leave rendering disabled for this diagnostic.
        // The PPU should display its universal background color.
    }


    public string GetText(Acornima.Range range)
    {
        return _src.Substring(range.Start, range.Length);
    }

}

