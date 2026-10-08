using Acornima.Ast;
using System;
using System.Collections.Generic;

namespace NESCompiler
{
    public class CompilerFunctions
    {
        private readonly Compiler _compiler;
        private readonly Bytecode _prg;

        private const byte Controller1Address = 0xEE;
        private const byte Controller2Address = 0xED;

        // Existing compiler temporary.
        // Used only by hardware intrinsics when two runtime
        // values must temporarily coexist.
        private const byte ExpressionTemp = 0xEF;

        public CompilerFunctions(
            Compiler compiler,
            Bytecode prg)
        {
            _compiler = compiler;
            _prg = prg;
        }

        // ============================================================
        // Function dispatcher
        // ============================================================

        public VariableType Execute(
            string name,
            IReadOnlyList<Expression>? args = null)
        {
            switch (name)
            {
                case "scroll":
                    Scroll(args);
                    return VariableType.None;

                case "print":
                    Print(args);
                    return VariableType.None;

                case "tileAt":
                    TileAt(args);
                    return VariableType.None;

                case "sprite":
                    Sprite(args);
                    return VariableType.None;

                case "frame":
                    RequireNoArguments(args, "frame");
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

        // ============================================================
        // Argument validation
        // ============================================================

        private static void RequireArguments(
            IReadOnlyList<Expression>? args,
            int count,
            string functionName)
        {
            if (args == null || args.Count != count)
            {
                throw new ArgumentException(
                    $"{functionName} requires exactly {count} arguments.");
            }
        }

        private static void RequireNoArguments(
            IReadOnlyList<Expression>? args,
            string functionName)
        {
            if (args != null && args.Count != 0)
            {
                throw new ArgumentException(
                    $"{functionName} does not accept arguments.");
            }
        }

        // ============================================================
        // Hardware byte argument
        //
        // Allowed:
        //     literal
        //     variable
        //
        // Not allowed:
        //     x + 1
        //     x * 2
        //     foo()
        //
        // IMPORTANT:
        // An identifier is resolved directly to its RAM address.
        // No expression stack is involved.
        // ============================================================

        private void EmitByteArgument(
            Expression argument,
            string functionName,
            string argumentName)
        {
            switch (argument)
            {
                case NumericLiteral literal:
                    {
                        int value = Convert.ToInt32(literal.Value);

                        if (value < 0 || value > 255)
                        {
                            throw new ArgumentOutOfRangeException(
                                argumentName,
                                value,
                                $"{functionName}() argument '{argumentName}' " +
                                "must be between 0 and 255.");
                        }

                        _prg.Add(
                            OpCode.LdaImmediate,
                            (byte)value);

                        return;
                    }

                case Identifier identifier:
                    {
                        Variable variable =
                            _prg.GetVariable(identifier.Name);

                        if (variable.Address <= 0xFF)
                        {
                            _prg.Add(
                                OpCode.LdaZeroPage,
                                variable.Address);
                        }
                        else
                        {
                            _prg.Add(
                                OpCode.LdaAbsolute,
                                variable.Address);
                        }

                        return;
                    }

                default:
                    throw new ArgumentException(
                        $"{functionName}() argument '{argumentName}' " +
                        "must be a literal or variable.");
            }
        }

        // ============================================================
        // Scroll
        //
        // scroll(x, y)
        //
        // x/y:
        //     literal
        //     variable
        //
        // No expressions.
        // ============================================================

        private void Scroll(
            IReadOnlyList<Expression>? args)
        {
            RequireArguments(args, 2, "scroll");

            // Reset $2005 latch.
            _prg.Add(
                OpCode.LdaAbsolute,
                0x2002);

            EmitByteArgument(
                args![0],
                "scroll",
                "x");

            _prg.Add(
                OpCode.StaAbsolute,
                0x2005);

            EmitByteArgument(
                args[1],
                "scroll",
                "y");

            _prg.Add(
                OpCode.StaAbsolute,
                0x2005);
        }

        // ============================================================
        // TileAt
        //
        // tileAt(x, y, tile)
        //
        // x/y/tile:
        //     literal
        //     variable
        //
        // No expressions.
        //
        // Address:
        //
        //     $2000 + y * 32 + x
        //
        // We specialize the four possible cases:
        //
        //     literal/literal
        //     variable/literal
        //     literal/variable
        //     variable/variable
        //
        // This prevents unnecessary runtime math.
        // ============================================================

        private void TileAt(
            IReadOnlyList<Expression>? args)
        {
            RequireArguments(args, 3, "tileAt");

            Expression x = args![0];
            Expression y = args[1];
            Expression tile = args[2];

            bool xLiteral = x is NumericLiteral;
            bool yLiteral = y is NumericLiteral;

            // --------------------------------------------------------
            // x = literal, y = literal
            //
            // Entire address known at compile time.
            //
            // Example:
            //
            // tileAt(11, 10, 128)
            //
            // becomes:
            //
            // PPUADDR($214B)
            // LDA #$80
            // STA $2007
            // --------------------------------------------------------

            if (xLiteral && yLiteral)
            {
                int xv = GetLiteralByte(
                    x,
                    "tileAt",
                    "x");

                int yv = GetTileY(
                    y,
                    "tileAt",
                    "y");

                ushort address =
                    (ushort)(0x2000 + yv * 32 + xv);

                _prg.PpuAddress(address);

                EmitByteArgument(
                    tile,
                    "tileAt",
                    "tile");

                _prg.Add(
                    OpCode.StaAbsolute,
                    0x2007);

                // Restore V so tileAt does not change scrolling.
                _prg.PpuAddress(0x2000);

                return;
            }

            // --------------------------------------------------------
            // x = variable, y = literal
            //
            // Only x is dynamic.
            //
            // address = fixedBase + x
            //
            // Because x is 0..31, this cannot cross a page boundary.
            // --------------------------------------------------------

            if (!xLiteral && yLiteral)
            {
                int yv = GetTileY(
                    y,
                    "tileAt",
                    "y");

                int baseAddress =
                    0x2000 + yv * 32;

                // High byte is known.
                _prg.Add(
                    OpCode.LdaImmediate,
                    (byte)(baseAddress >> 8));

                _prg.Add(
                    OpCode.StaAbsolute,
                    0x2006);

                // x
                EmitByteArgument(
                    x,
                    "tileAt",
                    "x");

                // Low byte = base low + x.
                _prg.Add(
                    OpCode.Clc);

                if ((baseAddress & 0xFF) == 0)
                {
                    // Nothing to add.
                }
                else
                {
                    _prg.Add(
                        OpCode.AdcImmediate,
                        (byte)(baseAddress & 0xFF));
                }

                _prg.Add(
                    OpCode.StaAbsolute,
                    0x2006);

                // tile
                EmitByteArgument(
                    tile,
                    "tileAt",
                    "tile");

                _prg.Add(
                    OpCode.StaAbsolute,
                    0x2007);

                _prg.PpuAddress(0x2000);

                return;
            }

            // --------------------------------------------------------
            // x = literal, y = variable
            //
            // Need to calculate:
            //
            //     high = $20 + (y >> 3)
            //     low  = (y & 7) << 5 + x
            //
            // Only y is dynamic.
            // --------------------------------------------------------

            if (xLiteral && !yLiteral)
            {
                int xv = GetLiteralByte(
                    x,
                    "tileAt",
                    "x");

                // y -> A
                EmitByteArgument(
                    y,
                    "tileAt",
                    "y");

                // Save y.
                _prg.Add(
                    OpCode.StaZeroPage,
                    ExpressionTemp);

                // y >> 3
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.LsrAccumulator);

                // $20 + (y >> 3)
                _prg.Add(
                    OpCode.Clc);

                _prg.Add(
                    OpCode.AdcImmediate,
                    0x20);

                // PPUADDR high
                _prg.Add(
                    OpCode.StaAbsolute,
                    0x2006);

                // Restore y.
                _prg.Add(
                    OpCode.LdaZeroPage,
                    ExpressionTemp);

                // y & 7
                _prg.Add(
                    OpCode.AndImmediate,
                    0x07);

                // * 32
                _prg.Add(OpCode.AslAccumulator);
                _prg.Add(OpCode.AslAccumulator);
                _prg.Add(OpCode.AslAccumulator);
                _prg.Add(OpCode.AslAccumulator);
                _prg.Add(OpCode.AslAccumulator);

                // Add constant x.
                if (xv != 0)
                {
                    _prg.Add(
                        OpCode.Clc);

                    _prg.Add(
                        OpCode.AdcImmediate,
                        (byte)xv);
                }

                // PPUADDR low
                _prg.Add(
                    OpCode.StaAbsolute,
                    0x2006);

                // tile
                EmitByteArgument(
                    tile,
                    "tileAt",
                    "tile");

                _prg.Add(
                    OpCode.StaAbsolute,
                    0x2007);

                _prg.PpuAddress(0x2000);

                return;
            }

            // --------------------------------------------------------
            // x = variable, y = variable
            //
            // Both values are dynamic.
            //
            // We must preserve one value while calculating the other.
            // ExpressionTemp is used only as a one-byte hardware scratch.
            // --------------------------------------------------------

            EmitByteArgument(
                y,
                "tileAt",
                "y");

            // Save y.
            _prg.Add(
                OpCode.StaZeroPage,
                ExpressionTemp);

            // high = $20 + (y >> 3)
            _prg.Add(OpCode.LsrAccumulator);
            _prg.Add(OpCode.LsrAccumulator);
            _prg.Add(OpCode.LsrAccumulator);

            _prg.Add(
                OpCode.Clc);

            _prg.Add(
                OpCode.AdcImmediate,
                0x20);

            // PPUADDR high
            _prg.Add(
                OpCode.StaAbsolute,
                0x2006);

            // Restore y.
            _prg.Add(
                OpCode.LdaZeroPage,
                ExpressionTemp);

            // y & 7
            _prg.Add(
                OpCode.AndImmediate,
                0x07);

            // * 32
            _prg.Add(OpCode.AslAccumulator);
            _prg.Add(OpCode.AslAccumulator);
            _prg.Add(OpCode.AslAccumulator);
            _prg.Add(OpCode.AslAccumulator);
            _prg.Add(OpCode.AslAccumulator);

            // Save low base.
            _prg.Add(
                OpCode.StaZeroPage,
                ExpressionTemp);

            // x
            EmitByteArgument(
                x,
                "tileAt",
                "x");

            // x + yLow
            _prg.Add(
                OpCode.Clc);

            _prg.Add(
                OpCode.AdcZeroPage,
                ExpressionTemp);

            // PPUADDR low
            _prg.Add(
                OpCode.StaAbsolute,
                0x2006);

            // tile
            EmitByteArgument(
                tile,
                "tileAt",
                "tile");

            _prg.Add(
                OpCode.StaAbsolute,
                0x2007);

            _prg.PpuAddress(0x2000);
        }

        // ============================================================
        // Tile helpers
        // ============================================================

        private static int GetLiteralByte(
            Expression expression,
            string functionName,
            string argumentName)
        {
            if (expression is not NumericLiteral literal)
            {
                throw new ArgumentException(
                    $"{functionName}() argument '{argumentName}' " +
                    "must be a numeric literal.");
            }

            int value =
                Convert.ToInt32(literal.Value);

            if (value < 0 || value > 255)
            {
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    value,
                    $"{functionName}() argument '{argumentName}' " +
                    "must be between 0 and 255.");
            }

            return value;
        }

