using Acornima;
using Acornima.Ast;
using System;
using System.Collections.Generic;

namespace NESCompiler
{
    public class CompilerFunctions
    {

        private readonly Compiler _compiler;
        private readonly Bytecode _prg;
        private IReadOnlyList<Expression> _arguments = Array.Empty<Expression>();

        private const byte Controller1Address = 0xEE;
        private const byte Controller2Address = 0xED;
        private const byte HardwareTemp = 0xEF;

        public CompilerFunctions(Compiler compiler, Bytecode prg)
        {
            _compiler = compiler;
            _prg = prg;
        }

        public VariableType Execute(string name, IReadOnlyList<Expression>? args = null)
        {
            _arguments = args ?? Array.Empty<Expression>();

            switch (name)
            {
                case "scroll": Scroll(); return VariableType.None;
                case "print": Print(); return VariableType.None;
                case "tileAt": TileAt(); return VariableType.None;
                case "sprite": Sprite(); return VariableType.None;
                case "sprite1x3": Sprites(1, 3); return VariableType.None;
                case "sprite2x2": Sprites(2, 2); return VariableType.None;
                case "sync":
                    RequireArguments(0, "sync");
                    Input();
                    EmitWaitVBlank();
                    return VariableType.None;
                case "backdrop":
                    Backdrop();
                    return VariableType.None;

                case "backgroundPalette":
                    WritePalette(0x3F00, "backgroundPalette");
                    return VariableType.None;

                case "spritePalette":
                    WritePalette(0x3F10, "spritePalette");
                    return VariableType.None;
                case "A1": A(Controller1Address); return VariableType.Byte;
                case "B1": B(Controller1Address); return VariableType.Byte;
                case "START1": Start(Controller1Address); return VariableType.Byte;
                case "SELECT1": Select(Controller1Address); return VariableType.Byte;
                case "UP1": Up(Controller1Address); return VariableType.Byte;
                case "DOWN1": Down(Controller1Address); return VariableType.Byte;
                case "LEFT1": Left(Controller1Address); return VariableType.Byte;
                case "RIGHT1": Right(Controller1Address); return VariableType.Byte;
                case "A2": A(Controller2Address); return VariableType.Byte;
                case "B2": B(Controller2Address); return VariableType.Byte;
                case "START2": Start(Controller2Address); return VariableType.Byte;
                case "SELECT2": Select(Controller2Address); return VariableType.Byte;
                case "UP2": Up(Controller2Address); return VariableType.Byte;
                case "DOWN2": Down(Controller2Address); return VariableType.Byte;
                case "LEFT2": Left(Controller2Address); return VariableType.Byte;
                case "RIGHT2": Right(Controller2Address); return VariableType.Byte;
                default:
                    throw new NotSupportedException($"Compiler function '{name}' is not supported.");
            }
        }

        // ------------------------------------------------------------
        // Argument helpers
        // ------------------------------------------------------------

        private void RequireArguments(int count, string functionName)
        {
            if (_arguments.Count != count)
                throw new ArgumentException($"{functionName}() requires exactly {count} arguments.");
        }

        private Expression GetFunctionArgument(int argumentIndex)
        {
            if (argumentIndex < 0 || argumentIndex >= _arguments.Count)
                throw new ArgumentOutOfRangeException(nameof(argumentIndex), $"Function argument {argumentIndex} does not exist.");

            return _arguments[argumentIndex];
        }

        private void EmitByteArgument(int argumentIndex, string functionName, string argumentName)
        {
            EmitByteArgument(GetFunctionArgument(argumentIndex), functionName, argumentName);
        }

        private void EmitByteArgument(Expression argument, string functionName, string argumentName)
        {
            switch (argument)
            {
                case NumericLiteral literal:
                    {
                        int value = Convert.ToInt32(literal.Value);
                        ValidateByte(value, functionName, argumentName);
                        _prg.Add(OpCode.LdaImmediate, (byte)value);
                        return;
                    }
                case Identifier identifier:
                    {
                        Variable variable = _prg.GetVariable(identifier.Name);
                        if (variable.Address <= 0xFF)
                            _prg.Add(OpCode.LdaZeroPage, variable.Address);
                        else
                            _prg.Add(OpCode.LdaAbsolute, variable.Address);
                        return;
                    }
                default:
                    throw new NotSupportedException(
                        $"{functionName}() argument '{argumentName}' must be a numeric literal or variable.");
            }
        }

