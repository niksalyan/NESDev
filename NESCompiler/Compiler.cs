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

    public byte[] Compile(string source)
    {
        _src = source;
        var nes = new NesFile
        {
            Mapper = 0,
            PRG = new byte[32 * 1024],
            CHR = Tileset.ConvertsChar(AppDomain.CurrentDomain.BaseDirectory + "\\Oldskool-PC.png")
        };

        Node ast = _parser.ParseScript(_src);
        Visit(ast);

        // BuildChr(nes.CHR);


        Debug.WriteLine(AppDomain.CurrentDomain.BaseDirectory);

        BuildProgram(nes.PRG);

        return nes.ToBytes();
    }



    // ============================================================
    // 6502 PROGRAM
    // ============================================================

    private static void BuildProgram(byte[] prg)
    {
        int p = 0;

        // --------------------------------------------------------
        // CPU initialization
        // --------------------------------------------------------

        // SEI
        Emit(prg, ref p, 0x78);

        // CLD
        Emit(prg, ref p, 0xD8);

        // --------------------------------------------------------
        // Wait for PPU VBlank
        // --------------------------------------------------------

        int waitVBlank = p;

        // BIT $2002
        Emit(prg, ref p,
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
        Emit(prg, ref p,
            0x10,
            unchecked((byte)(waitVBlank - (p + 2))));

        // --------------------------------------------------------
        // Disable rendering while we configure the PPU
        // --------------------------------------------------------

        // LDA #$00
        Emit(prg, ref p,
            0xA9,
            0x00);

        // STA $2000
        // PPUCTRL
        Emit(prg, ref p,
            0x8D,
            0x00,
            0x20);

        // STA $2001
        // PPUMASK
        Emit(prg, ref p,
            0x8D,
            0x01,
            0x20);

        // --------------------------------------------------------
        // Palette
        // --------------------------------------------------------

        // $3F00 = background palette
        PpuAddress(prg, ref p, 0x3F00);

        // $3F00
        // Universal background color
        WritePpu(prg, ref p, 0x0F);

        // $3F01
        WritePpu(prg, ref p, 0x30);

        // $3F02
        WritePpu(prg, ref p, 0x20);

        // $3F03
        WritePpu(prg, ref p, 0x10);

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
        PpuAddress(prg, ref p, 0x2090);

        WritePpu(prg, ref p, 72 - 32);
        WritePpu(prg, ref p, 69 - 32);
        WritePpu(prg, ref p, 76 - 32);
        WritePpu(prg, ref p, 76 - 32);
        WritePpu(prg, ref p, 79 - 32);

        // --------------------------------------------------------
        // Reset scroll
        // --------------------------------------------------------

        // Reading PPUSTATUS resets the $2005/$2006 write toggle.
        Emit(prg, ref p,
            0xAD,
            0x02,
            0x20);

        // LDA #$00
        Emit(prg, ref p,
            0xA9,
            0x00);

        // STA $2005
        // Horizontal scroll = 0
        Emit(prg, ref p,
            0x8D,
            0x05,
            0x20);

        // STA $2005
        // Vertical scroll = 0
        Emit(prg, ref p,
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
        Emit(prg, ref p,
            0xA9,
            0x08);

        // STA $2001
        Emit(prg, ref p,
            0x8D,
            0x01,
            0x20);

        // --------------------------------------------------------
        // Infinite loop
        // --------------------------------------------------------

        int loopAddress = p;

        // JMP $8000 + loopAddress
        ushort cpuAddress = (ushort)(0x8000 + loopAddress);

        Emit(prg, ref p,
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

        WriteVector(prg, 0x7FFA, 0x8000);
        WriteVector(prg, 0x7FFC, 0x8000);
        WriteVector(prg, 0x7FFE, 0x8000);
    }

    // ============================================================
    // PPU helpers
    // ============================================================

    private static void PpuAddress(
        byte[] prg,
        ref int p,
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
        Emit(prg, ref p,
            0xAD,
            0x02,
            0x20);

        // LDA #high byte
        Emit(prg, ref p,
            0xA9,
            (byte)(address >> 8));

        // STA $2006
        Emit(prg, ref p,
            0x8D,
            0x06,
            0x20);

        // LDA #low byte
        Emit(prg, ref p,
            0xA9,
            (byte)(address & 0xFF));

        // STA $2006
        Emit(prg, ref p,
            0x8D,
            0x06,
            0x20);
    }

    private static void WritePpu(
        byte[] prg,
        ref int p,
        byte value)
    {
        // LDA #value
        Emit(prg, ref p,
            0xA9,
            value);

        // STA $2007
        Emit(prg, ref p,
            0x8D,
            0x07,
            0x20);
    }

    // ============================================================
    // 6502 helpers
    // ============================================================

    private static void WriteVector(
        byte[] prg,
        int offset,
        ushort address)
    {
        prg[offset] = (byte)(address & 0xFF);
        prg[offset + 1] = (byte)(address >> 8);
    }

    private static void Emit(
        byte[] prg,
        ref int p,
        params byte[] bytes)
    {
        foreach (byte b in bytes)
        {
            prg[p++] = b;
        }
    }

    public string GetText(Acornima.Range range)
    {
        return _src.Substring(range.Start, range.Length);
    }

}