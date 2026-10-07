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

    private const byte ExpressionTemp = 0xEF;

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
        _expressionStackDepth = 0;
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

    protected override object? VisitIfStatement(
    Acornima.Ast.IfStatement ifStatement)
    {
        var test = ifStatement.Test;

        if (test is NonLogicalBinaryExpression binary)
        {
            EmitComparison(binary);

            var branchIndexes =
                EmitComparisonBranch(binary.Operator);


            // Consequent.
            Visit(ifStatement.Consequent);

            if (ifStatement.Alternate != null)
            {
                // Skip else after consequent.
                int jmpIndex = _prg.Instructions.Count;

                _prg.Add(
                    OpCode.JmpAbsolute,
                    0);

                // False condition goes here.
                int elseStart = _prg.Instructions.Count;

                foreach (int index in branchIndexes)
                {
                    var old = _prg.Instructions[index];

                    _prg.Instructions[index] =
                        new Instruction(
                            old.OpCode,
                            elseStart);
                }

                // Else.
                Visit(ifStatement.Alternate);

                // Patch jump over else.
                int afterElse = _prg.Instructions.Count;

                var jmpOld = _prg.Instructions[jmpIndex];

                _prg.Instructions[jmpIndex] =
                    new Instruction(
                        jmpOld.OpCode,
                        afterElse);
            }
            else
            {
                // No else. False condition skips consequent.
                int afterConsequent =
                    _prg.Instructions.Count;

                foreach (int index in branchIndexes)
                {
                    var old = _prg.Instructions[index];

                    _prg.Instructions[index] =
                        new Instruction(
                            old.OpCode,
                            afterConsequent);
                }
            }

            return null;
        }

        // Normal boolean/numeric expression.
        EmitExpression(test);
        Pop();

        int beqIndex = _prg.Instructions.Count;

        _prg.Add(
            OpCode.Beq,
            0);

        // Consequent.
        Visit(ifStatement.Consequent);

        if (ifStatement.Alternate != null)
        {
            int jmpIndex = _prg.Instructions.Count;

            _prg.Add(
                OpCode.JmpAbsolute,
                0);

            int elseStart = _prg.Instructions.Count;

            _prg.Instructions[beqIndex] =
                new Instruction(
                    OpCode.Beq,
                    elseStart);

            Visit(ifStatement.Alternate);

            int afterElse = _prg.Instructions.Count;

            _prg.Instructions[jmpIndex] =
                new Instruction(
                    OpCode.JmpAbsolute,
                    afterElse);
        }
        else
        {
            int afterConsequent =
                _prg.Instructions.Count;

            _prg.Instructions[beqIndex] =
                new Instruction(
                    OpCode.Beq,
                    afterConsequent);
        }

        return null;
    }

    protected override object? VisitAssignmentExpression(
    AssignmentExpression assignmentExpression)
    {
        if (assignmentExpression.Left is not Identifier identifier)
            throw new NotSupportedException(
                "Only simple variable assignments are supported.");

        if (assignmentExpression.Operator != Operator.Assignment)
            throw new NotSupportedException(
                $"Assignment operator '{assignmentExpression.Operator}' is not supported.");

        var variable = _prg.GetVariable(identifier.Name);

        EmitExpression(assignmentExpression.Right);

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

        return null;
    }

    protected override object? VisitWhileStatement(
    Acornima.Ast.WhileStatement whileStatement)
    {
        int conditionStart = _prg.Instructions.Count;

        if (whileStatement.Test is NonLogicalBinaryExpression binary)
        {
            EmitExpression(binary.Left);
            EmitExpression(binary.Right);

            // A = right
            Pop();

            _prg.Add(
                OpCode.StaZeroPage,
                ExpressionTemp);

            // A = left
            Pop();

            _prg.Add(
                OpCode.CmpZeroPage,
                ExpressionTemp);

            int branchIndex = _prg.Instructions.Count;

            // Placeholder. EmitComparisonBranch will emit the
            // actual branch instructions.
            EmitComparisonBranch(
                binary.Operator);

            // We need to patch the generated false branches.
            // For now, collect them from the emitted range.
            for (int i = branchIndex;
                 i < _prg.Instructions.Count;
                 i++)
            {
                var instruction = _prg.Instructions[i];

                if (instruction.OpCode == OpCode.Beq ||
                    instruction.OpCode == OpCode.Bne ||
                    instruction.OpCode == OpCode.Bcc ||
                    instruction.OpCode == OpCode.Bcs)
                {
                    _prg.Instructions[i] =
                        new Instruction(
                            instruction.OpCode,
                            -1);
                }
            }

            Visit(whileStatement.Body);

            _prg.Add(
                OpCode.JmpAbsolute,
                conditionStart);

            int afterWhile = _prg.Instructions.Count;

            // Patch comparison branches to loop exit.
            for (int i = branchIndex;
                 i < _prg.Instructions.Count;
                 i++)
            {
                var instruction = _prg.Instructions[i];

                if (instruction.Operand is int operand && operand == -1)
                {
                    _prg.Instructions[i] =
                        new Instruction(
                            instruction.OpCode,
                            afterWhile);
                }
            }

            return null;
        }

        // Simple truthy while
        EmitExpression(whileStatement.Test);
        Pop();

        int branchIndexSimple = _prg.Instructions.Count;

        _prg.Add(
            OpCode.Beq,
            0);

        Visit(whileStatement.Body);

        _prg.Add(
            OpCode.JmpAbsolute,
            conditionStart);

        int afterWhileSimple = _prg.Instructions.Count;

        _prg.Instructions[branchIndexSimple] =
            new Instruction(
                OpCode.Beq,
                afterWhileSimple);

        return null;
    }

    protected override object? VisitForStatement(Acornima.Ast.ForStatement forStatement)
    {
        // Initialization
        if (forStatement.Init != null)
            Visit(forStatement.Init);

        int conditionStart = _prg.Instructions.Count;

        // Condition
        if (forStatement.Test != null)
        {
            var test = forStatement.Test;

            if (test.GetType().Name == "NonLogicalBinaryExpression")
            {
                var leftProp = test.GetType().GetProperty("Left");
                var rightProp = test.GetType().GetProperty("Right");
                var opProp = test.GetType().GetProperty("Operator");

                if (leftProp == null || rightProp == null || opProp == null)
                    throw new NotSupportedException(
                        "Unsupported for test expression shape.");

                var left = (Expression)leftProp.GetValue(test)!;
                var right = (Expression)rightProp.GetValue(test)!;
                var op = opProp.GetValue(test)!;

                EmitExpression(left);
                EmitExpression(right);

                // A = right
                Pop();

                _prg.Add(OpCode.StaZeroPage, ExpressionTemp);

                // A = left
                Pop();

                _prg.Add(OpCode.CmpZeroPage, ExpressionTemp);

                string opName = op.ToString()!.ToLowerInvariant();

                int branchIndex = _prg.Instructions.Count;

                if (opName.Contains("equal") &&
                    !opName.Contains("not") &&
                    !opName.Contains("inequal") &&
                    !opName.Contains("neq"))
                {
                    // Exit when left != right
                    _prg.Add(OpCode.Bne, 0);
                }
                else if (opName.Contains("not") ||
                         opName.Contains("inequal") ||
                         opName.Contains("neq"))
                {
                    // Exit when left == right
                    _prg.Add(OpCode.Beq, 0);
                }
                else
                {
                    throw new NotSupportedException(
                        $"Operator '{op}' is not supported in for tests.");
                }

                // Body
                Visit(forStatement.Body);

                // Update
                if (forStatement.Update != null)
                    Visit(forStatement.Update);

                // Repeat
                _prg.Add(OpCode.JmpAbsolute, conditionStart);

                // Patch exit branch
                int afterFor = _prg.Instructions.Count;

                var branchOld = _prg.Instructions[branchIndex];
                _prg.Instructions[branchIndex] =
                    new Instruction(branchOld.OpCode, afterFor);

                return null;
            }

            // Simple truthy condition
            EmitExpression(test);
            Pop();

            int simpleBranchIndex = _prg.Instructions.Count;
            _prg.Add(OpCode.Beq, 0);

            Visit(forStatement.Body);

            if (forStatement.Update != null)
                Visit(forStatement.Update);

            _prg.Add(OpCode.JmpAbsolute, conditionStart);

            int simpleAfterFor = _prg.Instructions.Count;

            var simpleBranchOld = _prg.Instructions[simpleBranchIndex];
            _prg.Instructions[simpleBranchIndex] =
                new Instruction(simpleBranchOld.OpCode, simpleAfterFor);

            return null;
        }

        // for (;;)
        // No condition means infinite loop.
        Visit(forStatement.Body);

        if (forStatement.Update != null)
            Visit(forStatement.Update);

        _prg.Add(OpCode.JmpAbsolute, conditionStart);

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
        EmitExpression(expression);
        Pop();

        if (address <= 0xFF)
        {
            _prg.Add(OpCode.StaZeroPage, address);
        }
        else
        {
            _prg.Add(OpCode.StaAbsolute, address);
        }
    }

    private void EmitComparison(
    Expression left,
    Expression right,
    Operator op,
    int falseAddress)
    {
        EmitExpression(left);
        EmitExpression(right);

        // Right operand -> temporary
        Pop();
        _prg.Add(OpCode.StaZeroPage, ExpressionTemp);

        // Left operand -> A
        Pop();

        _prg.Add(
            OpCode.CmpZeroPage,
            ExpressionTemp);

        switch (op)
        {
            case Operator.Equality:
                _prg.Add(OpCode.Bne, falseAddress);
                break;

            case Operator.Inequality:
                _prg.Add(OpCode.Beq, falseAddress);
                break;

            default:
                throw new NotSupportedException(
                    $"Comparison operator '{op}' is not supported.");
        }
    }

    private void EmitComparison(NonLogicalBinaryExpression binary)
    {
        EmitExpression(binary.Left);
        EmitExpression(binary.Right);

        Pop();

        _prg.Add(
            OpCode.StaZeroPage,
            ExpressionTemp);

        Pop();

        _prg.Add(
            OpCode.CmpZeroPage,
            ExpressionTemp);
    }

    private List<int> EmitComparisonBranch(Operator op)
    {
        var branchIndexes = new List<int>();

        switch (op)
        {
            case Operator.Equality:
                branchIndexes.Add(_prg.Instructions.Count);
                _prg.Add(OpCode.Bne, 0);
                break;

            case Operator.Inequality:
                branchIndexes.Add(_prg.Instructions.Count);
                _prg.Add(OpCode.Beq, 0);
                break;

            case Operator.LessThan:
                branchIndexes.Add(_prg.Instructions.Count);
                _prg.Add(OpCode.Bcs, 0);
                break;

            case Operator.GreaterThanOrEqual:
                branchIndexes.Add(_prg.Instructions.Count);
                _prg.Add(OpCode.Bcc, 0);
                break;

            case Operator.GreaterThan:
                // False when:
                // left < right
                // OR
                // left == right

                branchIndexes.Add(_prg.Instructions.Count);
                _prg.Add(OpCode.Bcc, 0);

                branchIndexes.Add(_prg.Instructions.Count);
                _prg.Add(OpCode.Beq, 0);
                break;

            case Operator.LessThanOrEqual:
                // False only when left > right.
                //
                // C = 0 => left < right => true
                // C = 1, Z = 1 => equal => true
                // C = 1, Z = 0 => greater => false

                int bccIndex = _prg.Instructions.Count;
                _prg.Add(OpCode.Bcc, 0);

                int beqIndex = _prg.Instructions.Count;
                _prg.Add(OpCode.Beq, 0);

                // If neither BCC nor BEQ was taken,
                // left > right, so branch false.
                branchIndexes.Add(_prg.Instructions.Count);
                _prg.Add(OpCode.JmpAbsolute, 0);

                // Both BCC and BEQ should continue here.
                int trueAddress = _prg.Instructions.Count;

                _prg.Instructions[bccIndex] =
                    new Instruction(
                        _prg.Instructions[bccIndex].OpCode,
                        trueAddress);

                _prg.Instructions[beqIndex] =
                    new Instruction(
                        _prg.Instructions[beqIndex].OpCode,
                        trueAddress);

                break;

            default:
                throw new NotSupportedException(
                    $"Comparison operator '{op}' is not supported.");
        }

        return branchIndexes;
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
            /*case Operator.Multiplication:
                EmitMultiply();
                break;*/

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