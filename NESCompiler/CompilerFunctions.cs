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

                case "print":
                    Print(args);
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

        private void CursorAt(object[]? args)
        {
            if (args == null || args.Length != 2)
                throw new ArgumentException(
                    "cursorAt(x, y) requires exactly two arguments.");

            int x = Convert.ToInt32(args[0]);
            int y = Convert.ToInt32(args[1]);

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