        private static int GetTileY(
            Expression expression,
            string functionName,
            string argumentName)
        {
            int value =
                GetLiteralByte(
                    expression,
                    functionName,
                    argumentName);

            if (value > 29)
            {
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    value,
                    "Tile Y must be between 0 and 29.");
            }

            return value;
        }

        // ============================================================
        // Sprite
        //
        // sprite(index, tile, x, y)
        //
        // index must be literal.
        // tile/x/y may be literal or variable.
        //
        // IMPORTANT:
        // Variables are resolved directly to their memory addresses.
        // No expression stack.
        // ============================================================

        private void Sprite(
            IReadOnlyList<Expression>? args)
        {
            RequireArguments(args, 4, "sprite");

            if (args![0] is not NumericLiteral indexLiteral)
            {
                throw new ArgumentException(
                    "sprite() index must be a numeric literal.");
            }

            int index =
                Convert.ToInt32(indexLiteral.Value);

            if (index < 0 || index >= 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    "Sprite index must be between 0 and 63.");
            }

            // OAMADDR = index * 4
            _prg.Add(
                OpCode.LdaImmediate,
                (byte)(index * 4));

            _prg.Add(
                OpCode.StaAbsolute,
                0x2003);

            // --------------------------------------------------------
            // Y
            // --------------------------------------------------------

