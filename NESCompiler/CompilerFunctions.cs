
using Acornima.Ast;
using System;
using System.Collections.Generic;

namespace NESCompiler
{
    public class CompilerFunctions
    {
        private readonly Bytecode _prg;

        private IReadOnlyList<Expression> _arguments =
            Array.Empty<Expression>();

        private const byte Controller1Address = 0xEE;
        private const byte Controller2Address = 0xED;

        // Dedicated hardware-calculation scratch byte.
        // Must not overlap compiler variables or other reserved RAM.
        private const byte HardwareTemp = 0xEF;

        public CompilerFunctions(Compiler compiler, Bytecode prg)
        {
            _prg = prg;
        }

        // ============================================================
        // Function dispatcher
        // ============================================================

        public VariableType Execute(
            string name,
            IReadOnlyList<Expression>? args = null)
        {
            _arguments = args ?? Array.Empty<Expression>();

            switch (name)
            {
                case "scroll":
                    Scroll();
                    return VariableType.None;

                case "print":
                    Print();
                    return VariableType.None;

                case "tileAt":
                    TileAt();
                    return VariableType.None;

                case "sprite":
                    Sprite();
                    return VariableType.None;
                case "sprite1x3":
                    Sprites(1, 3, args);
                    return VariableType.None;
                case "sprite2x2":
                    Sprites(2, 2, args);
                    return VariableType.None;
                case "sync":
                    RequireArguments(0, "sync");
                    Input();
                    EmitWaitVBlank();
                    return VariableType.None;

                case "A1":
                    A(Controller1Address);
                    return VariableType.Byte;

                case "B1":
                    B(Controller1Address);
                    return VariableType.Byte;

                case "START1":
                    Start(Controller1Address);
                    return VariableType.Byte;

                case "SELECT1":
                    Select(Controller1Address);
                    return VariableType.Byte;

                case "UP1":
                    Up(Controller1Address);
                    return VariableType.Byte;

                case "DOWN1":
                    Down(Controller1Address);
                    return VariableType.Byte;

                case "LEFT1":
                    Left(Controller1Address);
                    return VariableType.Byte;

                case "RIGHT1":
                    Right(Controller1Address);
                    return VariableType.Byte;

                case "A2":
                    A(Controller2Address);
                    return VariableType.Byte;

                case "B2":
                    B(Controller2Address);
                    return VariableType.Byte;

                case "START2":
                    Start(Controller2Address);
                    return VariableType.Byte;

                case "SELECT2":
                    Select(Controller2Address);
                    return VariableType.Byte;

                case "UP2":
                    Up(Controller2Address);
                    return VariableType.Byte;

                case "DOWN2":
                    Down(Controller2Address);
                    return VariableType.Byte;

                case "LEFT2":
                    Left(Controller2Address);
                    return VariableType.Byte;

                case "RIGHT2":
                    Right(Controller2Address);
                    return VariableType.Byte;

                default:
                    throw new NotSupportedException(
                        $"Compiler function '{name}' is not supported.");
            }
        }

        // ============================================================
        // Arguments
        // ============================================================

        private void RequireArguments(int count, string functionName)
        {
            if (_arguments.Count != count)
            {
                throw new ArgumentException(
                    $"{functionName}() requires exactly {count} arguments.");
            }
        }

        private Expression GetFunctionArgument(int argumentIndex)
        {
            if (argumentIndex < 0 ||
                argumentIndex >= _arguments.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(argumentIndex),
                    $"Function argument {argumentIndex} does not exist.");
            }

            return _arguments[argumentIndex];
        }

