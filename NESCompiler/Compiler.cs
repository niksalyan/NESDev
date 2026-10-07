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

        _compilerFunctions = new CompilerFunctions(_prg);

        

        BuildProgram();

        Debug.WriteLine($"PRG size: {_prg.ToBytecode().Length} bytes");

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
        _prg.Add(OpCode.Sei);
        _prg.Add(OpCode.Cld);

        // This is the instruction we want BPL to jump back to.
        int waitVBlank = _prg.Instructions.Count;

        _prg.Add(OpCode.BitAbsolute, 0x2002);

        // Target instruction index.
        _prg.Add(OpCode.Bpl, waitVBlank);

        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2000);

        _prg.PpuAddress(0x3F00);

        _prg.WritePpu(0x0F);
        _prg.WritePpu(0x30);
        _prg.WritePpu(0x20);
        _prg.WritePpu(0x10);

        _prg.PpuAddress(0x2090);

        Node ast = _parser.ParseScript(_src);
        Visit(ast);

        

        /*_prg.WritePpu(0);
        _prg.WritePpu(243);
        _prg.WritePpu(128);*/

        _prg.Add(OpCode.LdaAbsolute, 0x2002);

        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2005);
        _prg.Add(OpCode.StaAbsolute, 0x2005);

        _prg.Add(OpCode.LdaImmediate, 0x08);
        _prg.Add(OpCode.StaAbsolute, 0x2001);

        // Jump back to the first instruction.
        _prg.Add(OpCode.JmpAbsolute, 0);
    }

    protected override object? VisitVariableDeclaration(
    VariableDeclaration variableDeclaration)
    {
        foreach (var declaration in variableDeclaration.Declarations)
        {
            if (declaration.Id is not Identifier identifier)
                throw new NotSupportedException(
                    "Only simple variables are supported.");

            if (declaration.Init == null)
                throw new InvalidOperationException(
                    $"Variable '{identifier.Name}' must have a value.");


            var variable = _prg.DeclareVariable(identifier.Name, VariableType.Byte, VariableDeclarationKind.Var);
            EmitByteValue(declaration.Init, variable);

        }

        return null;
    }

    private void VisitExpression(BinaryExpression expression)
    {
        LoadByte(expression.Left);

        switch (expression.Operator)
        {
            case BinaryOperator.Plus:
                _prg.Add(OpCode.Clc);
                LoadByte(expression.Right);
                _prg.Add(OpCode.AdcZeroPage, GetVariable(
                    ((Identifier)expression.Right).Name).Address);
                break;

            case BinaryOperator.Minus:
                _prg.Add(OpCode.Sec);
                LoadByte(expression.Right);
                _prg.Add(OpCode.SbcZeroPage, GetVariable(
                    ((Identifier)expression.Right).Name).Address);
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported operator: {expression.Operator}");
        }
    }

    private void VisitIf(IfStatement ifStatement)
    {
        EmitCondition(ifStatement.Test);

        int skipBody = _prg.Instructions.Count;

        // We don't know the target yet.
        // EmitCondition will eventually branch here.

        Visit(ifStatement.Consequent);

        // Later:
        // branch operand = skipBody
    }

    protected override object? VisitCallExpression(
    CallExpression callExpression)
    {
        if (callExpression.Callee is not Identifier identifier)
            throw new InvalidOperationException(
                "Only named function calls are supported.");

        string functionName = identifier.Name;

        // ------------------------------------------------------------
        // User-defined function / subroutine
        // ------------------------------------------------------------

        /*if (_functions.TryGetValue(
                functionName,
                out int userFunctionIndex))
        {
            if (callExpression.Arguments.Count != 0)
                throw new InvalidOperationException(
                    $"Function '{functionName}' does not accept arguments.");

            Add(
                OpCode.CallSubroutine,
                userFunctionIndex);

            return VariableType.None;
        }*/

        // ------------------------------------------------------------
        // VM function
        // ------------------------------------------------------------

        var arguments = new List<object?>();

        foreach (var argument in callExpression.Arguments)
        {
            arguments.Add(Visit(argument));
        }

        _compilerFunctions.Execute(
            functionName,
            arguments.ToArray());

        // Call function here

        return VariableType.None;
    }

    private void EmitByteValue(
    Expression expression,
    Variable destination)
    {
        switch (expression)
        {
            case Literal literal:
                {
                    byte value = Convert.ToByte(literal.Value);

                    _prg.Add(
                        OpCode.LdaImmediate,
                        value);

                    break;
                }

            case Identifier identifier:
                {
                    var source = _prg.GetVariable(identifier.Name);

                    _prg.Add(
                        OpCode.LdaZeroPage,
                        source.Address);

                    break;
                }

            default:
                throw new NotSupportedException(
                    $"Expression '{expression.GetType().Name}' " +
                    "is not supported yet.");
        }

        _prg.Add(
            OpCode.StaZeroPage,
            destination.Address);
    }

    public string GetText(Acornima.Range range)
    {
        return _src.Substring(range.Start, range.Length);
    }

}