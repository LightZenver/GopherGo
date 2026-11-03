namespace Burrow.Runtime
{
    public enum OpCode
    {
        // Input
        PushInt,    // Works
        PushBool,   // Works

        // Math
        Add,        // Works
        Substract,  // Works
        Multiply,   // Works
        Divide,     // Works

        // Stack
        Dup,        // Works
        Swap,       // Works
        Pop,        // Works

        // Flow
        Jump,       // Works
        JumpIfFalse,// Works
        JumpIfTrue, // Works
        Call,       // Works
        Return,     // Works

        // Output
        Print,      // Works
        Halt        // Works
    }
}