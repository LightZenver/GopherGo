using System;

namespace Burrow.Runtime
{
public class Instruction
{
public int Address { get; set; }

    public OpCode Code { get; private set; }

    public int IntOperand { get; private set; }

    public bool BoolOperand { get; private set; }

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
}
}