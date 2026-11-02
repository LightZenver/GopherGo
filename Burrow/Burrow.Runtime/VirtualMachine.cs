using System;
using System.Collections.Generic;
using System.IO;

namespace Burrow.Runtime
{
    public class VirtualMachine
    {
        private readonly Stack<int> _stack;
        private readonly TextWriter _output;

        public VirtualMachine(TextWriter output)
        {
            _stack = new Stack<int>();
            _output = output;
        }

        public void Execute(IList<Instruction> program)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));

            int ip = 0;
            bool halted = false;

            while (ip < program.Count && !halted)
            {
                Instruction instruction = program[ip];

                switch (instruction.Code)
                {
                    case OpCode.PushInt:
                        _stack.Push(instruction.Operand);
                        break;

                    case OpCode.Add:
                    {
                        int b = Pop();
                        int a = Pop();
                        _stack.Push(a + b);
                        break;
                    }

                    case OpCode.Substract:
                    {
                        int b = Pop();
                        int a = Pop();
                        _stack.Push(a - b);
                        break;
                    }

                    case OpCode.Multiply:
                    {
                        int b = Pop();
                        int a = Pop();
                        _stack.Push(a * b);
                        break;
                    }

                    case OpCode.Divide:
                    {
                        int b = Pop();
                        int a = Pop();

                        if (b == 0)
                            _stack.Push(0);
                            break;

                        _stack.Push(a / b);
                        break;
                    }

                    case OpCode.Print:
                        _output.WriteLine(Pop());
                        break;

                    case OpCode.Halt:
                        halted = true;
                        break;

                    default:
                        throw new InvalidOperationException(
                            "Unknown opcode: " + instruction.Code);
                }

                ip++;
            }
        }

        private int Pop()
        {
            if (_stack.Count == 0)
                throw new InvalidOperationException(
                    "GVM stack underflow.");

            return _stack.Pop();
        }
    }
}
