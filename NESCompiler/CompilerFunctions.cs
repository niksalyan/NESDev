using Acornima.Ast;
using System;
using System.Collections.Generic;
using System.Text;

namespace NESCompiler
{
    public class CompilerFunctions
    {
        private Bytecode _prg;
        public CompilerFunctions(Bytecode prg)
        {
            _prg = prg;
        }

        public void Execute(string name, object[]? args) {

            var text = (StringLiteral)args[0];

            foreach (char c in text.Value)
            {
                _prg.WritePpu((byte)(c - 32));
            }
        }
    }
}