        // Supports a literal, variable, or one flat operation:
        // variable + literal, variable - literal, literal + literal, literal - literal.
        // Nested expressions and variable-to-variable arithmetic are intentionally unsupported here.
        private void EmitByteExpression(int argumentIndex, string functionName, string argumentName)
        {
            Expression expression = GetFunctionArgument(argumentIndex);

            if (expression is not NonLogicalBinaryExpression binary)
            {
                EmitByteArgument(expression, functionName, argumentName);
                return;
            }

            if (binary.Left is not (Identifier or NumericLiteral))
                throw new NotSupportedException(
                    $"{functionName}() argument '{argumentName}' must have a simple left operand.");

            if (binary.Right is not NumericLiteral rightLiteral)
                throw new NotSupportedException(
                    $"{functionName}() argument '{argumentName}' must have a numeric literal on the right side of + or -.");

            int value = Convert.ToInt32(rightLiteral.Value);
            ValidateByte(value, functionName, argumentName);

            EmitByteArgument(binary.Left, functionName, argumentName);

            switch (binary.Operator)
            {
                case Operator.Addition:
                    _prg.Add(OpCode.Clc);
                    _prg.Add(OpCode.AdcImmediate, (byte)value);
                    break;
                case Operator.Subtraction:
                    _prg.Add(OpCode.Sec);
                    _prg.Add(OpCode.SbcImmediate, (byte)value);
                    break;
                default:
                    throw new NotSupportedException(
                        $"{functionName}() argument '{argumentName}' supports only + and - with a numeric literal.");
            }
        }

        private static void ValidateByte(int value, string functionName, string argumentName)
        {
            if (value < 0 || value > 255)
                throw new ArgumentOutOfRangeException(
                    argumentName, value,
                    $"{functionName}() argument '{argumentName}' must be between 0 and 255.");
        }

        private int GetLiteralByte(
                    Expression expression,
                    string functionName,
                    string argumentName)
        {
            int value;

            if (expression is NumericLiteral literal)
            {
                value = Convert.ToInt32(literal.Value);
            }
            else if (expression is Identifier identifier &&
                     _compiler.Consts.ContainsKey(identifier.Name))
            {
                value = _compiler.Consts[identifier.Name];
            }
            else
            {
                throw new ArgumentException(
                    $"{functionName}() argument '{argumentName}' " +
                    "must be a numeric literal or constant.");
            }

            ValidateByte(value, functionName, argumentName);
            return value;
        }

        private int GetTileY(Expression expression, string functionName, string argumentName)
        {
            int value = GetLiteralByte(expression, functionName, argumentName);
            if (value > 29)
                throw new ArgumentOutOfRangeException(argumentName, value, "Tile Y must be between 0 and 29.");
            return value;
        }

        // ------------------------------------------------------------
        // Scroll
        // scroll(x, y)
        // ------------------------------------------------------------

        private void Scroll()
        {
            RequireArguments(2, "scroll");
            _prg.Add(OpCode.LdaAbsolute, 0x2002); // Reset PPU scroll latch.
            EmitByteExpression(0, "scroll", "x");
            _prg.Add(OpCode.StaAbsolute, 0x2005);
            EmitByteExpression(1, "scroll", "y");
            _prg.Add(OpCode.StaAbsolute, 0x2005);
        }

        // ------------------------------------------------------------
        // TileAt
        // tileAt(x, y, tile)
        // ------------------------------------------------------------

