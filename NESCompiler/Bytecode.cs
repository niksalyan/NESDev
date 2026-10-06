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
        private readonly byte[] _prg = new byte[32 * 1024];
        private int _p = 0;

        public int P => _p;

        private List<Variable> _variables = [];


        public byte[] ToBytecode() => _prg.ToArray();

        public void LdaImmediate(byte value)
        {
            Emit(0xA9);
            Emit(value);
        }

        public void Sta(ushort address)
        {
            Emit(0x8D);
            Emit((byte)(address & 0xFF));
            Emit((byte)(address >> 8));
        }

        public void Lda(ushort address)
        {
            Emit(0xAD);
            Emit((byte)(address & 0xFF));
            Emit((byte)(address >> 8));
        }

        public void Inc(ushort address)
        {
            Emit(0xEE);
            Emit((byte)(address & 0xFF));
            Emit((byte)(address >> 8));
        }

        public void Jmp(ushort address)
        {
            Emit(0x4C);
            Emit((byte)(address & 0xFF));
            Emit((byte)(address >> 8));
        }

        public void Emit(
        params byte[] bytes)
        {
            foreach (byte b in bytes)
            {
                _prg[_p++] = b;
            }
        }

        public void WriteVector(
        int offset,
        ushort address)
        {
            _prg[offset] = (byte)(address & 0xFF);
            _prg[offset + 1] = (byte)(address >> 8);
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
