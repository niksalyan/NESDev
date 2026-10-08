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

    private const int DelayCounterAddress = 0x01FF;

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
        // -------------------------
        // CPU initialization
        // -------------------------

        _prg.Add(OpCode.Sei);
        _prg.Add(OpCode.Cld);

        // Initialize 6502 stack.
        _prg.Add(OpCode.LdxImmediate, 0xFF);
        _prg.Add(OpCode.Txs);

        // Your expression stack uses X as its index.
        _prg.Add(OpCode.LdxImmediate, 0);

        // -------------------------
        // PPU initialization
        // -------------------------

        // Disable NMI and rendering.
        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2000);
        _prg.Add(OpCode.StaAbsolute, 0x2001);

        // Wait for first VBlank.
        EmitWaitVBlank();

        // PPU initialization goes here.
        // Palette, nametable, etc.

        // Reset PPU latch.
        _prg.Add(OpCode.LdaAbsolute, 0x2002);

        // Reset scroll.
        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2005);
        _prg.Add(OpCode.StaAbsolute, 0x2005);

        // Wait for another VBlank.
        EmitWaitVBlank();

        // -------------------------
        // User program
        // -------------------------

        Node ast = _parser.ParseScript(_src); 
        Visit(ast);

        // -------------------------
        // End / restart policy
        // -------------------------

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
            EmitComparison(binary);

            var branchIndexes =
                EmitComparisonBranch(binary.Operator);

            Visit(whileStatement.Body);

            _prg.Add(
                OpCode.JmpAbsolute,
                conditionStart);

            int afterWhile = _prg.Instructions.Count;

            foreach (int index in branchIndexes)
            {
                var old = _prg.Instructions[index];

                _prg.Instructions[index] =
                    new Instruction(
                        old.OpCode,
                        afterWhile);
            }

            return null;
        }

        // Simple truthy while
        EmitExpression(whileStatement.Test);
        Pop();

        int branchIndexSimple =
            _prg.Instructions.Count;

        _prg.Add(
            OpCode.Beq,
            0);

        Visit(whileStatement.Body);

        _prg.Add(
            OpCode.JmpAbsolute,
            conditionStart);

        int afterWhileSimple =
            _prg.Instructions.Count;

        _prg.Instructions[branchIndexSimple] =
            new Instruction(
                OpCode.Beq,
                afterWhileSimple);

        return null;
    }

    protected override object? VisitForStatement(
    Acornima.Ast.ForStatement forStatement)
    {
        // Initialization
        if (forStatement.Init != null)
            Visit(forStatement.Init);

        int conditionStart = _prg.Instructions.Count;

        // Condition
        if (forStatement.Test is NonLogicalBinaryExpression binary)
        {
            EmitComparison(binary);

            var branchIndexes =
                EmitComparisonBranch(binary.Operator);

            // Body
            Visit(forStatement.Body);

            // Update
            if (forStatement.Update != null)
                Visit(forStatement.Update);

            // Loop back to condition
            _prg.Add(
                OpCode.JmpAbsolute,
                conditionStart);

            int afterFor = _prg.Instructions.Count;

            // False comparison exits the loop
            foreach (int index in branchIndexes)
            {
                var old = _prg.Instructions[index];

                _prg.Instructions[index] =
                    new Instruction(
                        old.OpCode,
                        afterFor);
            }

            return null;
        }

        // Simple truthy/numeric condition
        if (forStatement.Test != null)
        {
            EmitExpression(forStatement.Test);
            Pop();

            int branchIndex =
                _prg.Instructions.Count;

            _prg.Add(
                OpCode.Beq,
                0);

            Visit(forStatement.Body);

            if (forStatement.Update != null)
                Visit(forStatement.Update);

            _prg.Add(
                OpCode.JmpAbsolute,
                conditionStart);

            int afterFor =
                _prg.Instructions.Count;

            _prg.Instructions[branchIndex] =
                new Instruction(
                    OpCode.Beq,
                    afterFor);

            return null;
        }

        // for (;;) with no condition
        Visit(forStatement.Body);

        if (forStatement.Update != null)
            Visit(forStatement.Update);

        _prg.Add(
            OpCode.JmpAbsolute,
            conditionStart);

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

        if (functionName == "frame")
        {
            EmitWaitVBlank();
        } else
        {
            _compilerFunctions.Execute(
            functionName,
            arguments.ToArray());
        }

        

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

    private void EmitComparison(NonLogicalBinaryExpression binary)
    {
        EmitExpression(binary.Left);
        EmitExpression(binary.Right);

        // Right operand -> temporary
        Pop();
        _prg.Add(OpCode.StaZeroPage, ExpressionTemp);

        // Left operand -> A
        Pop();

        // A <=> right
        _prg.Add(OpCode.CmpZeroPage, ExpressionTemp);
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

    private void EmitWaitVBlank()
    {
        int waitStart = _prg.Instructions.Count;

        _prg.Add(OpCode.BitAbsolute, 0x2002);
        _prg.Add(OpCode.Bpl, waitStart);
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

    private sealed class ComparisonBranches
    {
        public List<int> TrueBranches { get; } = new();
        public List<int> FalseBranches { get; } = new();
    }

}

