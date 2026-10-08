using Acornima.Ast;
using System;
using System.Diagnostics;

namespace NESCompiler
{
    public class CompilerFunctions
    {
        private readonly Compiler _compiler;
        private readonly Bytecode _prg;
        private readonly Action<Expression> _emitExpression;
        private readonly Action _popExpression;
        private readonly Func<byte> _getExpressionStackDepth;
        private readonly byte _expressionStackBase;

        private const byte Controller1Address = 0xEE;
        private const byte Controller2Address = 0xED;

        public CompilerFunctions(
            Compiler compiler,
            Bytecode prg,
            Action<Expression> emitExpression,
            Action popExpression,
            Func<byte> getExpressionStackDepth,
            byte expressionStackBase)
        {
            _compiler = compiler;
            _prg = prg;
            _emitExpression = emitExpression;
            _popExpression = popExpression;
            _getExpressionStackDepth = getExpressionStackDepth;
            _expressionStackBase = expressionStackBase;
        }

        public VariableType Execute(
            string name,
            IReadOnlyList<Expression>? args = null)
        {
            switch (name)
            {
                case "cursor":
                    CursorAt(args);
                    return VariableType.None;

                case "scroll":
                    Scroll(args);
                    return VariableType.None;

                case "print":
                    Print(args);
                    return VariableType.None;

                case "tile":
                    TileAt(args);
                    return VariableType.None;

                case "sprite":
                    Sprite(args);
                    return VariableType.None;

                case "frame":
                    if (args != null && args.Count != 0)
                    {
                        throw new ArgumentException(
                            "frame() does not accept arguments.");
                    }

                    Input();
                    EmitWaitVBlank();
                    return VariableType.None;

                case "A1":
                    A1();
                    return VariableType.Byte;
                case "B1":
                    B1();
                    return VariableType.Byte;
                case "START1":
                    Start1();
                    return VariableType.Byte;
                case "SELECT1":
                    Select1();
                    return VariableType.Byte;
                case "UP1":
                    Up1();
                    return VariableType.Byte;
                case "DOWN1":
                    Down1();
                    return VariableType.Byte;
                case "LEFT1":
                    Left1();
                    return VariableType.Byte;
                case "RIGHT1":
                    Right1();
                    return VariableType.Byte;
                default:
                    throw new NotSupportedException(
                        $"Compiler function '{name}' is not supported.");
            }
        }

        private void EmitWaitVBlank()
        {
            int waitStart = _prg.Instructions.Count;

            _prg.Add(
                OpCode.BitAbsolute,
                0x2002);

            _prg.Add(
                OpCode.Bpl,
                waitStart);
        }

        private int EmitArguments(
            IReadOnlyList<Expression>? args,
            int expectedCount,
            string functionName)
        {
            if (args == null || args.Count != expectedCount)
            {
                throw new ArgumentException(
                    $"{functionName} requires exactly {expectedCount} arguments.");
            }

            // Remember where these arguments will start in
            // the expression stack.
            int startDepth = _getExpressionStackDepth();

            foreach (var argument in args)
            {
                _emitExpression(argument);
            }

            return startDepth;
        }

        private void PopArguments(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _popExpression();
            }
        }

        private void LoadArgument(
            int startDepth,
            int index)
        {
            int address =
                _expressionStackBase +
                startDepth +
                index;

            _prg.Add(
                OpCode.LdaZeroPage,
                address);
        }

        private void CursorAt(IReadOnlyList<Expression>? args)
        {
            int startDepth =
                EmitArguments(
                    args,
                    2,
                    "cursor");

            _compiler.EmitCursorAddress(startDepth);

            PopArguments(2);
        }


        private void Scroll(IReadOnlyList<Expression>? args)
        {
            int startDepth =
                EmitArguments(
                    args,
                    2,
                    "scroll");

            // Reset PPU $2005 write latch.
            _prg.Add(
                OpCode.LdaAbsolute,
                0x2002);

            // X
            LoadArgument(startDepth, 0);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2005);

