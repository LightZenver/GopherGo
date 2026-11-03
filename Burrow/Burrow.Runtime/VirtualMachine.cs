using System;
using System.Collections.Generic;
using System.IO;

namespace Burrow.Runtime
{
    public class VirtualMachine
    {
        private readonly Stack<object> _stack;
        private readonly TextWriter _output;
        private Stack<int> _callStack = new Stack<int>();

        public VirtualMachine(TextWriter output)
        {
            _stack = new Stack<object>();
            _output = output;

        }

        public void Execute(IList<Instruction> program)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));

            var addressToIndex = new Dictionary<int, int>();
            for (int i = 0; i < program.Count; i++)
                addressToIndex[program[i].Address] = i;

            _output.WriteLine("===== GVM WORKER =====");

            int ip = 0;
            bool halted = false;

            while (ip < program.Count && !halted)
            {
                Instruction instruction = program[ip];

                switch (instruction.Code)
                {
                    // Input
                    case OpCode.PushInt:
                    {
                        _stack.Push(instruction.IntOperand);
                        break;
                    }

                    case OpCode.PushBool:
                    {
                        _stack.Push(instruction.BoolOperand);
                        break;
                    }
                        
                    // Math
                    case OpCode.Add:
                    {
                        int b = PopInt();
                        int a = PopInt();
                        _stack.Push(a + b);
                        break;
                    }

                    case OpCode.Substract:
                    {
                        int b = PopInt();
                        int a = PopInt();
                        _stack.Push(a - b);
                        break;
                    }

                    case OpCode.Multiply:
                    {
                        int b = PopInt();
                        int a = PopInt();
                        _stack.Push(a * b);
                        break;
                    }

                    case OpCode.Divide:
                    {
                        int b = PopInt();
                        int a = PopInt();

                        if (b == 0)
                        {
                            _stack.Push(0);
                            break;
                        }
                        _stack.Push(a / b);
                        break;
                    }

                    // Stack 
                    case OpCode.Swap:
                    {
                        if (_stack.Count < 2)
                            throw new InvalidOperationException("GVM stack underflow: SWAP requires 2 values.");
                        
                        object a = _stack.Pop();
                        object b = _stack.Pop();

                        _stack.Push(a);
                        _stack.Push(b);

                        break;
                    }

                    case OpCode.Dup:
                    {
                        if (_stack.Count < 1)
                            throw new InvalidOperationException("GVM stack underflow: DUP requires 1 value");
                        object value = _stack.Peek();
                        _stack.Push(value);

                        break;
                    }

                    case OpCode.Pop:
                    {
                        if (_stack.Count < 1)
                            throw new InvalidOperationException("GVM stack underflow: POP requires 1 value");
                        _stack.Pop();

                        break;
                    }

                    // Flow
                    case OpCode.Jump:
                    {
                        if (!addressToIndex.TryGetValue(instruction.IntOperand, out int target))
                            throw new InvalidOperationException($"Invalid JUMP target: {instruction.IntOperand} not found");
                        ip = target;
                        continue;
                    }

                    case OpCode.JumpIfFalse:
                    {
                        bool cond = PopBool();
                        if (!cond)
                        {
                            if (!addressToIndex.TryGetValue(instruction.IntOperand, out int target))
                                throw new InvalidOperationException($"Invalid JUMP target: {instruction.IntOperand} not found");
                            ip = target;
                            continue;
                        }
                        break;
                    }

                    case OpCode.JumpIfTrue:
                    {
                        bool cond = PopBool();
                        if (cond)
                        {
                            if (!addressToIndex.TryGetValue(instruction.IntOperand, out int target))
                                throw new InvalidOperationException($"Invalid JUMP target: {instruction.IntOperand} not found");
                            ip = target;
                            continue;
                        }
                        break;
                    }

                    case OpCode.Call:
                    {
                        if (!addressToIndex.TryGetValue(instruction.IntOperand, out int target))
                            throw new InvalidOperationException($"Invalid CALL target: {instruction.IntOperand} not found");
                        _callStack.Push(ip + 1);
                        ip = target;
                        continue;
                    }

                    case OpCode.Return:
                    {
                        if (_callStack.Count == 0)
                            throw new InvalidOperationException("GVM call stack underflow");
                        
                        ip = _callStack.Pop();
                        continue;
                    }

                    // Output
                    case OpCode.Print:
                    {
                        _output.WriteLine(Pop());
                        break;
                    }

                    case OpCode.Halt:
                    {
                        halted = true;
                        break;
                    }

                    default:
                        throw new InvalidOperationException("Unknown opcode: " + instruction.Code);
                }

                ip++;
            }

            _output.WriteLine("GVM Completed!");
        }

        private object Pop()
        {
            if (_stack.Count == 0)
                throw new InvalidOperationException("GVM stack underflow.");

            return _stack.Pop();
        }

        private int PopInt()
        {
            object value = Pop();

            if (!(value is int))
                throw new InvalidOperationException("Expected int, got " + (value?.GetType().Name ?? "null"));

            return (int)value;
        }

        private bool PopBool()
        {
            object value = Pop();

            if (!(value is bool))
                throw new InvalidOperationException("Expected bool, got " + (value?.GetType().Name ?? "null"));

            return (bool)value;
        }
    }
}