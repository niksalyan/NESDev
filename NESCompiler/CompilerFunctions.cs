using Acornima.Ast;
using System;

namespace NESCompiler
{
    public class CompilerFunctions
    {
        private readonly Bytecode _prg;

        public CompilerFunctions(Bytecode prg)
        {
            _prg = prg;
        }

        public void Execute(string name, object[]? args = null)
        {
            switch (name)
            {
                case "cursorAt":
                    CursorAt(args);
                    break;
                case "scroll":
                    Scroll(args);
                    break;
                case "print":
                    Print(args);
                    break;
                case "sprite":
                    Sprite(args);
                    break;
                case "frame":
                    EmitWaitVBlank();
                    break;

                default:
                    throw new NotSupportedException(
                        $"Compiler function '{name}' is not supported.");
            }
        }

        private void EmitWaitVBlank()
        {
            int waitStart = _prg.Instructions.Count;

            _prg.Add(OpCode.BitAbsolute, 0x2002);
            _prg.Add(OpCode.Bpl, waitStart);
        }

        private void Scroll(object[]? args)
        {
            if (args == null || args.Length != 2)
                throw new ArgumentException(
                    "scroll(x, y) requires exactly two arguments.");

            if (args[0] is not NumericLiteral xLiteral ||
                args[1] is not NumericLiteral yLiteral)
            {
                throw new ArgumentException(
                    "scroll(x, y) requires numeric literals.");
            }

            int x = Convert.ToInt32(xLiteral.Value);
            int y = Convert.ToInt32(yLiteral.Value);

            if (x < 0 || x > 255)
                throw new ArgumentOutOfRangeException(
                    nameof(x), "X must be between 0 and 255.");

            if (y < 0 || y > 255)
                throw new ArgumentOutOfRangeException(
                    nameof(y), "Y must be between 0 and 255.");

            // Reset PPU $2005 write latch.
            _prg.Add(OpCode.LdaAbsolute, 0x2002);

            // Horizontal scroll.
            _prg.Add(OpCode.LdaImmediate, x);
            _prg.Add(OpCode.StaAbsolute, 0x2005);

            // Vertical scroll.
            _prg.Add(OpCode.LdaImmediate, y);
            _prg.Add(OpCode.StaAbsolute, 0x2005);
        }

        private void Sprite(object[]? args)
        {
            if (args == null || args.Length != 4)
                throw new ArgumentException(
                    "sprite(index, tile, x, y) requires exactly four arguments.");

            if (args[0] is not NumericLiteral indexLiteral ||
                args[1] is not NumericLiteral tileLiteral ||
                args[2] is not NumericLiteral xLiteral ||
                args[3] is not NumericLiteral yLiteral)
            {
                throw new ArgumentException(
                    "sprite(index, tile, x, y) requires numeric literals.");
            }

            int index = Convert.ToInt32(indexLiteral.Value);
            int tile = Convert.ToInt32(tileLiteral.Value);
            int x = Convert.ToInt32(xLiteral.Value);
            int y = Convert.ToInt32(yLiteral.Value);

            if (index < 0 || index >= 64)
                throw new ArgumentOutOfRangeException(
                    nameof(index), "Sprite index must be between 0 and 63.");

            if (tile < 0 || tile > 255)
                throw new ArgumentOutOfRangeException(
                    nameof(tile), "Tile must be between 0 and 255.");

            if (x < 0 || x > 255)
                throw new ArgumentOutOfRangeException(
                    nameof(x), "X must be between 0 and 255.");

            if (y < 0 || y > 255)
                throw new ArgumentOutOfRangeException(
                    nameof(y), "Y must be between 0 and 255.");

            int oamAddress = index * 4;

            // Select sprite slot.
            _prg.Add(
                OpCode.LdaImmediate,
                oamAddress);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2003); // OAMADDR

            // Y
            _prg.Add(
                OpCode.LdaImmediate,
                y);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004); // OAMDATA

            // Tile
            _prg.Add(
                OpCode.LdaImmediate,
                tile);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004);

            // Attributes
            _prg.Add(
                OpCode.LdaImmediate,
                0x00);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004);

            // X
            _prg.Add(
                OpCode.LdaImmediate,
                x);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004);
        }

        private void CursorAt(object[]? args)
        {
            if (args == null || args.Length != 2)
                throw new ArgumentException(
                    "cursorAt(x, y) requires exactly two arguments.");

            if (args[0] is not Literal xLiteral ||
                args[1] is not Literal yLiteral)
            {
                throw new ArgumentException(
                    "cursorAt(x, y) requires numeric arguments.");
            }

            int x = Convert.ToInt32(xLiteral.Value);
            int y = Convert.ToInt32(yLiteral.Value);

            if (x < 0 || x >= 32)
                throw new ArgumentOutOfRangeException(
                    nameof(x), "X must be between 0 and 31.");

            if (y < 0 || y >= 30)
                throw new ArgumentOutOfRangeException(
                    nameof(y), "Y must be between 0 and 29.");

            int ppuAddress = 0x2000 + (y * 32) + x;

            _prg.PpuAddress((ushort)ppuAddress);
        }

        private void Print(object[]? args)
        {
            if (args == null || args.Length != 1)
                throw new ArgumentException(
                    "print(text) requires exactly one argument.");

            if (args[0] is not StringLiteral text)
                throw new ArgumentException(
                    "print() requires a string literal.");

            foreach (char c in text.Value)
            {
                _prg.WritePpu((byte)(c - 32));
            }
        }
    }
}