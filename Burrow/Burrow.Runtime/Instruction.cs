namespace Burrow.Runtime
{
    public class Instruction
    {
        public OpCode Code { get; private set; }
        public int IntOperand { get; private set; }
        public bool BoolOperand { get; private set; }
        public int Address { get; set; }

        public Instruction(OpCode code)
        {
            Code = code;
        }

        public Instruction(OpCode code, int operand = 0)
        {
            Code = code;
            IntOperand = operand;
        }

        public Instruction(OpCode code, bool operand = false)
        {
            Code = code;
            BoolOperand = operand;
        }
    }
}