        private void TileAt()
        {
            RequireArguments(3, "tileAt");
            Expression x = GetFunctionArgument(0);
            Expression y = GetFunctionArgument(1);
            bool xLiteral = x is NumericLiteral;
            bool yLiteral = y is NumericLiteral;

            if (xLiteral && yLiteral)
            {
                int xv = GetLiteralByte(x, "tileAt", "x");
                int yv = GetTileY(y, "tileAt", "y");
                if (xv > 31)
                    throw new ArgumentOutOfRangeException("x", xv, "Tile X must be between 0 and 31.");

                _prg.PpuAddress((ushort)(0x2000 + yv * 32 + xv));
                EmitByteExpression(2, "tileAt", "tile");
                _prg.Add(OpCode.StaAbsolute, 0x2007);
                _prg.PpuAddress(0x2000);
                return;
            }

            if (!xLiteral && yLiteral)
            {
                int yv = GetTileY(y, "tileAt", "y");
                int baseAddress = 0x2000 + yv * 32;
                _prg.Add(OpCode.LdaImmediate, (byte)(baseAddress >> 8));
                _prg.Add(OpCode.StaAbsolute, 0x2006);
                _prg.Add(OpCode.LdaImmediate, 0);
                EmitByteExpression(0, "tileAt", "x");

                if ((baseAddress & 0xFF) != 0)
                {
                    _prg.Add(OpCode.Clc);
                    _prg.Add(OpCode.AdcImmediate, (byte)(baseAddress & 0xFF));
                }

                _prg.Add(OpCode.StaAbsolute, 0x2006);
                EmitByteExpression(2, "tileAt", "tile");
                _prg.Add(OpCode.StaAbsolute, 0x2007);
                _prg.PpuAddress(0x2000);
                return;
            }

            if (xLiteral && !yLiteral)
            {
                int xv = GetLiteralByte(x, "tileAt", "x");
                if (xv > 31)
                    throw new ArgumentOutOfRangeException("x", xv, "Tile X must be between 0 and 31.");

                EmitByteExpression(1, "tileAt", "y");
                _prg.Add(OpCode.StaZeroPage, HardwareTemp);
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.Clc);
                _prg.Add(OpCode.AdcImmediate, 0x20);
                _prg.Add(OpCode.StaAbsolute, 0x2006);

                _prg.Add(OpCode.LdaZeroPage, HardwareTemp);
                _prg.Add(OpCode.AndImmediate, 0x07);
                for (int i = 0; i < 5; i++)
                    _prg.Add(OpCode.AslAccumulator);

                if (xv != 0)
                {
                    _prg.Add(OpCode.Clc);
                    _prg.Add(OpCode.AdcImmediate, (byte)xv);
                }

                _prg.Add(OpCode.StaAbsolute, 0x2006);
                EmitByteExpression(2, "tileAt", "tile");
                _prg.Add(OpCode.StaAbsolute, 0x2007);
                _prg.PpuAddress(0x2000);
                return;
            }

            // Variable X and variable Y.
            EmitByteExpression(1, "tileAt", "y");
            _prg.Add(OpCode.StaZeroPage, HardwareTemp);
            _prg.Add(OpCode.LsrAccumulator);
            _prg.Add(OpCode.LsrAccumulator);
            _prg.Add(OpCode.LsrAccumulator);
            _prg.Add(OpCode.Clc);
            _prg.Add(OpCode.AdcImmediate, 0x20);
            _prg.Add(OpCode.StaAbsolute, 0x2006);

            _prg.Add(OpCode.LdaZeroPage, HardwareTemp);
            _prg.Add(OpCode.AndImmediate, 0x07);
            for (int i = 0; i < 5; i++)
                _prg.Add(OpCode.AslAccumulator);

            _prg.Add(OpCode.StaZeroPage, HardwareTemp);
            EmitByteExpression(0, "tileAt", "x");
            _prg.Add(OpCode.Clc);
            _prg.Add(OpCode.AdcZeroPage, HardwareTemp);
            _prg.Add(OpCode.StaAbsolute, 0x2006);

            EmitByteExpression(2, "tileAt", "tile");
            _prg.Add(OpCode.StaAbsolute, 0x2007);
            _prg.PpuAddress(0x2000);
        }

        // ------------------------------------------------------------
        // Sprite functions
        // sprite(index, tile, x, y) keeps the existing argument layout:
        // index, x, y, charindex in emitted OAM order.
        // ------------------------------------------------------------