            EmitByteArgument(
                args[3],
                "sprite",
                "y");

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004);

            // --------------------------------------------------------
            // Tile
            // --------------------------------------------------------

            EmitByteArgument(
                args[1],
                "sprite",
                "tile");

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004);

            // --------------------------------------------------------
            // Attributes
            // --------------------------------------------------------

            _prg.Add(
                OpCode.LdaImmediate,
                0x00);

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004);

            // --------------------------------------------------------
            // X
            // --------------------------------------------------------

            EmitByteArgument(
                args[2],
                "sprite",
                "x");

            _prg.Add(
                OpCode.StaAbsolute,
                0x2004);
        }

        // ============================================================
        // Print
        //
        // print("HELLO")
        //
        // Text remains literal-only.
        // ============================================================

        private void Print(
            IReadOnlyList<Expression>? args)
        {
            RequireArguments(args, 1, "print");

            if (args![0] is not StringLiteral text)
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

        // ============================================================
        // Frame
        // ============================================================

        private void EmitWaitVBlank()
        {
            int waitStart =
                _prg.Instructions.Count;

            _prg.Add(
                OpCode.BitAbsolute,
                0x2002);

            _prg.Add(
                OpCode.Bpl,
                waitStart);
        }

        // ============================================================
        // Controller
        // ============================================================

        private void Input()
        {
            // Latch controllers.

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

            // Clear controller state.

            _prg.Add(
                OpCode.LdaImmediate,
                0x00);

            _prg.Add(
                OpCode.StaZeroPage,
                Controller1Address);

            _prg.Add(
                OpCode.StaZeroPage,
                Controller2Address);

            // Controller 1.

            for (int i = 0; i < 8; i++)
            {
                _prg.Add(
                    OpCode.LdaAbsolute,
                    0x4016);

                _prg.Add(
                    OpCode.LsrAccumulator);

                _prg.Add(
                    OpCode.RolZeroPage,
                    Controller1Address);
            }

            // Controller 2.

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

        // ============================================================
        // Controller buttons
        // ============================================================

        private void A1()
        {
            EmitButton(
                Controller1Address,
                0x80);
        }

        private void B1()
        {
            EmitButton(
                Controller1Address,
                0x40);
        }

        private void Select1()
        {
            EmitButton(
                Controller1Address,
                0x20);
        }

        private void Start1()
        {
            EmitButton(
                Controller1Address,
                0x10);
        }

        private void Up1()
        {
            EmitButton(
                Controller1Address,
                0x08);
        }

        private void Down1()
        {
            EmitButton(
                Controller1Address,
                0x04);
        }

        private void Left1()
        {
            EmitButton(
                Controller1Address,
                0x02);
        }

        private void Right1()
        {
            EmitButton(
                Controller1Address,
                0x01);
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