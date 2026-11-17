using System;

namespace Burrow.Runtime
{
    public class Instruction
    {
        public int Address { get; set; }

        public OpCode Code { get; private set; }

        public int IntOperand { get; private set; }

        public bool BoolOperand { get; private set; }

        public double FloatOperand { get; private set; }

        public string StringOperand { get; private set; }

        public GvmType TypeOperand { get; private set; }

        public Instruction(OpCode code)
        {
            Code = code;
        }

        public Instruction(OpCode code, int operand)
        {
            Code = code;
            IntOperand = operand;
        }

        public Instruction(OpCode code, bool operand)
        {
            Code = code;
            BoolOperand = operand;
        }

        public Instruction(OpCode code, double operand)
        {
            Code = code;
            FloatOperand = operand;
        }

        public Instruction(OpCode code, string operand)
        {
            Code = code;
            StringOperand = operand;
        }

        public Instruction(OpCode code, GvmType operand)
        {
            Code = code;
            TypeOperand = operand;
        }
    }
}
