using Acornima;
using Acornima.Ast;
using com.clusterrr.Famicom.Containers;
using System.Diagnostics;

namespace NESCompiler;

public class Compiler : AstVisitor
{
    public List<Variable> Variables => _prg?.Variables ?? [];

    public List<Instruction> Instructions => _prg?.Instructions ?? [];

    private CompilerFunctions _compilerFunctions;


    public const byte ExpressionTemp = 0xEF;

    private const int DelayCounterAddress = 0x01FF;



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
        _compilerFunctions = new CompilerFunctions(
            this,
            _prg);
        _prg.Variables.Clear();

        

        BuildProgram();

        Debug.WriteLine($"PRG size: {_prg.ToBytecode().Length} bytes");

        var nes = new NesFile
        {
            Mapper = 0,
            PRG = _prg.ToBytecode(),
            CHR = Tileset.Default
        };

        var rom = nes.ToBytes();

        File.WriteAllBytes("output.nes", rom);
        File.WriteAllText("output.txt", Tileset.ToArduinoArray(nes.PRG));

        return rom;
    }



    // ============================================================
    // 6502 PROGRAM
    // ============================================================

    private void BuildProgram()
    {
        // ============================================================
        // CPU INITIALIZATION
        // ============================================================

        _prg.Add(OpCode.Sei);
        _prg.Add(OpCode.Cld);

        // Initialize 6502 hardware stack.
        _prg.Add(OpCode.LdxImmediate, 0xFF);
        _prg.Add(OpCode.Txs);

        // Initialize expression stack index.
        _prg.Add(OpCode.LdxImmediate, 0);

        // ============================================================
        // PPU INITIALIZATION
        // ============================================================

        // Disable NMI and rendering.
        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2000);
        _prg.Add(OpCode.StaAbsolute, 0x2001);

        // Wait for VBlank.
        _compilerFunctions.Execute("sync");

        // Reset PPU address/scroll latch.
        _prg.Add(OpCode.LdaAbsolute, 0x2002);

        // ------------------------------------------------------------
        // Palette
        // ------------------------------------------------------------

        _prg.PpuAddress(0x3F00);

        _prg.WritePpu(0x0F);
        _prg.WritePpu(0x30);
        _prg.WritePpu(0x20);
        _prg.WritePpu(0x10);

        // ------------------------------------------------------------
        // Initial nametable address
        // ------------------------------------------------------------

        _prg.PpuAddress(0x2090);

        // ------------------------------------------------------------
        // Reset scroll
        // ------------------------------------------------------------

        _prg.Add(OpCode.LdaImmediate, 0x00);
        _prg.Add(OpCode.StaAbsolute, 0x2005);
        _prg.Add(OpCode.StaAbsolute, 0x2005);

        // Wait for another VBlank before enabling rendering.
        _compilerFunctions.Execute("sync");

        // ------------------------------------------------------------
        // Enable background rendering
        // ------------------------------------------------------------

        _prg.Add(OpCode.LdaImmediate, 0x18);
        _prg.Add(OpCode.StaAbsolute, 0x2001);

        // ============================================================
        // USER PROGRAM
        // ============================================================

        Node ast = _parser.ParseScript(_src);
        Visit(ast);

        // ============================================================
        // END OF PROGRAM
        // ============================================================

        // Do not restart PPU initialization.
        int end = _prg.Instructions.Count;

        _prg.Add(
            OpCode.JmpAbsolute,
            end);
    }

    protected override object? VisitExpressionStatement(
    ExpressionStatement expressionStatement)
    {
        return Visit(expressionStatement.Expression);
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

            var variable = _prg.DeclareVariable(
                identifier.Name,
                VariableType.Byte,
                VariableDeclarationKind.Var);

            EmitExpression(declaration.Init);

            if (variable.Address <= 0xFF)
            {
                _prg.Add(OpCode.StaZeroPage, variable.Address);
            }
            else
            {
                _prg.Add(OpCode.StaAbsolute, variable.Address);
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

            Visit(ifStatement.Consequent);

            if (ifStatement.Alternate != null)
            {
                int jmpIndex = _prg.Instructions.Count;

                _prg.Add(OpCode.JmpAbsolute, 0);

                int elseStart = _prg.Instructions.Count;

                foreach (int index in branchIndexes)
                {
                    var old = _prg.Instructions[index];

                    _prg.Instructions[index] =
                        new Instruction(old.OpCode, elseStart);
                }

                Visit(ifStatement.Alternate);

                int afterElse = _prg.Instructions.Count;

                var jmpOld = _prg.Instructions[jmpIndex];

                _prg.Instructions[jmpIndex] =
                    new Instruction(jmpOld.OpCode, afterElse);
            }
            else
            {
                int afterConsequent = _prg.Instructions.Count;

                foreach (int index in branchIndexes)
                {
                    var old = _prg.Instructions[index];

                    _prg.Instructions[index] =
                        new Instruction(old.OpCode, afterConsequent);
                }
            }

            return null;
        }

        // Evaluate the condition into A.
        EmitExpression(test);

        // Branch if A == 0.
        // EmitExpression must leave the result in A.
        int beqIndex = _prg.Instructions.Count;

        _prg.Add(OpCode.Beq, 0);

        Visit(ifStatement.Consequent);

        if (ifStatement.Alternate != null)
        {
            int jmpIndex = _prg.Instructions.Count;

            _prg.Add(OpCode.JmpAbsolute, 0);

            int elseStart = _prg.Instructions.Count;

            _prg.Instructions[beqIndex] =
                new Instruction(OpCode.Beq, elseStart);

            Visit(ifStatement.Alternate);

            int afterElse = _prg.Instructions.Count;

            _prg.Instructions[jmpIndex] =
                new Instruction(OpCode.JmpAbsolute, afterElse);
        }
        else
        {
            int afterConsequent = _prg.Instructions.Count;

            _prg.Instructions[beqIndex] =
                new Instruction(OpCode.Beq, afterConsequent);
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

        // Result must be left in the 6502 accumulator (A).
        EmitExpression(assignmentExpression.Right);

        if (variable.Address <= 0xFF)
        {
            _prg.Add(OpCode.StaZeroPage, variable.Address);
        }
        else
        {
            _prg.Add(OpCode.StaAbsolute, variable.Address);
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

            var jumpIndexes =
                EmitComparisonBranch(binary.Operator);

            Visit(whileStatement.Body);

            // Loop back to condition.
            _prg.Add(OpCode.JmpAbsolute, conditionStart);

            int afterWhile = _prg.Instructions.Count;

            // Patch the absolute jumps used for exiting the loop.
            foreach (int index in jumpIndexes)
            {
                var old = _prg.Instructions[index];

                _prg.Instructions[index] =
                    new Instruction(OpCode.JmpAbsolute, afterWhile);
            }

            return null;
        }

        // Simple truthy while.
        EmitExpression(whileStatement.Test);

        // Ensure Zero flag reflects A == 0.
        _prg.Add(OpCode.CmpImmediate, 0);

        // If nonzero, skip the exit jump.
        int branchIndex = _prg.Instructions.Count;

        _prg.Add(OpCode.Bne, branchIndex + 2);

        // Far jump to loop exit.
        int exitJumpIndex = _prg.Instructions.Count;

        _prg.Add(OpCode.JmpAbsolute, 0);

        Visit(whileStatement.Body);

        // Loop back to condition.
        _prg.Add(OpCode.JmpAbsolute, conditionStart);

        int afterWhileSimple = _prg.Instructions.Count;

        _prg.Instructions[exitJumpIndex] =
            new Instruction(OpCode.JmpAbsolute, afterWhileSimple);

        return null;
    }



    protected override object? VisitForStatement(
        Acornima.Ast.ForStatement forStatement)
    {
        // Initialization
        if (forStatement.Init != null)
            Visit(forStatement.Init);

        int conditionStart = _prg.Instructions.Count;

        // Comparison condition
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
            _prg.Add(OpCode.JmpAbsolute, conditionStart);

            int afterFor = _prg.Instructions.Count;

            // False comparison exits the loop
            foreach (int index in branchIndexes)
            {
                var old = _prg.Instructions[index];

                _prg.Instructions[index] =
                    new Instruction(old.OpCode, afterFor);
            }

            return null;
        }

        // Simple truthy/numeric condition
        if (forStatement.Test != null)
        {
            EmitExpression(forStatement.Test);

            // Set Zero flag based on accumulator A.
            _prg.Add(OpCode.CmpImmediate, 0);

            int branchIndex = _prg.Instructions.Count;

            _prg.Add(OpCode.Beq, 0);

            Visit(forStatement.Body);

            if (forStatement.Update != null)
                Visit(forStatement.Update);

            _prg.Add(OpCode.JmpAbsolute, conditionStart);

            int afterFor = _prg.Instructions.Count;

            _prg.Instructions[branchIndex] =
                new Instruction(OpCode.Beq, afterFor);

            return null;
        }

        // for (;;) with no condition
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

        var arguments = new List<Expression>();

        foreach (var argument in callExpression.Arguments)
        {
            arguments.Add(argument);
        }

        return _compilerFunctions.Execute(
            functionName,
            arguments);
    }

    private void EmitComparison(NonLogicalBinaryExpression binary)
    {
        EmitExpression(binary.Left);

        if (binary.Right is Literal literal)
        {
            // Compare A with an immediate constant.
            _prg.Add(
                OpCode.CmpImmediate,
                Convert.ToByte(literal.Value));
        }
        else if (binary.Right is Identifier identifier)
        {
            var variable = _prg.GetVariable(identifier.Name);

            if (variable.Address <= 0xFF)
            {
                _prg.Add(
                    OpCode.CmpZeroPage,
                    variable.Address);
            }
            else
            {
                _prg.Add(
                    OpCode.CmpAbsolute,
                    variable.Address);
            }
        }
        else
        {
            throw new NotSupportedException(
                "Comparison operands must be simple literals or variables.");
        }
    }


    private List<int> EmitComparisonBranch(Operator op)
    {
        var jumpIndexes = new List<int>();

        switch (op)
        {
            case Operator.Equality:
                jumpIndexes.Add(
                    EmitFarFalseJump(OpCode.Bne));
                break;

            case Operator.Inequality:
                jumpIndexes.Add(
                    EmitFarFalseJump(OpCode.Beq));
                break;

            case Operator.LessThan:
                jumpIndexes.Add(
                    EmitFarFalseJump(OpCode.Bcs));
                break;

            case Operator.GreaterThanOrEqual:
                jumpIndexes.Add(
                    EmitFarFalseJump(OpCode.Bcc));
                break;

            case Operator.GreaterThan:
                jumpIndexes.Add(
                    EmitFarFalseJump(OpCode.Bcc));

                jumpIndexes.Add(
                    EmitFarFalseJump(OpCode.Beq));
                break;

            case Operator.LessThanOrEqual:
                {
                    int start = _prg.Instructions.Count;

                    // Less -> continue.
                    _prg.Add(
                        OpCode.Bcc,
                        start + 3);

                    // Equal -> continue.
                    _prg.Add(
                        OpCode.Beq,
                        start + 3);

                    // Greater -> false.
                    int jumpIndex =
                        _prg.Instructions.Count;

                    _prg.Add(
                        OpCode.JmpAbsolute,
                        0);

                    jumpIndexes.Add(jumpIndex);

                    break;
                }

            default:
                throw new NotSupportedException(
                    $"Comparison operator '{op}' is not supported.");
        }

        return jumpIndexes;
    }

    private int EmitFarFalseJump(OpCode falseBranch)
    {
        OpCode trueBranch = falseBranch switch
        {
            OpCode.Bne => OpCode.Beq,
            OpCode.Beq => OpCode.Bne,
            OpCode.Bcs => OpCode.Bcc,
            OpCode.Bcc => OpCode.Bcs,

            _ => throw new NotSupportedException(
                $"Cannot invert branch '{falseBranch}'.")
        };

        // The conditional branch skips over the JMP.
        int branchIndex =
            _prg.Instructions.Count;

        _prg.Add(
            trueBranch,
            branchIndex + 2);

        // This is the actual far jump.
        int jumpIndex =
            _prg.Instructions.Count;

        _prg.Add(
            OpCode.JmpAbsolute,
            0);

        return jumpIndex;
    }


    public void EmitExpression(Expression expression)
    {
        if (expression is NonLogicalBinaryExpression nonLogicalBinary)
        {
            EmitNonLogicalBinaryExpression(nonLogicalBinary);
            return;
        }

        switch (expression)
        {
            case Literal literal:
                _prg.Add(
                    OpCode.LdaImmediate,
                    Convert.ToByte(literal.Value));
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

                    return;
                }

            case CallExpression callExpression:
                {
                    VisitCallExpression(callExpression);
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

    private void EmitAdd(Expression left, Expression right)
    {
        EmitExpression(left); // A = left

        switch (right)
        {
            case Literal literal:
                _prg.Add(
                    OpCode.Clc);

                _prg.Add(
                    OpCode.AdcImmediate,
                    Convert.ToByte(literal.Value));
                break;

            case Identifier identifier:
                {
                    var variable = _prg.GetVariable(identifier.Name);

                    _prg.Add(OpCode.Clc);

                    if (variable.Address <= 0xFF)
                    {
                        _prg.Add(
                            OpCode.AdcZeroPage,
                            variable.Address);
                    }
                    else
                    {
                        _prg.Add(
                            OpCode.AdcAbsolute,
                            variable.Address);
                    }

                    break;
                }

            default:
                throw new NotSupportedException(
                    "Addition supports only literals and variables as operands.");
        }
    }


    private void EmitSubtract(Expression left, Expression right)
    {
        EmitExpression(left); // A = left

        _prg.Add(OpCode.Sec);

        switch (right)
        {
            case Literal literal:
                _prg.Add(
                    OpCode.SbcImmediate,
                    Convert.ToByte(literal.Value));
                break;

            case Identifier identifier:
                {
                    var variable = _prg.GetVariable(identifier.Name);

                    if (variable.Address <= 0xFF)
                    {
                        _prg.Add(
                            OpCode.SbcZeroPage,
                            variable.Address);
                    }
                    else
                    {
                        _prg.Add(
                            OpCode.SbcAbsolute,
                            variable.Address);
                    }

                    break;
                }

            default:
                throw new NotSupportedException(
                    "Subtraction supports only literals and variables as operands.");
        }
    }


    public void EmitCursorAddress(
        Expression x,
        Expression y,
        byte scratchAddress)
    {
        // Reset PPU $2006 write latch.
        _prg.Add(OpCode.LdaAbsolute, 0x2002);

        // Calculate Y * 32.
        EmitExpression(y);

        _prg.Add(OpCode.AslAccumulator);
        _prg.Add(OpCode.AslAccumulator);
        _prg.Add(OpCode.AslAccumulator);
        _prg.Add(OpCode.AslAccumulator);
        _prg.Add(OpCode.AslAccumulator);

        // Preserve Y * 32 while loading X.
        _prg.Add(OpCode.StaZeroPage, scratchAddress);

        EmitExpression(x);

        // A = (Y * 32) + X.
        _prg.Add(OpCode.Clc);
        _prg.Add(OpCode.AdcZeroPage, scratchAddress);

        // Write PPU address high byte ($20).
        _prg.Add(OpCode.LdaImmediate, 0x20);
        _prg.Add(OpCode.StaAbsolute, 0x2006);

        // Write PPU address low byte.
        EmitExpression(y);

        _prg.Add(OpCode.AslAccumulator);
        _prg.Add(OpCode.AslAccumulator);
        _prg.Add(OpCode.AslAccumulator);
        _prg.Add(OpCode.AslAccumulator);
        _prg.Add(OpCode.AslAccumulator);

        _prg.Add(OpCode.Clc);
        _prg.Add(OpCode.AdcZeroPage, scratchAddress);

        _prg.Add(OpCode.StaAbsolute, 0x2006);
    }




    private void EmitNonLogicalBinaryExpression(Expression expression)
    {
        var operations = new List<(Operator Operator, Expression Operand)>();

        Expression GetFirstOperand(Expression node)
        {
            while (node is NonLogicalBinaryExpression binary)
            {
                operations.Add((binary.Operator, binary.Right));
                node = binary.Left;
            }

            return node;
        }

        var first = GetFirstOperand(expression);

        // Emit the first operand into A.
        EmitExpression(first);

        // The collected operations are currently right-to-left.
        operations.Reverse();

        foreach (var (op, operand) in operations)
        {
            switch (op)
            {
                case Operator.Addition:
                    EmitAdd(operand);
                    break;

                case Operator.Subtraction:
                    EmitSubtract(operand);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Operator '{op}' is not supported. Only + and - are allowed.");
            }
        }
    }



    private void EmitFlatExpression(
        Expression expression,
        object parentOperator)
    {
        if (expression is NonLogicalBinaryExpression binary)
        {
            string opName = binary.Operator.ToString()!.ToLowerInvariant();

            if (opName.Contains("add") || opName.Contains("+"))
            {
                EmitFlatExpression(binary.Left, parentOperator);
                EmitAdd(binary.Right);
                return;
            }

            if (opName.Contains("sub") || opName.Contains("-"))
            {
                EmitFlatExpression(binary.Left, parentOperator);
                EmitSubtract(binary.Right);
                return;
            }
        }

        string parentOpName = parentOperator.ToString()!.ToLowerInvariant();

        if (parentOpName.Contains("add") || parentOpName.Contains("+"))
        {
            EmitAdd(expression);
            return;
        }

        if (parentOpName.Contains("sub") || parentOpName.Contains("-"))
        {
            EmitSubtract(expression);
            return;
        }

        throw new NotSupportedException(
            $"Operator '{parentOperator}' is not supported.");
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

