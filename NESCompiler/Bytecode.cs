using Acornima.Ast;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace NESCompiler
{

    public enum VariableType
    {
        None,
        Byte,
        Int16,

        Int32
    }

    public class Variable
    {
        public string Name { get; }

        [DisplayName("Type")]
        public string DisplayType => Kind.ToString() + "." + Type.ToString() + (IsArray ? "[" + Length + "]" : "");

        [Browsable(false)]
        public VariableType Type { get; }

        [Browsable(false)]
        public VariableDeclarationKind Kind { get; }

        public int Address { get; set; }

        public int Size => GetSize();

        public bool IsArray = false;
        public int Length;

        public Variable(
            string name,
            VariableType type,
            VariableDeclarationKind kind,
            bool isArray = false,
            int length = 1
            )
        {
            Name = name;
            Type = type;
            Kind = kind;
            IsArray = isArray;
            Length = length;
        }

        public int GetElementSize()
        {
            switch (Type)
            {
                case VariableType.Byte:
                    return 1;
                case VariableType.Int16:
                    return 2;
                case VariableType.Int32:
                    return 4;
                default:
                    return 0;
            }
        }

        public int GetSize()
        {
            return GetElementSize() * Length;
        }
    }

    public class Bytecode
    {
        private readonly List<byte> _code = [];
        private List<Variable> _variables = [];


        public byte[] ToArray()
            => _code.ToArray();

        public void LdaImmediate(byte value)
        {
            _code.Add(0xA9);
            _code.Add(value);
        }

        public void Sta(ushort address)
        {
            _code.Add(0x8D);
            _code.Add((byte)(address & 0xFF));
            _code.Add((byte)(address >> 8));
        }

        public void Lda(ushort address)
        {
            _code.Add(0xAD);
            _code.Add((byte)(address & 0xFF));
            _code.Add((byte)(address >> 8));
        }

        public void Inc(ushort address)
        {
            _code.Add(0xEE);
            _code.Add((byte)(address & 0xFF));
            _code.Add((byte)(address >> 8));
        }

        public void Jmp(ushort address)
        {
            _code.Add(0x4C);
            _code.Add((byte)(address & 0xFF));
            _code.Add((byte)(address >> 8));
        }

        public Variable GetVariable(string name)
        {
            var variable = _variables.FirstOrDefault(x => x.Name == name);

            if (variable == null)
                throw new InvalidOperationException(
                    $"Variable '{name}' is not declared.");

            return variable;
        }

        public Variable DeclareVariable(
        string name,
        VariableType type,
        VariableDeclarationKind kind,
        bool isArray = false,
        int length = 1)
        {
            var variable = _variables.FirstOrDefault(x => x.Name == name);
            if (variable != null)
            {
                return variable;
            }

            variable = new Variable(
                name,
                type,
                kind,
                isArray,
                length);

            _variables.Add(variable);
            UpdateAddresses();

            return variable;
        }

        public void UpdateAddresses()
        {
            int address = 0;
            foreach (var variable in _variables)
            {
                variable.Address = address;
                address += variable.GetSize();
            }
        }

    }
}
