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
    private const byte ExpressionStackBase = 0xF0;
    private const byte ExpressionStackSize = 16;

    private const byte ExprTempAddress = 0xEF;

    private byte _expressionStackDepth;

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
        _prg.Variables.Clear();

        

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
        _prg.Add(OpCode.LdxImmediate, 0);


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
            EmitExpression(declaration.Init);

            Pop();

            if (variable.Address <= 0xFF)
            {
                _prg.Add(
                    OpCode.StaZeroPage,
                    variable.Address);
            }
            else
            {
                _prg.Add(
                    OpCode.StaAbsolute,
                    variable.Address);
            }

        }

        return null;
    }

    protected override object? VisitBinaryExpression(
    BinaryExpression expression)
    {
        switch (expression.Operator)
        {
            case Operator.Addition:
                {
                    // Prefer generating: LDA <left>; CLC; ADC <right>
                    // Only use a scratch temp when the right-hand side is a complex expression.

                    if (expression.Right is Literal litRight)
                    {
                        LoadByte(expression.Left);
                        _prg.Add(OpCode.Clc);
                        _prg.Add(OpCode.AdcImmediate, Convert.ToByte(litRight.Value));
                        return null;
                    }

                    if (expression.Right is Identifier idRight)
                    {
                        LoadByte(expression.Left);
                        _prg.Add(OpCode.Clc);
                        var src = _prg.GetVariable(idRight.Name);
                        if (src.Address <= 0xFF)
                            _prg.Add(OpCode.AdcZeroPage, src.Address);
                        else
                            _prg.Add(OpCode.AdcAbsolute, src.Address);
                        return null;
                    }

                    // Right is complex: evaluate it into the fixed scratch at $0200, then add.
                    EmitExpressionToAddress(expression.Right, ExprTempAddress);

                    LoadByte(expression.Left);
                    _prg.Add(OpCode.Clc);
                    _prg.Add(OpCode.AdcAbsolute, ExprTempAddress);

                    return null;
                }

            case Operator.Subtraction:
                {
                    // Prefer: LDA <left>; SEC; SBC <right>

                    if (expression.Right is Literal litR)
                    {
                        LoadByte(expression.Left);
                        _prg.Add(OpCode.Sec);
                        _prg.Add(OpCode.SbcImmediate, Convert.ToByte(litR.Value));
                        return null;
                    }

                    if (expression.Right is Identifier idR)
                    {
                        LoadByte(expression.Left);
                        _prg.Add(OpCode.Sec);
                        var src = _prg.GetVariable(idR.Name);
                        if (src.Address <= 0xFF)
                            _prg.Add(OpCode.SbcZeroPage, src.Address);
                        else
                            _prg.Add(OpCode.SbcAbsolute, src.Address);
                        return null;
                    }

                    EmitExpressionToAddress(expression.Right, ExprTempAddress);

                    LoadByte(expression.Left);
                    _prg.Add(OpCode.Sec);
                    _prg.Add(OpCode.SbcAbsolute, ExprTempAddress);

                    return null;
                }

            default:
                throw new NotSupportedException(
                    $"Operator '{expression.Operator}' is not supported.");
        }
    }

    protected override object? VisitIfStatement(Acornima.Ast.IfStatement ifStatement)
    {
        // Special-case non-logical binary expressions (comparisons) because
        // the AST may represent them with a different node type.
        var test = ifStatement.Test;

        if (test.GetType().Name == "NonLogicalBinaryExpression")
        {
            // Use reflection to access Left, Right and Operator
            var leftProp = test.GetType().GetProperty("Left");
            var rightProp = test.GetType().GetProperty("Right");
            var opProp = test.GetType().GetProperty("Operator");

            if (leftProp == null || rightProp == null || opProp == null)
                throw new NotSupportedException("Unsupported test expression shape.");

            var left = (Expression)leftProp.GetValue(test)!;
            var right = (Expression)rightProp.GetValue(test)!;
            var op = opProp.GetValue(test)!;

            // Ensure accumulator contains left value.
            // If left is a simple expression, load it; otherwise let Visit(left) produce code that leaves A set.
            if (left is Literal || left is Identifier)
            {
                LoadByte(left);
            }
            else
            {
                Visit(left);
            }

            // Emit CMP depending on right kind
            if (right is Literal lit)
            {
                _prg.Add(OpCode.CmpImmediate, Convert.ToByte(lit.Value));
            }
            else if (right is Identifier id)
            {
                var src = _prg.GetVariable(id.Name);
                if (src.Address <= 0xFF)
                    _prg.Add(OpCode.CmpZeroPage, src.Address);
                else
                    _prg.Add(OpCode.CmpAbsolute, src.Address);
            }
            else
            {
                // Complex right: evaluate into fixed scratch then CMP scratch
                EmitExpressionToAddress(right, ExprTempAddress);
                _prg.Add(OpCode.CmpAbsolute, ExprTempAddress);
            }

            // Decide branch opcode based on operator name (best-effort)
            string opName = op.ToString().ToLowerInvariant();
            bool isEquality = opName.Contains("equal");
            bool isNegation = opName.Contains("not") || opName.Contains("inequal") || opName.Contains("neq");

            int branchIndex = _prg.Instructions.Count;
            if (isEquality && !isNegation)
            {
                // if (left == right) -> execute consequent; jump to else when comparison is false
                _prg.Add(OpCode.Bne, 0);
            }
            else if (isEquality && isNegation)
            {
                // if (left != right) -> execute consequent; jump to else when equal
                _prg.Add(OpCode.Beq, 0);
            }
            else
            {
                throw new NotSupportedException(
                    $"Comparison operator '{op}' is not supported in if tests.");
            }

            // Visit consequent
            Visit(ifStatement.Consequent);

            if (ifStatement.Alternate != null)
            {
                int jmpIndex = _prg.Instructions.Count;
                _prg.Add(OpCode.JmpAbsolute, 0);

                int elseStart = _prg.Instructions.Count;
                var branchOld = _prg.Instructions[branchIndex];
                _prg.Instructions[branchIndex] = new Instruction(branchOld.OpCode, elseStart);

                Visit(ifStatement.Alternate);

                int afterElse = _prg.Instructions.Count;
                var jmpOld = _prg.Instructions[jmpIndex];
                _prg.Instructions[jmpIndex] = new Instruction(jmpOld.OpCode, afterElse);
            }
            else
            {
                int afterConsequent = _prg.Instructions.Count;
                var branchOld2 = _prg.Instructions[branchIndex];
                _prg.Instructions[branchIndex] = new Instruction(branchOld2.OpCode, afterConsequent);
            }

            return null;
        }

        // Fallback: evaluate test into A and branch if zero (false)
        // If Visit(test) leaves a value in memory, ensure A contains the test value.
        Visit(test);
        try
        {
            LoadByte(test);
        }
        catch
        {
            // If LoadByte cannot handle the test node, assume Visit(test) left the result in A.
        }

        int beqIndex = _prg.Instructions.Count;
        _prg.Add(OpCode.Beq, 0); // jump when zero (false)

        Visit(ifStatement.Consequent);

        if (ifStatement.Alternate != null)
        {
            int jmpIndex = _prg.Instructions.Count;
            _prg.Add(OpCode.JmpAbsolute, 0);

            int elseStart = _prg.Instructions.Count;
            var beqOld = _prg.Instructions[beqIndex];
            _prg.Instructions[beqIndex] = new Instruction(beqOld.OpCode, elseStart);

            Visit(ifStatement.Alternate);

            int afterElse = _prg.Instructions.Count;
            var jmpOld = _prg.Instructions[jmpIndex];
            _prg.Instructions[jmpIndex] = new Instruction(jmpOld.OpCode, afterElse);
        }
        else
        {
            int afterConsequent = _prg.Instructions.Count;
            var beqOld2 = _prg.Instructions[beqIndex];
            _prg.Instructions[beqIndex] = new Instruction(beqOld2.OpCode, afterConsequent);
        }

        return null;
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

    // Evaluate expression and store accumulator A into the given address.
    private void EmitExpressionToAddress(Expression expression, int address)
    {
        switch (expression)
        {
            case Literal literal:
                _prg.Add(OpCode.LdaImmediate, Convert.ToByte(literal.Value));
                break;

            case Identifier identifier:
                var source = _prg.GetVariable(identifier.Name);
                if (source.Address <= 0xFF)
                    _prg.Add(OpCode.LdaZeroPage, source.Address);
                else
                    _prg.Add(OpCode.LdaAbsolute, source.Address);
                break;

            case BinaryExpression binary:
                Visit(binary);
                break;

            default:
                if (expression.GetType().Name == "NonLogicalBinaryExpression")
                {
                    // Reuse the same handling as in EmitByteValue's non-logical branch
                    var leftProp = expression.GetType().GetProperty("Left");
                    var rightProp = expression.GetType().GetProperty("Right");
                    var opProp = expression.GetType().GetProperty("Operator");

                    if (leftProp == null || rightProp == null || opProp == null)
                        throw new NotSupportedException("Unsupported non-logical binary expression shape.");

                    var left = (Expression)leftProp.GetValue(expression)!;
                    var right = (Expression)rightProp.GetValue(expression)!;
                    var op = opProp.GetValue(expression)!;

                    string opName = op.ToString().ToLowerInvariant();

                    if (opName.Contains("add") || opName.Contains("+"))
                    {
                        if (right is Literal litRight)
                        {
                            LoadByte(left);
                            _prg.Add(OpCode.Clc);
                            _prg.Add(OpCode.AdcImmediate, Convert.ToByte(litRight.Value));
                        }
                        else if (right is Identifier idRight)
                        {
                            LoadByte(left);
                            _prg.Add(OpCode.Clc);
                            var src = _prg.GetVariable(idRight.Name);
                            if (src.Address <= 0xFF)
                                _prg.Add(OpCode.AdcZeroPage, src.Address);
                            else
                                _prg.Add(OpCode.AdcAbsolute, src.Address);
                        }
                        else
                        {
                            EmitExpressionToAddress(right, ExprTempAddress);
                            LoadByte(left);
                            _prg.Add(OpCode.Clc);
                            _prg.Add(OpCode.AdcAbsolute, ExprTempAddress);
                        }

                        break;
                    }

                    if (opName.Contains("sub") || opName.Contains("-"))
                    {
                        if (right is Literal litR)
                        {
                            LoadByte(left);
                            _prg.Add(OpCode.Sec);
                            _prg.Add(OpCode.SbcImmediate, Convert.ToByte(litR.Value));
                        }
                        else if (right is Identifier idR)
                        {
                            LoadByte(left);
                            _prg.Add(OpCode.Sec);
                            var src = _prg.GetVariable(idR.Name);
                            if (src.Address <= 0xFF)
                                _prg.Add(OpCode.SbcZeroPage, src.Address);
                            else
                                _prg.Add(OpCode.SbcAbsolute, src.Address);
                        }
                        else
                        {
                            EmitExpressionToAddress(right, ExprTempAddress);
                            LoadByte(left);
                            _prg.Add(OpCode.Sec);
                            _prg.Add(OpCode.SbcAbsolute, ExprTempAddress);
                        }

                        break;
                    }

                    throw new NotSupportedException($"Operator '{op}' is not supported in non-logical binary expression.");
                }

                throw new NotSupportedException($"Expression '{expression.GetType().Name}' is not supported.");
        }

        // Store A into address
        if (address <= 0xFF)
            _prg.Add(OpCode.StaZeroPage, address);
        else
            _prg.Add(OpCode.StaAbsolute, address);
    }

    private void EmitExpression(Expression expression)
    {
        if (expression.GetType().Name == "NonLogicalBinaryExpression")
        {
            EmitNonLogicalBinaryExpression(expression);
            return;
        }

        switch (expression)
        {
            case Literal literal:
                _prg.Add(
                    OpCode.LdaImmediate,
                    Convert.ToByte(literal.Value));

                Push();
                return;

            case Identifier identifier:
                {
                    var variable = _prg.GetVariable(identifier.Name);

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

                    Push();
                    return;
                }

            case BinaryExpression binary:
                EmitBinaryExpression(binary);
                return;

            default:
                throw new NotSupportedException(
                    $"Expression '{expression.GetType().Name}' is not supported.");
        }
    }

    private void EmitBinaryExpression(BinaryExpression expression)
    {
        EmitExpression(expression.Left);
        EmitExpression(expression.Right);

        switch (expression.Operator)
        {
            case Operator.Addition:
                EmitAdd();
                break;

            case Operator.Subtraction:
                EmitSubtract();
                break;
            case Operator.Multiplication:
                EmitMultiply();
                break;

            default:
                throw new NotSupportedException(
                    $"Operator '{expression.Operator}' is not supported.");
        }
    }

    private void EmitAdd()
    {
        // Stack:
        // [left, right]

        Pop();       // A = right
        _prg.Add(OpCode.StaZeroPage, ExpressionTemp);

        Pop();       // A = left

        _prg.Add(OpCode.Clc);
        _prg.Add(OpCode.AdcZeroPage, ExpressionTemp);

        Push();
    }

    private void EmitSubtract()
    {
        // Stack:
        // [left, right]

        Pop();       // A = right
        _prg.Add(OpCode.StaZeroPage, ExpressionTemp);

        Pop();       // A = left

        _prg.Add(OpCode.Sec);
        _prg.Add(OpCode.SbcZeroPage, ExpressionTemp);

        Push();
    }

    private void EmitNonLogicalBinaryExpression(Expression expression)
    {
        var leftProp = expression.GetType().GetProperty("Left");
        var rightProp = expression.GetType().GetProperty("Right");
        var opProp = expression.GetType().GetProperty("Operator");

        if (leftProp == null || rightProp == null || opProp == null)
            throw new NotSupportedException(
                "Unsupported non-logical binary expression shape.");

        var left = (Expression)leftProp.GetValue(expression)!;
        var right = (Expression)rightProp.GetValue(expression)!;
        var op = opProp.GetValue(expression)!;

        EmitExpression(left);
        EmitExpression(right);

        string opName = op.ToString()!.ToLowerInvariant();

        if (opName.Contains("add") || opName.Contains("+"))
        {
            EmitAdd();
            return;
        }

        if (opName.Contains("sub") || opName.Contains("-"))
        {
            EmitSubtract();
            return;
        }

        throw new NotSupportedException(
            $"Operator '{op}' is not supported.");
    }

    private void Push()
    {
        _prg.Add(OpCode.StaZeroPageX, ExpressionStackBase);
        _prg.Add(OpCode.Inx);

        _expressionStackDepth++;

        if (_expressionStackDepth > ExpressionStackSize)
            throw new InvalidOperationException(
                "Expression stack overflow.");
    }

    private void Pop()
    {
        if (_expressionStackDepth == 0)
            throw new InvalidOperationException(
                "Expression stack underflow.");

        _prg.Add(OpCode.Dex);
        _prg.Add(OpCode.LdaZeroPageX, ExpressionStackBase);

        _expressionStackDepth--;
    }

    public string GetText(Acornima.Range range)
    {
        return _src.Substring(range.Start, range.Length);
    }

}