        private void Sprite()
        {
            RequireArguments(4, "sprite");
            if (GetFunctionArgument(0) is not NumericLiteral indexLiteral)
                throw new ArgumentException("sprite() index must be a numeric literal.");

            int index = Convert.ToInt32(indexLiteral.Value);
            if (index < 0 || index >= 64)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Sprite index must be between 0 and 63.");

            _prg.Add(OpCode.LdaImmediate, (byte)(index * 4));
            _prg.Add(OpCode.StaAbsolute, 0x2003);

            // OAM order: Y, tile, attributes, X.
            EmitByteExpression(2, "sprite", "y");
            _prg.Add(OpCode.StaAbsolute, 0x2004);
            EmitByteExpression(3, "sprite", "charindex");
            _prg.Add(OpCode.StaAbsolute, 0x2004);
            _prg.Add(OpCode.LdaImmediate, 0);
            _prg.Add(OpCode.StaAbsolute, 0x2004);
            EmitByteExpression(1, "sprite", "x");
            _prg.Add(OpCode.StaAbsolute, 0x2004);
        }

        private void Sprites(int xSprites, int ySprites)
        {
            int spriteCount = xSprites * ySprites;
            string functionName = $"sprite{xSprites}x{ySprites}";
            RequireArguments(3 + spriteCount, functionName);

            if (GetFunctionArgument(0) is not NumericLiteral indexLiteral)
                throw new ArgumentException($"{functionName}() sprite index must be a numeric literal.");

            int spriteIndex = Convert.ToInt32(indexLiteral.Value);
            if (spriteIndex < 0 || spriteIndex + spriteCount > 64)
                throw new ArgumentOutOfRangeException(nameof(spriteIndex), "The sprite layout must fit within the 64 OAM entries.");

            for (int row = 0; row < ySprites; row++)
            {
                for (int col = 0; col < xSprites; col++)
                {
                    int currentIndex = spriteIndex + row * xSprites + col;
                    int tileArgumentIndex = 3 + row * xSprites + col;

                    _prg.Add(OpCode.LdaImmediate, (byte)(currentIndex * 4));
                    _prg.Add(OpCode.StaAbsolute, 0x2003);

                    // Y coordinate = base Y expression + row * 8.
                    EmitByteExpression(2, functionName, "y");
                    int yOffset = row * 8;
                    if (yOffset != 0)
                    {
                        _prg.Add(OpCode.Clc);
                        _prg.Add(OpCode.AdcImmediate, (byte)yOffset);
                    }
                    _prg.Add(OpCode.StaAbsolute, 0x2004);

                    EmitByteExpression(tileArgumentIndex, functionName, $"tile{row * xSprites + col + 1}");
                    _prg.Add(OpCode.StaAbsolute, 0x2004);
                    _prg.Add(OpCode.LdaImmediate, 0);
                    _prg.Add(OpCode.StaAbsolute, 0x2004);

                    // X coordinate = base X expression + column * 8.
                    EmitByteExpression(1, functionName, "x");
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

        // ------------------------------------------------------------
        // Print
        // print("HELLO")
        // ------------------------------------------------------------

        private void Print()
        {
            RequireArguments(1, "print");
            if (GetFunctionArgument(0) is not StringLiteral text)
                throw new ArgumentException("print() requires a string literal.");

            foreach (char c in text.Value)
                _prg.WritePpu((byte)(c - 32));
        }

        // ------------------------------------------------------------
        // VBlank and controller input
        // ------------------------------------------------------------

        private void EmitWaitVBlank()
        {
            int waitStart = _prg.Instructions.Count;
            _prg.Add(OpCode.BitAbsolute, 0x2002);
            _prg.Add(OpCode.Bpl, waitStart);
        }

        private void Input()
        {
            _prg.Add(OpCode.LdaImmediate, 0x01);
            _prg.Add(OpCode.StaAbsolute, 0x4016);
            _prg.Add(OpCode.LdaImmediate, 0x00);
            _prg.Add(OpCode.StaAbsolute, 0x4016);

            _prg.Add(OpCode.LdaImmediate, 0x00);
            _prg.Add(OpCode.StaZeroPage, Controller1Address);
            _prg.Add(OpCode.StaZeroPage, Controller2Address);

            for (int i = 0; i < 8; i++)
            {
                _prg.Add(OpCode.LdaAbsolute, 0x4016);
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.RolZeroPage, Controller1Address);
            }

            for (int i = 0; i < 8; i++)
            {
                _prg.Add(OpCode.LdaAbsolute, 0x4017);
                _prg.Add(OpCode.LsrAccumulator);
                _prg.Add(OpCode.RolZeroPage, Controller2Address);
            }
        }

        // ------------------------------------------------------------
        // Palette function
        // ------------------------------------------------------------

        // ------------------------------------------------------------
        // Palette functions
        // backdrop(color)
        // backgroundPalette(index, color1, color2, color3)
        // spritePalette(index, color1, color2, color3)
        // ------------------------------------------------------------

        private void Backdrop()
        {
            RequireArguments(1, "backdrop");

            int color = GetLiteralByte(
                GetFunctionArgument(0),
                "backdrop",
                "color");

            ValidatePaletteColor(color, "backdrop", "color");

            // Universal background color.
            _prg.PpuAddress(0x3F00);
            _prg.WritePpu((byte)color);

            // Restore PPU address to the nametable area.
            _prg.PpuAddress(0x2000);
        }

        private void WritePalette(int baseAddress, string functionName)
        {
            RequireArguments(4, functionName);

            int paletteIndex = GetLiteralByte(
                GetFunctionArgument(0),
                functionName,
                "index");

            if (paletteIndex > 3)
            {
                throw new ArgumentOutOfRangeException(
                    "index",
                    paletteIndex,
                    $"{functionName}() index must be between 0 and 3.");
            }

            int color1 = GetLiteralByte(
                GetFunctionArgument(1), functionName, "color1");

            int color2 = GetLiteralByte(
                GetFunctionArgument(2), functionName, "color2");

            int color3 = GetLiteralByte(
                GetFunctionArgument(3), functionName, "color3");

            ValidatePaletteColor(color1, functionName, "color1");
            ValidatePaletteColor(color2, functionName, "color2");
            ValidatePaletteColor(color3, functionName, "color3");

            // Each palette occupies four entries.
            // Entry 0 is the shared backdrop color.
            int address = baseAddress + paletteIndex * 4;

            _prg.PpuAddress((ushort)(address + 1));
            _prg.WritePpu((byte)color1);
            _prg.WritePpu((byte)color2);
            _prg.WritePpu((byte)color3);

            _prg.PpuAddress(0x2000);
        }

        private static void ValidatePaletteColor(
            int color,
            string functionName,
            string argumentName)
        {
            if (color > 0x3F)
            {
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    color,
                    $"{functionName}() color must be between 0 and 63.");
            }
        }

        private void Palette(
    int baseAddress,
    int entryCount,
    string functionName)
        {
            RequireArguments(2, functionName);

            int index = GetLiteralByte(
                GetFunctionArgument(0),
                functionName,
                "index");

            int color = GetLiteralByte(
                GetFunctionArgument(1),
                functionName,
                "color");

            if (index >= entryCount)
            {
                throw new ArgumentOutOfRangeException(
                    "index",
                    index,
                    $"{functionName}() index must be between 0 and {entryCount - 1}.");
            }

            if (color > 0x3F)
            {
                throw new ArgumentOutOfRangeException(
                    "color",
                    color,
                    "NES palette colors must be between 0 and 63.");
            }

            _prg.PpuAddress((ushort)(baseAddress + index));
            _prg.WritePpu((byte)color);

            // Restore the PPU address to the nametable area.
            _prg.PpuAddress(0x2000);
        }

        // ------------------------------------------------------------
        // Controller buttons
        // ------------------------------------------------------------

        private void A(byte address) => EmitButton(address, 0x80);
        private void B(byte address) => EmitButton(address, 0x40);
        private void Select(byte address) => EmitButton(address, 0x20);
        private void Start(byte address) => EmitButton(address, 0x10);
        private void Up(byte address) => EmitButton(address, 0x08);
        private void Down(byte address) => EmitButton(address, 0x04);
        private void Left(byte address) => EmitButton(address, 0x02);
        private void Right(byte address) => EmitButton(address, 0x01);

        private void EmitButton(byte controllerAddress, byte mask)
        {
            _prg.Add(OpCode.LdaZeroPage, controllerAddress);
            _prg.Add(OpCode.AndImmediate, mask);
        }
    }
}