        private void EmitByteArgument(
            int argumentIndex,
            string functionName,
            string argumentName)
        {
            Expression argument =
                GetFunctionArgument(argumentIndex);

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
                    throw new NotSupportedException(
                        $"{functionName}() argument '{argumentName}' " +
                        "must be a numeric literal or variable.");
            }
        }

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

            int value = Convert.ToInt32(literal.Value);

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
            int value = GetLiteralByte(
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
        // Scroll
        //
        // scroll(x, y)
        // ============================================================

        private void Scroll()
        {
            RequireArguments(2, "scroll");

            // Reset the PPU scroll latch.
            _prg.Add(OpCode.LdaAbsolute, 0x2002);

            EmitByteArgument(0, "scroll", "x");
            _prg.Add(OpCode.StaAbsolute, 0x2005);

            EmitByteArgument(1, "scroll", "y");
            _prg.Add(OpCode.StaAbsolute, 0x2005);
        }

        // ============================================================
        // TileAt
        //
        // tileAt(x, y, tile)
        //
        // x/y/tile may be numeric literals or variables.
        // ============================================================

        private void TileAt()
        {
            RequireArguments(3, "tileAt");

            Expression x = GetFunctionArgument(0);
            Expression y = GetFunctionArgument(1);

            bool xLiteral = x is NumericLiteral;
            bool yLiteral = y is NumericLiteral;

            // Both coordinates are compile-time constants.
            if (xLiteral && yLiteral)
            {
                int xv = GetLiteralByte(x, "tileAt", "x");
                int yv = GetTileY(y, "tileAt", "y");

                if (xv > 31)
                {
                    throw new ArgumentOutOfRangeException(
                        "x", xv, "Tile X must be between 0 and 31.");
                }

                ushort address =
                    (ushort)(0x2000 + yv * 32 + xv);

                _prg.PpuAddress(address);

                EmitByteArgument(2, "tileAt", "tile");
                _prg.Add(OpCode.StaAbsolute, 0x2007);

                _prg.PpuAddress(0x2000);
                return;
            }

            // Variable X, constant Y.
            if (!xLiteral && yLiteral)
            {
                int yv = GetTileY(y, "tileAt", "y");

                int baseAddress = 0x2000 + yv * 32;

                // PPU address high byte.
                _prg.Add(
                    OpCode.LdaImmediate,
                    (byte)(baseAddress >> 8));

                _prg.Add(OpCode.StaAbsolute, 0x2006);

                // Low byte = base low byte + X.
                _prg.Add(OpCode.LdaImmediate, 0);

                EmitByteArgument(0, "tileAt", "x");

                if ((baseAddress & 0xFF) != 0)
                {
                    // Preserve X while adding the base address.
                    _prg.Add(OpCode.Clc);
                    _prg.Add(
                        OpCode.AdcImmediate,
                        (byte)(baseAddress & 0xFF));
                }

                _prg.Add(OpCode.StaAbsolute, 0x2006);

                EmitByteArgument(2, "tileAt", "tile");
                _prg.Add(OpCode.StaAbsolute, 0x2007);

                _prg.PpuAddress(0x2000);
                return;
            }

            // Constant X, variable Y.
            if (xLiteral && !yLiteral)
            {
                int xv = GetLiteralByte(x, "tileAt", "x");

                if (xv > 31)
                {
                    throw new ArgumentOutOfRangeException(
                        "x", xv, "Tile X must be between 0 and 31.");
                }

                EmitByteArgument(1, "tileAt", "y");

                // Save Y while calculating the PPU address.
                _prg.Add(OpCode.StaZeroPage, HardwareTemp);

                // High byte = $20 + (Y >> 3).
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.LsrAccumulator);

                _prg.Add(OpCode.Clc);
                _prg.Add(OpCode.AdcImmediate, 0x20);
                _prg.Add(OpCode.StaAbsolute, 0x2006);

                // Low byte = ((Y & 7) << 5) + X.
                _prg.Add(OpCode.LdaZeroPage, HardwareTemp);
                _prg.Add(OpCode.AndImmediate, 0x07);

                for (int i = 0; i < 5; i++)
                    _prg.Add(OpCode.AslAccumulator);

                if (xv != 0)
                {
                    _prg.Add(OpCode.Clc);
                    _prg.Add(
                        OpCode.AdcImmediate,
                        (byte)xv);
                }

                _prg.Add(OpCode.StaAbsolute, 0x2006);

                EmitByteArgument(2, "tileAt", "tile");
                _prg.Add(OpCode.StaAbsolute, 0x2007);

                _prg.PpuAddress(0x2000);
                return;
            }

            // Variable X and variable Y.
            EmitByteArgument(1, "tileAt", "y");

            // Save Y for the low-byte calculation.
            _prg.Add(OpCode.StaZeroPage, HardwareTemp);

            // High byte = $20 + (Y >> 3).
            _prg.Add(OpCode.LsrAccumulator);
            _prg.Add(OpCode.LsrAccumulator);
            _prg.Add(OpCode.LsrAccumulator);

            _prg.Add(OpCode.Clc);
            _prg.Add(OpCode.AdcImmediate, 0x20);
            _prg.Add(OpCode.StaAbsolute, 0x2006);

            // Low byte = ((Y & 7) << 5) + X.
            _prg.Add(OpCode.LdaZeroPage, HardwareTemp);
            _prg.Add(OpCode.AndImmediate, 0x07);

            for (int i = 0; i < 5; i++)
                _prg.Add(OpCode.AslAccumulator);

            // Preserve the low-byte base.
            _prg.Add(OpCode.StaZeroPage, HardwareTemp);

            EmitByteArgument(0, "tileAt", "x");

            _prg.Add(OpCode.Clc);
            _prg.Add(OpCode.AdcZeroPage, HardwareTemp);
            _prg.Add(OpCode.StaAbsolute, 0x2006);

            EmitByteArgument(2, "tileAt", "tile");
            _prg.Add(OpCode.StaAbsolute, 0x2007);

            _prg.PpuAddress(0x2000);
        }

        // ============================================================
        // Sprite
        //
        // sprite(index, tile, x, y)
        //
        // Index is a numeric literal.
        // Tile, X and Y may be literals or variables.
        // ============================================================

        private void Sprite()
        {
            RequireArguments(4, "sprite");

            Expression indexExpression = GetFunctionArgument(0);

            if (indexExpression is not NumericLiteral indexLiteral)
            {
                throw new ArgumentException(
                    "sprite() index must be a numeric literal.");
            }

            int index = Convert.ToInt32(indexLiteral.Value);

            if (index < 0 || index >= 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    "Sprite index must be between 0 and 63.");
            }

            // OAMADDR = spriteIndex * 4.
            _prg.Add(
                OpCode.LdaImmediate,
                (byte)(index * 4));

            _prg.Add(OpCode.StaAbsolute, 0x2003);

            // NES OAM order: Y, tile, attributes, X.

            // Y = argument 2.
            EmitByteArgument(2, "sprite", "y");
            _prg.Add(OpCode.StaAbsolute, 0x2004);

            // Tile = argument 3 (charindex).
            EmitByteArgument(3, "sprite", "charindex");
            _prg.Add(OpCode.StaAbsolute, 0x2004);

            // Attributes = 0.
            _prg.Add(OpCode.LdaImmediate, 0x00);
            _prg.Add(OpCode.StaAbsolute, 0x2004);

            // X = argument 1.
            EmitByteArgument(1, "sprite", "x");
            _prg.Add(OpCode.StaAbsolute, 0x2004);
        }


        private void Sprites(
            int xSprites,
            int ySprites,
            IReadOnlyList<Expression>? args)
        {
           

            int spriteCount = xSprites * ySprites;
            string functionName = "sprite" + xSprites + "x" + ySprites;

            RequireArguments(3 + spriteCount, functionName);

            if (args![0] is not NumericLiteral indexLiteral)
            {
                throw new ArgumentException(
                    $"{functionName}() sprite index must be a numeric literal.");
            }

            int spriteIndex = Convert.ToInt32(indexLiteral.Value);

            if (spriteIndex < 0 || spriteIndex + spriteCount > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spriteIndex),
                    "The sprite layout must fit within the 64 OAM entries.");
            }

            for (int row = 0; row < ySprites; row++)
            {
                for (int col = 0; col < xSprites; col++)
                {
                    int currentIndex = spriteIndex + row * xSprites + col;
                    int tileArgumentIndex = 3 + row * xSprites + col;

                    // Set OAM address.
                    _prg.Add(
                        OpCode.LdaImmediate,
                        (byte)(currentIndex * 4));
                    _prg.Add(OpCode.StaAbsolute, 0x2003);

                    // Y coordinate = y + row * 8.
                    EmitByteArgument(2, functionName, "y");

                    int yOffset = row * 8;
                    if (yOffset != 0)
                    {
                        _prg.Add(OpCode.Clc);
                        _prg.Add(OpCode.AdcImmediate, (byte)yOffset);
                    }

                    _prg.Add(OpCode.StaAbsolute, 0x2004);

                    // Tile.
                    EmitByteArgument(
                        tileArgumentIndex,
                        functionName,
                        $"tile{row * xSprites + col + 1}");
                    _prg.Add(OpCode.StaAbsolute, 0x2004);

                    // Attributes = 0.
                    _prg.Add(OpCode.LdaImmediate, 0);
                    _prg.Add(OpCode.StaAbsolute, 0x2004);

                    // X coordinate = x + col * 8.
                    EmitByteArgument(1, functionName, "x");

                    int xOffset = col * 8;
                    if (xOffset != 0)
                    {
                        _prg.Add(OpCode.Clc);
                        _prg.Add(OpCode.AdcImmediate, (byte)xOffset);
                    }

                    _prg.Add(OpCode.StaAbsolute, 0x2004);
                }
            }
        }


        // ============================================================
        // Print
        //
        // print("HELLO")
        //
        // String literals are the only supported argument type here.
        // ============================================================

        private void Print()
        {
            RequireArguments(1, "print");

            if (GetFunctionArgument(0) is not StringLiteral text)
            {
                throw new ArgumentException(
                    "print() requires a string literal.");
            }

            foreach (char c in text.Value)
            {
                _prg.WritePpu((byte)(c - 32));
            }
        }

        // ============================================================
        // VBlank
        // ============================================================

        private void EmitWaitVBlank()
        {
            int waitStart = _prg.Instructions.Count;

            _prg.Add(OpCode.BitAbsolute, 0x2002);
            _prg.Add(OpCode.Bpl, waitStart);
        }

        // ============================================================
        // Controller input
        // ============================================================

        private void Input()
        {
            // Latch controllers.
            _prg.Add(OpCode.LdaImmediate, 0x01);
            _prg.Add(OpCode.StaAbsolute, 0x4016);

            _prg.Add(OpCode.LdaImmediate, 0x00);
            _prg.Add(OpCode.StaAbsolute, 0x4016);

            // Clear controller state.
            _prg.Add(OpCode.LdaImmediate, 0x00);
            _prg.Add(OpCode.StaZeroPage, Controller1Address);
            _prg.Add(OpCode.StaZeroPage, Controller2Address);

            // Controller 1.
            for (int i = 0; i < 8; i++)
            {
                _prg.Add(OpCode.LdaAbsolute, 0x4016);
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.RolZeroPage, Controller1Address);
            }

            // Controller 2.
            for (int i = 0; i < 8; i++)
            {
                _prg.Add(OpCode.LdaAbsolute, 0x4017);
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.RolZeroPage, Controller2Address);
            }
        }

        // ============================================================
        // Controller buttons
        // ============================================================

        private void A(byte address) =>
            EmitButton(address, 0x80);

        private void B(byte address) =>
            EmitButton(address, 0x40);

        private void Select(byte address) =>
            EmitButton(address, 0x20);

        private void Start(byte address) =>
            EmitButton(address, 0x10);

        private void Up(byte address) =>
            EmitButton(address, 0x08);

        private void Down(byte address) =>
            EmitButton(address, 0x04);

        private void Left(byte address) =>
            EmitButton(address, 0x02);

        private void Right(byte address) =>
            EmitButton(address, 0x01);

        private void EmitButton(byte controllerAddress, byte mask)
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
