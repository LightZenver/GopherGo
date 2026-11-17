namespace Burrow.Runtime
{
    public enum OpCode
    {
        // Input
        PushInt,    // Works
        PushBool,   // Works
        PushFloat,  // Works
        PushString, // Works

        // Math
        Add,        // Works
        Substract,  // Works
        Multiply,   // Works
        Divide,     // Works
        Mod,        // Works
        Neg,        // Works

        // Bit
        BitAnd,     // Works
        BitOr,      // Works
        BitXor,     // Works
        Shl,        // Works
        Shr,        // Works
        ShrU,       // Works

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

        // Comparison
        Eq,         // Works
        Neq,        // Works
        Lt,         // Works
        Lte,        // Works
        Gt,         // Works
        Gte,        // Works

        // Logic
        And,        // Works
        Or,         // Works
        Not,        // Works

        // Local
        StoreLocal, // Works
        LoadLocal,  // Works

        // Types
        Conv,       // Works

        // Misc
        Nop,        // Works

        // Containers
        MakeSlice,  // Works
        MakeMap,    // Works
        MakeStruct, // Works
        MakeFunc,   // Works
        PushNil,    // Works
        Len,        // Works
        IndexGet,   // Works
        IndexSet,   // Works
        SliceAppend,// Works
        SliceSlice, // Works
        MapDelete,  // Works

        // Structs
        GetField,   // Works
        SetField,   // Works

        // Pointers
        RefLocal,   // Works
        LoadRef,    // Works
        StoreRef,   // Works

        // Strings
        Concat,     // Works

        // Calls
        CallIndirect,// Works

        // Panic
        Panic,      // Works

        // Deferred
        Defer,      // Works
        Recover,    // Works

        // Pointers
        RefIndex,   // Works
        RefField,   // Works

        // Interfaces
        WrapIface,  // Works
        AssertType, // Works
        BindMethod, // Works
        CallMethod, // Works

        // Closures
        MakeClosure,// Works
        LoadEnv,    // Works

        // Output
        Print,      // Works
        Halt        // Works
    }
}