            // Y
            LoadArgument(startDepth, 1);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2005);

            PopArguments(2);
        }

        private void Sprite(IReadOnlyList<Expression>? args)
        {
            int startDepth =
                EmitArguments(
                    args,
                    4,
                    "sprite");


            if (args![0] is not NumericLiteral indexLiteral)
            {
                PopArguments(4);

                throw new ArgumentException(
                    "sprite() currently requires a numeric literal for index.");
            }

            int index =
                Convert.ToInt32(indexLiteral.Value);

            if (index < 0 || index >= 64)
            {
                PopArguments(4);

                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    "Sprite index must be between 0 and 63.");
            }

            int oamAddress = index * 4;

            // Select sprite slot.
            _prg.Add(
                OpCode.LdaImmediate,
                oamAddress);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2003);

            // Y
            LoadArgument(startDepth, 3);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004);

            // Tile
            LoadArgument(startDepth, 1);

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
            LoadArgument(startDepth, 2);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004);

            PopArguments(4);
        }

        
        private void Print(IReadOnlyList<Expression>? args)
        {
            if (args == null || args.Count != 1)
            {
                throw new ArgumentException(
                    "print(text) requires exactly one argument.");
            }

            if (args[0] is not StringLiteral text)
            {
                throw new ArgumentException(
                    "print() requires a string literal.");
            }

            foreach (char c in text.Value)
            {
                _prg.WritePpu(
                    (byte)(c - 32));
            }
        }

        private void TileAt(IReadOnlyList<Expression>? args)
        {
            int startDepth = EmitArguments(args, 3, "tileAt");

            // ------------------------------------------------------------
            // PPUADDR high byte
            //
            // address = $2000 + y * 32 + x
            //
            // high = $20 + (y >> 3)
            // ------------------------------------------------------------

            // Reset PPUADDR latch
            _prg.Add(OpCode.LdaAbsolute, 0x2002);

            // A = y
            LoadArgument(startDepth, 1);

            // A = y >> 3
            _prg.Add(OpCode.LsrAccumulator);
            _prg.Add(OpCode.LsrAccumulator);
            _prg.Add(OpCode.LsrAccumulator);

            // A = $20 + (y >> 3)
            _prg.Add(OpCode.Clc);
            _prg.Add(OpCode.AdcImmediate, 0x20);

            // PPUADDR high byte
            _prg.Add(OpCode.StaAbsolute, 0x2006);

            // ------------------------------------------------------------
            // PPUADDR low byte
            //
            // low = ((y & 7) << 5) + x
            // ------------------------------------------------------------

            // A = y
            LoadArgument(startDepth, 1);

            // A = y & 7
            _prg.Add(OpCode.AndImmediate, 0x07);

            // A = (y & 7) << 5
            _prg.Add(OpCode.AslAccumulator);
            _prg.Add(OpCode.AslAccumulator);
            _prg.Add(OpCode.AslAccumulator);
            _prg.Add(OpCode.AslAccumulator);
            _prg.Add(OpCode.AslAccumulator);

            // A = ((y & 7) << 5) + x
            _prg.Add(OpCode.Clc);
            _prg.Add(
                OpCode.AdcZeroPage,
                _expressionStackBase + startDepth);

            // PPUADDR low byte
            _prg.Add(OpCode.StaAbsolute, 0x2006);

            // ------------------------------------------------------------
            // Write tile
            // ------------------------------------------------------------

            // A = tile
            LoadArgument(startDepth, 2);

            // PPUDATA = tile
            _prg.Add(OpCode.StaAbsolute, 0x2007);

            // ------------------------------------------------------------
            // Restore normal rendering address
            // ------------------------------------------------------------

            _prg.PpuAddress(0x2000);

            // Remove x, y, tile from expression stack
            PopArguments(3);
        }

        private void Input()
        {
            // Latch both controllers.
            _prg.Add(
                OpCode.LdaImmediate,
                0x01);

            _prg.Add(
                OpCode.StaAbsolute,
                0x4016);

            _prg.Add(
                OpCode.LdaImmediate,
                0x00);

            _prg.Add(
                OpCode.StaAbsolute,
                0x4016);

            // Clear previous states.
            _prg.Add(
                OpCode.LdaImmediate,
                0x00);

            _prg.Add(
                OpCode.StaZeroPage,
                Controller1Address);

            _prg.Add(
                OpCode.StaZeroPage,
                Controller2Address);

            // Read controller 1.
            for (int i = 0; i < 8; i++)
            {
                _prg.Add(
                    OpCode.LdaAbsolute,
                    0x4016);

                // Controller bit 0 -> Carry.
                _prg.Add(
                    OpCode.LsrAccumulator);

                // Carry -> bit 0, previous bits shift left.
                _prg.Add(
                    OpCode.RolZeroPage,
                    Controller1Address);
            }

            // Read controller 2.
            for (int i = 0; i < 8; i++)
            {
                _prg.Add(
                    OpCode.LdaAbsolute,
                    0x4017);

                _prg.Add(
                    OpCode.LsrAccumulator);

                _prg.Add(
                    OpCode.RolZeroPage,
                    Controller2Address);
            }
        }

        private void A1()
        {
            EmitButton(Controller1Address, 0x80);
        }

        private void B1()
        {
            EmitButton(Controller1Address, 0x40);
        }

        private void Select1()
        {
            EmitButton(Controller1Address, 0x20);
        }

        private void Start1()
        {
            EmitButton(Controller1Address, 0x10);
        }

        private void Up1()
        {
            EmitButton(Controller1Address, 0x08);
        }

        private void Down1()
        {
            EmitButton(Controller1Address, 0x04);
        }

        private void Left1()
        {
            EmitButton(Controller1Address, 0x02);
        }

        private void Right1()
        {
            EmitButton(Controller1Address, 0x01);
        }

        private void EmitButton(
    byte controllerAddress,
    byte mask)
        {
            _prg.Add(
                OpCode.LdaZeroPage,
                controllerAddress);

            _prg.Add(
                OpCode.AndImmediate,
                mask);
        }
    }
}