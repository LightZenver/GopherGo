namespace Burrow.Runtime
{
    public enum OpCode
    {
        // Input
        PushInt,    // Works
        PushBool,   // Works
        Dup,        // Works
        Swap,       // Works
        Pop,        // Works

        // Math
        Add,        // Works
        Substract,  // Works
        Multiply,   // Works
        Divide,     // Works

        // Output
        Print,      // Works
        Halt        // Works
    }
}