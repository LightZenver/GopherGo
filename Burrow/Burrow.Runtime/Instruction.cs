namespace Burrow.Runtime
{
    public class Instruction
    {
        public OpCode Code {get; private set; }
        public int Operand {get; private set; }

        public Instruction(OpCode code, int operand = 0)
        {
            Code = code;
            Operand = operand;
        }   
    }
}