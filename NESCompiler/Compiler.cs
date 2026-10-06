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
        
        // --------------------------------------------------------
        // CPU initialization
        // --------------------------------------------------------

        // SEI
        Emit(0x78);

        // CLD
        Emit(0xD8);

        // --------------------------------------------------------
        // Wait for PPU VBlank
        // --------------------------------------------------------

        int waitVBlank = _prg.P;

        // BIT $2002
        Emit(
            0x2C,
            0x02,
            0x20);

        // BPL waitVBlank
        //
        // BIT starts at $8002:
        //   $8002 BIT $2002
        //   $8005 BPL
        //   $8007 next instruction
        //
        // $8002 - $8007 = -5 = FB
        Emit(
            0x10,
            unchecked((byte)(waitVBlank - (_prg.P + 2))));

        // --------------------------------------------------------
        // Disable rendering while we configure the PPU
        // --------------------------------------------------------

        // LDA #$00
        Emit(
            0xA9,
            0x00);

        // STA $2000
        // PPUCTRL
        Emit(
            0x8D,
            0x00,
            0x20);

        // STA $2001
        // PPUMASK
        Emit(
            0x8D,
            0x01,
            0x20);

        // --------------------------------------------------------
        // Palette
        // --------------------------------------------------------

        // $3F00 = background palette
        PpuAddress(0x3F00);

        // $3F00
        // Universal background color
        WritePpu(0x0F);

        // $3F01
        WritePpu(0x30);

        // $3F02
        WritePpu(0x20);

        // $3F03
        WritePpu(0x10);

        // --------------------------------------------------------
        // Nametable
        // --------------------------------------------------------

        // Write HELLO starting at $2000.
        //
        // Tile numbers:
        //
        //   1 = H
        //   2 = E
        //   3 = L
        //   3 = L
        //   4 = O
        //
        PpuAddress(0x2090);

        WritePpu((byte)'H' - 32);
        WritePpu((byte)'E' - 32);
        WritePpu((byte)'L' - 32);
        WritePpu((byte)'L' - 32);
        WritePpu((byte)'O' - 32);
        WritePpu((byte)'!' - 32);

        // --------------------------------------------------------
        // Reset scroll
        // --------------------------------------------------------

        // Reading PPUSTATUS resets the $2005/$2006 write toggle.
        _prg.Emit(
            0xAD,
            0x02,
            0x20);

        // LDA #$00
        Emit(
            0xA9,
            0x00);

        // STA $2005
        // Horizontal scroll = 0
        Emit(
            0x8D,
            0x05,
            0x20);

        // STA $2005
        // Vertical scroll = 0
        Emit(
            0x8D,
            0x05,
            0x20);

        // --------------------------------------------------------
        // Enable background rendering
        // --------------------------------------------------------

        // LDA #$08
        //
        // PPUMASK:
        // bit 3 = background enable
        //
        Emit(
            0xA9,
            0x08);

        // STA $2001
        Emit(
            0x8D,
            0x01,
            0x20);

        // --------------------------------------------------------
        // Infinite loop
        // --------------------------------------------------------

        int loopAddress = _prg.P;

        // JMP $8000 + loopAddress
        ushort cpuAddress = (ushort)(0x8000 + loopAddress);

        Emit(
            0x4C,
            (byte)(cpuAddress & 0xFF),
            (byte)(cpuAddress >> 8));

        // --------------------------------------------------------
        // Reset / interrupt vectors
        //
        // PRG offsets:
        //
        // $7FFA = NMI
        // $7FFC = RESET
        // $7FFE = IRQ/BRK
        //
        // All point to $8000 for this tiny test ROM.
        // --------------------------------------------------------

        _prg.WriteVector(0x7FFA, 0x8000);
        _prg.WriteVector(0x7FFC, 0x8000);
        _prg.WriteVector(0x7FFE, 0x8000);
    }

    // ============================================================
    // PPU helpers
    // ============================================================

    private void PpuAddress(
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
        Emit(
            0xAD,
            0x02,
            0x20);

        // LDA #high byte
        Emit(
            0xA9,
            (byte)(address >> 8));

        // STA $2006
        Emit(
            0x8D,
            0x06,
            0x20);

        // LDA #low byte
        Emit(
            0xA9,
            (byte)(address & 0xFF));

        // STA $2006
        Emit(
            0x8D,
            0x06,
            0x20);
    }

    private void WritePpu(
        byte value)
    {
        // LDA #value
        Emit(
            0xA9,
            value);

        // STA $2007
        Emit(
            0x8D,
            0x07,
            0x20);
    }


    private void Emit(
        params byte[] bytes)
    {
        _prg.Emit(bytes);
    }

    public string GetText(Acornima.Range range)
    {
        return _src.Substring(range.Start, range.Length);
    }

}