using System;
using System.Collections.Generic;
using System.IO;

namespace Burrow.Runtime
{
    public class VirtualMachine
    {
        private enum NumKind
        {
            Add,
            Sub,
            Mul,
            Div,
            Mod
        }

        private enum BitKind
        {
            And,
            Or,
            Xor,
            Shl,
            Shr,
            ShrU
        }

        private enum CmpKind
        {
            Eq,
            Neq,
            Lt,
            Lte,
            Gt,
            Gte
        }

        private class GvmFrame
        {
            public readonly int ReturnIp;
            public readonly List<GvmValue> Env;

            public GvmFrame(int returnIp, List<GvmValue> env)
            {
                ReturnIp = returnIp;
                Env = env;
            }
        }

        private readonly Stack<GvmValue> _stack;
        private readonly TextWriter _output;
        private readonly List<GvmValue> _locals = new List<GvmValue>();
        private Stack<GvmFrame> _callStack = new Stack<GvmFrame>();
        private Stack<DeferredCall> _deferred = new Stack<DeferredCall>();
        private GvmValue _pendingPanic;

        public VirtualMachine(TextWriter output)
        {
            _stack = new Stack<GvmValue>();
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
            bool done = false;

            while (!done)
            {
                try
                {
                    while (ip < program.Count)
                    {
                        int next = Dispatch(program[ip], ip, program, addressToIndex);

                        if (next == -2)
                            throw new InvalidOperationException("GVM stack underflow: The call stack is empty, maybe a CALL was forgotten?");

                        if (next == -3)
                        {
                            done = true;
                            break;
                        }

                        ip = next;
                    }

                    done = true;
                }
                catch (GvmPanicException panic)
                {
                    int resumeIp = UnwindPanic(program, addressToIndex, panic);

                    if (resumeIp >= 0)
                    {
                        ip = resumeIp;
                        continue;
                    }

                    done = true;
                }
            }

            _output.WriteLine("GVM Completed!");
        }

        private int Dispatch(Instruction instruction, int ip, IList<Instruction> program, Dictionary<int, int> addressToIndex)
        {
            switch (instruction.Code)
            {
                // Input
                case OpCode.PushInt:
                    _stack.Push(GvmValue.Of(instruction.IntOperand));
                    return ip + 1;

                case OpCode.PushBool:
                    _stack.Push(GvmValue.Of(instruction.BoolOperand));
                    return ip + 1;

                case OpCode.PushFloat:
                    _stack.Push(GvmValue.Of(instruction.FloatOperand));
                    return ip + 1;

                case OpCode.PushString:
                    _stack.Push(GvmValue.Of(instruction.StringOperand));
                    return ip + 1;

                // Math
                case OpCode.Add:
                    _stack.Push(NumOp(NumKind.Add));
                    return ip + 1;

                case OpCode.Substract:
                    _stack.Push(NumOp(NumKind.Sub));
                    return ip + 1;

                case OpCode.Multiply:
                    _stack.Push(NumOp(NumKind.Mul));
                    return ip + 1;

                case OpCode.Divide:
                    _stack.Push(NumOp(NumKind.Div));
                    return ip + 1;

                case OpCode.Mod:
                    _stack.Push(NumOp(NumKind.Mod));
                    return ip + 1;

                case OpCode.Neg:
                    _stack.Push(NegOp());
                    return ip + 1;

                // Bit
                case OpCode.BitAnd:
                    _stack.Push(BitOp(BitKind.And));
                    return ip + 1;

                case OpCode.BitOr:
                    _stack.Push(BitOp(BitKind.Or));
                    return ip + 1;

                case OpCode.BitXor:
                    _stack.Push(BitOp(BitKind.Xor));
                    return ip + 1;

                case OpCode.Shl:
                    _stack.Push(BitOp(BitKind.Shl));
                    return ip + 1;

                case OpCode.Shr:
                    _stack.Push(BitOp(BitKind.Shr));
                    return ip + 1;

                case OpCode.ShrU:
                    _stack.Push(BitOp(BitKind.ShrU));
                    return ip + 1;

                // Stack
                case OpCode.Swap:
                {
                    if (_stack.Count < 2)
                        throw new InvalidOperationException("GVM stack underflow: SWAP requires 2 values.");

                    GvmValue a = _stack.Pop();
                    GvmValue b = _stack.Pop();

                    _stack.Push(a);
                    _stack.Push(b);

                    return ip + 1;
                }

                case OpCode.Dup:
                {
                    if (_stack.Count < 1)
                        throw new InvalidOperationException("GVM stack underflow: DUP requires 1 value");
                    GvmValue value = _stack.Peek();
                    _stack.Push(value);

                    return ip + 1;
                }

                case OpCode.Pop:
                {
                    if (_stack.Count < 1)
                        throw new InvalidOperationException("GVM stack underflow: POP requires 1 value");
                    _stack.Pop();

                    return ip + 1;
                }

                // Flow
                case OpCode.Jump:
                {
                    if (!addressToIndex.TryGetValue(instruction.IntOperand, out int target))
                        throw new InvalidOperationException($"Invalid JUMP target: {instruction.IntOperand} not found");
                    return target;
                }

                case OpCode.JumpIfFalse:
                {
                    bool cond = PopBool();
                    if (!cond)
                    {
                        if (!addressToIndex.TryGetValue(instruction.IntOperand, out int target))
                            throw new InvalidOperationException($"Invalid JUMP target: {instruction.IntOperand} not found");
                        return target;
                    }
                    return ip + 1;
                }

                case OpCode.JumpIfTrue:
                {
                    bool cond = PopBool();
                    if (cond)
                    {
                        if (!addressToIndex.TryGetValue(instruction.IntOperand, out int target))
                            throw new InvalidOperationException($"Invalid JUMP target: {instruction.IntOperand} not found");
                        return target;
                    }
                    return ip + 1;
                }

                case OpCode.Call:
                {
                    if (!addressToIndex.TryGetValue(instruction.IntOperand, out int target))
                        throw new InvalidOperationException($"Invalid CALL target: {instruction.IntOperand} not found");

                    _callStack.Push(new GvmFrame(ip + 1, null));
                    _deferred.Push(new DeferredCall(null));
                    return target;
                }

                case OpCode.Return:
                {
                    RunFrameDefers(program, addressToIndex);

                    if (_callStack.Count == 0)
                        throw new InvalidOperationException("GVM stack underflow: The call stack is empty, maybe a CALL was forgotten?");

                    GvmFrame frame = _callStack.Peek();

                    if (frame.ReturnIp != -1 && _deferred.Count > 0 && _deferred.Peek().Func == null)
                        _deferred.Pop();

                    _callStack.Pop();

                    if (frame.ReturnIp == -1)
                        return -2;

                    return frame.ReturnIp;
                }

                // Comparison
                case OpCode.Eq:
                    _stack.Push(CmpOp(CmpKind.Eq));
                    return ip + 1;

                case OpCode.Neq:
                    _stack.Push(CmpOp(CmpKind.Neq));
                    return ip + 1;

                case OpCode.Lt:
                    _stack.Push(CmpOp(CmpKind.Lt));
                    return ip + 1;

                case OpCode.Lte:
                    _stack.Push(CmpOp(CmpKind.Lte));
                    return ip + 1;

                case OpCode.Gt:
                    _stack.Push(CmpOp(CmpKind.Gt));
                    return ip + 1;

                case OpCode.Gte:
                    _stack.Push(CmpOp(CmpKind.Gte));
                    return ip + 1;

                // Logic
                case OpCode.And:
                {
                    bool b = PopBool();
                    bool a = PopBool();
                    _stack.Push(GvmValue.Of(a && b));
                    return ip + 1;
                }

                case OpCode.Or:
                {
                    bool b = PopBool();
                    bool a = PopBool();
                    _stack.Push(GvmValue.Of(a || b));
                    return ip + 1;
                }

                case OpCode.Not:
                {
                    bool value = PopBool();
                    _stack.Push(GvmValue.Of(!value));
                    return ip + 1;
                }

                // Local
                case OpCode.StoreLocal:
                {
                    GvmValue value = PopValue();
                    SetLocal(instruction.IntOperand, value);
                    return ip + 1;
                }

                case OpCode.LoadLocal:
                {
                    _stack.Push(GetLocal(instruction.IntOperand));
                    return ip + 1;
                }

                // Types
                case OpCode.Conv:
                {
                    GvmValue value = PopValue();
                    _stack.Push(ConvertValue(value, instruction.TypeOperand));
                    return ip + 1;
                }

                // Misc
                case OpCode.Nop:
                    return ip + 1;

                // Containers
                case OpCode.MakeSlice:
                    _stack.Push(GvmValue.Of(new GvmSlice()));
                    return ip + 1;

                case OpCode.MakeMap:
                    _stack.Push(GvmValue.Of(new GvmMap()));
                    return ip + 1;

                case OpCode.MakeStruct:
                    _stack.Push(GvmValue.Of(new GvmStruct()));
                    return ip + 1;

                case OpCode.MakeFunc:
                    _stack.Push(GvmValue.Of(new GvmFunc(instruction.IntOperand)));
                    return ip + 1;

                case OpCode.MakeClosure:
                {
                    var env = new List<GvmValue>();
                    while (_stack.Count > 0)
                        env.Add(_stack.Pop());
                    env.Reverse();

                    _stack.Push(GvmValue.Of(new GvmFunc(instruction.IntOperand, env)));
                    return ip + 1;
                }

                case OpCode.PushNil:
                    _stack.Push(GvmValue.Nil());
                    return ip + 1;

                case OpCode.Len:
                {
                    GvmValue value = PopValue();

                    switch (value.Type)
                    {
                        case GvmType.Slice:
                            _stack.Push(GvmValue.Of(((GvmSlice)value.Raw).Items.Count));
                            break;

                        case GvmType.Map:
                            _stack.Push(GvmValue.Of(((GvmMap)value.Raw).Items.Count));
                            break;

                        case GvmType.String:
                            _stack.Push(GvmValue.Of(((string)value.Raw).Length));
                            break;

                        case GvmType.Nil:
                            _stack.Push(GvmValue.Of(0));
                            break;

                        default:
                            throw new InvalidOperationException("GVM type error: Len requires Slice, Map or String, got " + value.Type);
                    }
                    return ip + 1;
                }

                case OpCode.IndexGet:
                {
                    GvmValue key = PopValue();
                    GvmValue container = PopValue();

                    switch (container.Type)
                    {
                        case GvmType.Slice:
                        {
                            var slice = (GvmSlice)container.Raw;
                            int index = SliceIndex(key, slice.Items.Count, "IndexGet");
                            _stack.Push(slice.Items[index]);
                            break;
                        }

                        case GvmType.Map:
                        {
                            var map = (GvmMap)container.Raw;
                            string mapKey = GvmMap.KeyString(key);
                            _stack.Push(map.Items.TryGetValue(mapKey, out GvmValue item) ? item : GvmValue.Nil());
                            break;
                        }

                        case GvmType.String:
                        {
                            string text = (string)container.Raw;
                            int index = SliceIndex(key, text.Length, "IndexGet");
                            _stack.Push(GvmValue.Of((byte)text[index]));
                            break;
                        }

                        case GvmType.Nil:
                        {
                            _stack.Push(GvmValue.Nil());
                            break;
                        }

                        default:
                            throw new InvalidOperationException("GVM type error: IndexGet requires Slice, Map or String, got " + container.Type);
                    }
                    return ip + 1;
                }

                case OpCode.IndexSet:
                {
                    GvmValue value = PopValue();
                    GvmValue key = PopValue();
                    GvmValue container = PopValue();

                    switch (container.Type)
                    {
                        case GvmType.Slice:
                        {
                            var slice = (GvmSlice)container.Raw;
                            int index = SliceIndex(key, slice.Items.Count, "IndexSet");
                            slice.Items[index] = value;
                            break;
                        }

                        case GvmType.Map:
                        {
                            var map = (GvmMap)container.Raw;
                            map.Items[GvmMap.KeyString(key)] = value;
                            break;
                        }

                        case GvmType.Nil:
                            throw new InvalidOperationException("GVM panic: assignment to entry in nil map");

                        default:
                            throw new InvalidOperationException("GVM type error: IndexSet requires Slice or Map, got " + container.Type);
                    }
                    return ip + 1;
                }

                case OpCode.SliceAppend:
                {
                    GvmValue value = PopValue();
                    GvmValue container = PopValue();

                    if (container.Type == GvmType.Nil)
                    {
                        var fresh = new GvmSlice();
                        fresh.Items.Add(value);
                        _stack.Push(GvmValue.Of(fresh));
                        return ip + 1;
                    }

                    if (container.Type != GvmType.Slice)
                        throw new InvalidOperationException("GVM type error: SliceAppend requires Slice, got " + container.Type);

                    var slice = (GvmSlice)container.Raw;
                    slice.Items.Add(value);
                    _stack.Push(container);
                    return ip + 1;
                }

                case OpCode.SliceSlice:
                {
                    GvmValue high = PopValue();
                    GvmValue low = PopValue();
                    GvmValue container = PopValue();

                    if (container.Type == GvmType.Nil)
                        throw new InvalidOperationException("GVM nil dereference: SliceSlice on nil slice");

                    if (container.Type != GvmType.Slice)
                        throw new InvalidOperationException("GVM type error: SliceSlice requires Slice, got " + container.Type);

                    if (!low.IsInteger() || !high.IsInteger())
                        throw new InvalidOperationException("GVM type error: SliceSlice requires integer bounds");

                    var slice = (GvmSlice)container.Raw;
                    long lowIndex = low.IsSigned() ? ToLong(low) : (long)ToUlong(low);
                    long highIndex = high.IsSigned() ? ToLong(high) : (long)ToUlong(high);

                    if (lowIndex < 0 || highIndex > slice.Items.Count || lowIndex > highIndex)
                        throw new InvalidOperationException("GVM slice bounds out of range: [" + lowIndex + ":" + highIndex + "] (len " + slice.Items.Count + ")");

                    var view = new GvmSlice();
                    for (int i = (int)lowIndex; i < (int)highIndex; i++)
                        view.Items.Add(slice.Items[i]);

                    _stack.Push(GvmValue.Of(view));
                    return ip + 1;
                }

                case OpCode.MapDelete:
                {
                    GvmValue key = PopValue();
                    GvmValue container = PopValue();

                    if (container.Type == GvmType.Nil)
                        return ip + 1;

                    if (container.Type != GvmType.Map)
                        throw new InvalidOperationException("GVM type error: MapDelete requires Map, got " + container.Type);

                    ((GvmMap)container.Raw).Items.Remove(GvmMap.KeyString(key));
                    return ip + 1;
                }

                // Structs
                case OpCode.GetField:
                {
                    GvmValue container = PopValue();
                    GvmStruct structure = ResolveStruct(container, "GetField");

                    if (!structure.Fields.TryGetValue(instruction.StringOperand, out GvmValue value))
                        throw new InvalidOperationException("GVM struct: no field '" + instruction.StringOperand + "'");

                    _stack.Push(value);
                    return ip + 1;
                }

                case OpCode.SetField:
                {
                    GvmValue value = PopValue();
                    GvmValue container = PopValue();
                    GvmStruct structure = ResolveStruct(container, "SetField");

                    structure.Fields[instruction.StringOperand] = value;
                    return ip + 1;
                }

                // Pointers
                case OpCode.RefLocal:
                {
                    int index = instruction.IntOperand;
                    var pointer = new GvmRef(
                        () => GetLocal(index),
                        value => SetLocal(index, value));

                    _stack.Push(GvmValue.Of(pointer));
                    return ip + 1;
                }

                case OpCode.RefIndex:
                {
                    GvmValue key = PopValue();
                    GvmValue container = PopValue();

                    if (container.Type == GvmType.Nil)
                        throw new InvalidOperationException("GVM nil dereference: RefIndex on nil");

                    switch (container.Type)
                    {
                        case GvmType.Slice:
                        {
                            var slice = (GvmSlice)container.Raw;
                            int index = SliceIndex(key, slice.Items.Count, "RefIndex");
                            var pointer = new GvmRef(
                                () => slice.Items[index],
                                value => slice.Items[index] = value);
                            _stack.Push(GvmValue.Of(pointer));
                            break;
                        }

                        case GvmType.Map:
                        {
                            var map = (GvmMap)container.Raw;
                            string mapKey = GvmMap.KeyString(key);
                            var pointer = new GvmRef(
                                () => map.Items.TryGetValue(mapKey, out GvmValue item) ? item : GvmValue.Nil(),
                                value => map.Items[mapKey] = value);
                            _stack.Push(GvmValue.Of(pointer));
                            break;
                        }

                        default:
                            throw new InvalidOperationException("GVM type error: RefIndex requires Slice or Map, got " + container.Type);
                    }
                    return ip + 1;
                }

                case OpCode.RefField:
                {
                    GvmValue container = PopValue();
                    GvmStruct structure = ResolveStruct(container, "RefField");
                    string name = instruction.StringOperand;

                    var pointer = new GvmRef(
                        () =>
                        {
                            if (!structure.Fields.TryGetValue(name, out GvmValue item))
                                throw new InvalidOperationException("GVM struct: no field '" + name + "'");
                            return item;
                        },
                        item => structure.Fields[name] = item);

                    _stack.Push(GvmValue.Of(pointer));
                    return ip + 1;
                }

                case OpCode.LoadRef:
                {
                    GvmValue value = PopValue();

                    if (value.Type == GvmType.Nil)
                        throw new InvalidOperationException("GVM nil dereference: LoadRef on nil pointer");

                    if (value.Type != GvmType.Ref)
                        throw new InvalidOperationException("GVM type error: LoadRef requires Ref, got " + value.Type);

                    _stack.Push(((GvmRef)value.Raw).Load());
                    return ip + 1;
                }

                case OpCode.StoreRef:
                {
                    GvmValue value = PopValue();
                    GvmValue refValue = PopValue();

                    if (refValue.Type == GvmType.Nil)
                        throw new InvalidOperationException("GVM nil dereference: StoreRef on nil pointer");

                    if (refValue.Type != GvmType.Ref)
                        throw new InvalidOperationException("GVM type error: StoreRef requires Ref, got " + refValue.Type);

                    ((GvmRef)refValue.Raw).Store(value);
                    return ip + 1;
                }

                // Strings
                case OpCode.Concat:
                {
                    GvmValue b = PopValue();
                    GvmValue a = PopValue();

                    if (a.Type != GvmType.String || b.Type != GvmType.String)
                        throw new InvalidOperationException("GVM type error: Concat requires String operands, got " + a.Type + " and " + b.Type);

                    _stack.Push(GvmValue.Of((string)a.Raw + (string)b.Raw));
                    return ip + 1;
                }

                // Calls
                case OpCode.CallIndirect:
                {
                    GvmValue value = PopValue();

                    if (value.Type == GvmType.Nil)
                        throw new InvalidOperationException("GVM nil call: CallIndirect on nil func");

                    if (value.Type != GvmType.Func)
                        throw new InvalidOperationException("GVM type error: CallIndirect requires Func, got " + value.Type);

                    var func = (GvmFunc)value.Raw;
                    if (!addressToIndex.TryGetValue(func.Address, out int target))
                        throw new InvalidOperationException($"Invalid CALL target: {func.Address} not found");

                    _callStack.Push(new GvmFrame(ip + 1, func.Env));
                    _deferred.Push(new DeferredCall(null));
                    return target;
                }

                case OpCode.CallMethod:
                {
                    if (_stack.Count == 0)
                        throw new InvalidOperationException("GVM stack underflow: CallMethod requires receiver");

                    GvmFunc func = ResolveMethod(_stack.Peek(), instruction.StringOperand);

                    if (!addressToIndex.TryGetValue(func.Address, out int target))
                        throw new InvalidOperationException($"Invalid CALL target: {func.Address} not found");

                    _callStack.Push(new GvmFrame(ip + 1, func.Env));
                    _deferred.Push(new DeferredCall(null));
                    return target;
                }

                // Interfaces
                case OpCode.WrapIface:
                {
                    GvmValue value = PopValue();

                    if (value.Type == GvmType.Interface)
                        _stack.Push(value);
                    else
                        _stack.Push(GvmValue.Of(new GvmInterface(value)));

                    return ip + 1;
                }

                case OpCode.AssertType:
                {
                    GvmValue value = PopValue();
                    GvmType target = instruction.TypeOperand;

                    GvmValue dynamic = value.Type == GvmType.Interface ? ((GvmInterface)value.Raw).Dynamic : value;

                    if (dynamic.Type == target)
                    {
                        _stack.Push(dynamic);
                        _stack.Push(GvmValue.Of(true));
                    }
                    else
                    {
                        _stack.Push(GvmValue.Nil());
                        _stack.Push(GvmValue.Of(false));
                    }
                    return ip + 1;
                }

                case OpCode.BindMethod:
                {
                    GvmValue funcValue = PopValue();

                    if (funcValue.Type != GvmType.Func)
                        throw new InvalidOperationException("GVM type error: BindMethod requires Func, got " + funcValue.Type);

                    GvmValue receiver = PopValue();
                    GvmStruct structure = ResolveStruct(receiver, "BindMethod");
                    structure.Methods[instruction.StringOperand] = (GvmFunc)funcValue.Raw;

                    _stack.Push(receiver);
                    return ip + 1;
                }

                // Deferred
                case OpCode.Defer:
                {
                    GvmValue value = PopValue();

                    if (value.Type == GvmType.Nil)
                        throw new InvalidOperationException("GVM nil defer: Defer on nil func");

                    if (value.Type != GvmType.Func)
                        throw new InvalidOperationException("GVM type error: Defer requires Func, got " + value.Type);

                    _deferred.Push(new DeferredCall((GvmFunc)value.Raw));
                    return ip + 1;
                }

                case OpCode.Recover:
                {
                    GvmValue pending = _pendingPanic;
                    _pendingPanic = null;
                    _stack.Push(pending ?? GvmValue.Nil());
                    return ip + 1;
                }

                // Closures
                case OpCode.LoadEnv:
                {
                    if (_callStack.Count == 0)
                        throw new InvalidOperationException("GVM env: LoadEnv outside of call");

                    List<GvmValue> env = _callStack.Peek().Env;
                    if (env == null)
                        throw new InvalidOperationException("GVM env: LoadEnv outside closure");

                    int index = instruction.IntOperand;
                    if (index < 0 || index >= env.Count)
                        throw new InvalidOperationException("GVM env overflow: env " + index + " (len " + env.Count + ")");

                    _stack.Push(env[index]);
                    return ip + 1;
                }

                // Panic
                case OpCode.Panic:
                    throw new GvmPanicException(PopValue());

                // Output
                case OpCode.Print:
                    _output.WriteLine(PopValue().ToString());
                    return ip + 1;

                case OpCode.Halt:
                {
                    while (_deferred.Count > 0)
                    {
                        DeferredCall call = _deferred.Pop();

                        if (call.Func == null)
                            continue;

                        _callStack.Push(new GvmFrame(-1, call.Func.Env));
                        RunSub(program, addressToIndex, call.Func.Address);
                    }
                    return -3;
                }

                default:
                    throw new InvalidOperationException("Unknown opcode: " + instruction.Code);
            }
        }

        // Helpers

        private void RunSub(IList<Instruction> program, Dictionary<int, int> addressToIndex, int startIp)
        {
            int ip = addressToIndex[startIp];   

            while (true)
            {
                int next = Dispatch(program[ip], ip, program, addressToIndex);

                if (next == -2)
                    return;   

                if (next == -3)
                    return;

                ip = next;
            }
        }

        private void RunFrameDefers(IList<Instruction> program, Dictionary<int, int> addressToIndex)
        {
            while (_deferred.Count > 0 && _deferred.Peek().Func != null)
            {
                DeferredCall call = _deferred.Pop();
                _callStack.Push(new GvmFrame(-1, call.Func.Env));
                RunSub(program, addressToIndex, call.Func.Address);
            }
        }

        private int UnwindPanic(IList<Instruction> program, Dictionary<int, int> addressToIndex, GvmPanicException panic)
        {
            _pendingPanic = panic.Value;
            int resumeIp = -1;

            while (_deferred.Count > 0)
            {
                DeferredCall call = _deferred.Pop();

                if (call.Func == null)
                {
                    int ret = _callStack.Count > 0 ? _callStack.Pop().ReturnIp : -1;

                    if (_pendingPanic == null)
                    {
                        resumeIp = ret;
                        break;
                    }
                    continue;
                }

                try
                {
                    _callStack.Push(new GvmFrame(-1, call.Func.Env));
                    RunSub(program, addressToIndex, call.Func.Address);
                }
                catch (GvmPanicException nested)
                {
                    _pendingPanic = nested.Value;
                }
            }

            if (resumeIp >= 0)
                return resumeIp;   

            if (_pendingPanic != null)
                _output.WriteLine("panic: " + _pendingPanic);
            else
                _output.WriteLine("GVM Completed!");

            return -1;
        }

        private GvmValue NumOp(NumKind kind)
        {
            GvmValue b = PopValue();
            GvmValue a = PopValue();

            if (a.Type != b.Type)
                throw new InvalidOperationException("GVM type mismatch: " + a.Type + " and " + b.Type + " in " + kind);

            if (!a.IsNumeric())
                throw new InvalidOperationException("GVM type error: " + kind + " requires numeric operands, got " + a.Type);

            unchecked
            {
                // Floats
                if (a.Type == GvmType.F32 || a.Type == GvmType.F64)
                {
                    double x = a.Type == GvmType.F32 ? (float)a.Raw : (double)a.Raw;
                    double y = b.Type == GvmType.F32 ? (float)b.Raw : (double)b.Raw;

                    switch (kind)
                    {
                        case NumKind.Add:
                            return a.Type == GvmType.F32 ? GvmValue.Of((float)(x + y)) : GvmValue.Of(x + y);

                        case NumKind.Sub:
                            return a.Type == GvmType.F32 ? GvmValue.Of((float)(x - y)) : GvmValue.Of(x - y);

                        case NumKind.Mul:
                            return a.Type == GvmType.F32 ? GvmValue.Of((float)(x * y)) : GvmValue.Of(x * y);

                        case NumKind.Div:
                            return a.Type == GvmType.F32 ? GvmValue.Of((float)(x / y)) : GvmValue.Of(x / y);

                        case NumKind.Mod:
                            return a.Type == GvmType.F32 ? GvmValue.Of((float)(x % y)) : GvmValue.Of(x % y);
                    }
                }

                // Unsigned
                if (a.IsUnsigned())
                {
                    ulong x = ToUlong(a);
                    ulong y = ToUlong(b);

                    switch (kind)
                    {
                        case NumKind.Add:
                            return GvmValue.WrapUnsigned(a.Type, x + y);

                        case NumKind.Sub:
                            return GvmValue.WrapUnsigned(a.Type, x - y);

                        case NumKind.Mul:
                            return GvmValue.WrapUnsigned(a.Type, x * y);

                        case NumKind.Div:
                        {
                            if (y == 0)
                                return GvmValue.Zero(a.Type);
                            return GvmValue.WrapUnsigned(a.Type, x / y);
                        }

                        case NumKind.Mod:
                        {
                            if (y == 0)
                                throw new InvalidOperationException("GVM underflow: Division by zero in MOD");
                            return GvmValue.WrapUnsigned(a.Type, x % y);
                        }
                    }
                }

                // Signed
                long sx = ToLong(a);
                long sy = ToLong(b);

                switch (kind)
                {
                    case NumKind.Add:
                        return GvmValue.WrapSigned(a.Type, sx + sy);

                    case NumKind.Sub:
                        return GvmValue.WrapSigned(a.Type, sx - sy);

                    case NumKind.Mul:
                        return GvmValue.WrapSigned(a.Type, sx * sy);

                    case NumKind.Div:
                    {
                        if (sy == 0)
                            return GvmValue.Zero(a.Type);
                        return GvmValue.WrapSigned(a.Type, sx / sy);
                    }

                    case NumKind.Mod:
                    {
                        if (sy == 0)
                            throw new InvalidOperationException("GVM underflow: Division by zero in MOD");
                        return GvmValue.WrapSigned(a.Type, sx % sy);
                    }
                }
            }

            throw new InvalidOperationException("GVM type error: unreachable in " + kind);
        }

        private GvmValue NegOp()
        {
            GvmValue a = PopValue();

            if (!a.IsNumeric())
                throw new InvalidOperationException("GVM type error: Neg requires numeric operand, got " + a.Type);

            unchecked
            {
                if (a.IsSigned())
                    return GvmValue.WrapSigned(a.Type, -ToLong(a));

                if (a.IsUnsigned())
                    return GvmValue.WrapUnsigned(a.Type, 0UL - ToUlong(a));

                double value = a.Type == GvmType.F32 ? (float)a.Raw : (double)a.Raw;
                return a.Type == GvmType.F32 ? GvmValue.Of((float)-value) : GvmValue.Of(-value);
            }
        }

        private GvmValue BitOp(BitKind kind)
        {
            GvmValue b = PopValue();
            GvmValue a = PopValue();

            if (a.Type != b.Type)
                throw new InvalidOperationException("GVM type mismatch: " + a.Type + " and " + b.Type + " in " + kind);

            if (!a.IsInteger())
                throw new InvalidOperationException("GVM type error: " + kind + " requires integer operands, got " + a.Type);

            int width = GvmValue.Width(a.Type);

            unchecked
            {
                if (a.IsSigned())
                {
                    long x = ToLong(a);
                    long y = ToLong(b);

                    switch (kind)
                    {
                        case BitKind.And:
                            return GvmValue.WrapSigned(a.Type, x & y);

                        case BitKind.Or:
                            return GvmValue.WrapSigned(a.Type, x | y);

                        case BitKind.Xor:
                            return GvmValue.WrapSigned(a.Type, x ^ y);

                        case BitKind.Shl:
                        {
                            if (y < 0)
                                throw new InvalidOperationException("GVM shift: negative shift count");
                            if (y >= width)
                                return GvmValue.Zero(a.Type);
                            return GvmValue.WrapSigned(a.Type, x << (int)y);
                        }

                        case BitKind.Shr:
                        {
                            if (y < 0)
                                throw new InvalidOperationException("GVM shift: negative shift count");

                            long result = y >= width ? (x < 0 ? -1 : 0) : x >> (int)y;
                            return GvmValue.WrapSigned(a.Type, result);
                        }

                        case BitKind.ShrU:
                        {
                            if (y < 0)
                                throw new InvalidOperationException("GVM shift: negative shift count");

                            ulong mask = width >= 64 ? ulong.MaxValue : (1UL << width) - 1;
                            ulong pattern = (ulong)x & mask;

                            if (y >= width)
                                return GvmValue.Zero(a.Type);
                            return GvmValue.WrapSigned(a.Type, (long)(pattern >> (int)y));
                        }
                    }
                }
                else
                {
                    ulong x = ToUlong(a);
                    ulong y = ToUlong(b);

                    switch (kind)
                    {
                        case BitKind.And:
                            return GvmValue.WrapUnsigned(a.Type, x & y);

                        case BitKind.Or:
                            return GvmValue.WrapUnsigned(a.Type, x | y);

                        case BitKind.Xor:
                            return GvmValue.WrapUnsigned(a.Type, x ^ y);

                        case BitKind.Shl:
                        {
                            if (y >= (ulong)width)
                                return GvmValue.Zero(a.Type);
                            return GvmValue.WrapUnsigned(a.Type, x << (int)y);
                        }

                        case BitKind.Shr:
                        case BitKind.ShrU:
                        {
                            if (y >= (ulong)width)
                                return GvmValue.Zero(a.Type);
                            return GvmValue.WrapUnsigned(a.Type, x >> (int)y);
                        }
                    }
                }
            }

            throw new InvalidOperationException("GVM type error: unreachable in " + kind);
        }

        private GvmValue CmpOp(CmpKind kind)
        {
            GvmValue b = PopValue();
            GvmValue a = PopValue();

            if (a.Type == GvmType.Nil || b.Type == GvmType.Nil)
                return CmpNil(kind, a, b);

            if (a.Type != b.Type)
                throw new InvalidOperationException("GVM type mismatch: " + a.Type + " and " + b.Type + " in comparison");

            switch (a.Type)
            {
                case GvmType.Bool:
                {
                    if (kind != CmpKind.Eq && kind != CmpKind.Neq)
                        throw new InvalidOperationException("GVM type error: Bool supports only Eq/Neq, got " + kind);

                    bool x = (bool)a.Raw;
                    bool y = (bool)b.Raw;
                    return GvmValue.Of(kind == CmpKind.Eq ? x == y : x != y);
                }

                case GvmType.String:
                {
                    int cmp = string.CompareOrdinal((string)a.Raw, (string)b.Raw);
                    return GvmValue.Of(ApplyCmp(kind, cmp));
                }

                case GvmType.Ref:
                {
                    if (kind != CmpKind.Eq && kind != CmpKind.Neq)
                        throw new InvalidOperationException("GVM type error: Ref supports only Eq/Neq, got " + kind);

                    bool same = ReferenceEquals(a.Raw, b.Raw);
                    return GvmValue.Of(kind == CmpKind.Eq ? same : !same);
                }

                case GvmType.Interface:
                {
                    if (kind != CmpKind.Eq && kind != CmpKind.Neq)
                        throw new InvalidOperationException("GVM type error: Interface supports only Eq/Neq, got " + kind);

                    GvmValue da = ((GvmInterface)a.Raw).Dynamic;
                    GvmValue db = ((GvmInterface)b.Raw).Dynamic;

                    bool same;
                    if (da.Type != db.Type)
                        same = false;
                    else if (da.Type == GvmType.Nil)
                        same = true;
                    else
                        same = ReferenceEquals(da.Raw, db.Raw) || (da.Raw != null && da.Raw.Equals(db.Raw));

                    return GvmValue.Of(kind == CmpKind.Eq ? same : !same);
                }
            }

            if (!a.IsNumeric())
                throw new InvalidOperationException("GVM type error: cannot compare " + a.Type);

            if (a.Type == GvmType.F32 || a.Type == GvmType.F64)
            {
                double x = a.Type == GvmType.F32 ? (float)a.Raw : (double)a.Raw;
                double y = b.Type == GvmType.F32 ? (float)b.Raw : (double)b.Raw;

                switch (kind)
                {
                    case CmpKind.Eq:
                        return GvmValue.Of(x == y);

                    case CmpKind.Neq:
                        return GvmValue.Of(x != y);

                    case CmpKind.Lt:
                        return GvmValue.Of(x < y);

                    case CmpKind.Lte:
                        return GvmValue.Of(x <= y);

                    case CmpKind.Gt:
                        return GvmValue.Of(x > y);

                    case CmpKind.Gte:
                        return GvmValue.Of(x >= y);
                }
            }

            int intCmp;
            if (a.IsSigned())
                intCmp = ToLong(a).CompareTo(ToLong(b));
            else
                intCmp = ToUlong(a).CompareTo(ToUlong(b));

            return GvmValue.Of(ApplyCmp(kind, intCmp));
        }

        private GvmValue CmpNil(CmpKind kind, GvmValue a, GvmValue b)
        {
            if (kind != CmpKind.Eq && kind != CmpKind.Neq)
                throw new InvalidOperationException("GVM type error: Nil supports only Eq/Neq, got " + kind);

            bool bothNil = a.Type == GvmType.Nil && b.Type == GvmType.Nil;

            if (!bothNil)
            {
                GvmValue other = a.Type == GvmType.Nil ? b : a;
                if (!GvmValue.IsReferenceType(other.Type))
                    throw new InvalidOperationException("GVM type mismatch: Nil and " + other.Type + " in comparison");
            }

            bool equal = bothNil;
            return GvmValue.Of(kind == CmpKind.Eq ? equal : !equal);
        }

        private static bool ApplyCmp(CmpKind kind, int cmp)
        {
            switch (kind)
            {
                case CmpKind.Eq:
                    return cmp == 0;

                case CmpKind.Neq:
                    return cmp != 0;

                case CmpKind.Lt:
                    return cmp < 0;

                case CmpKind.Lte:
                    return cmp <= 0;

                case CmpKind.Gt:
                    return cmp > 0;

                case CmpKind.Gte:
                    return cmp >= 0;
            }

            throw new InvalidOperationException("GVM type error: unreachable in " + kind);
        }

        private static GvmValue ConvertValue(GvmValue value, GvmType target)
        {
            if (!value.IsNumeric() || !GvmValue.IsNumericType(target))
                throw new InvalidOperationException("GVM type error: cannot convert " + value.Type + " to " + target);

            unchecked
            {
                if (target == GvmType.F32 || target == GvmType.F64)
                {
                    double source;
                    if (value.IsSigned())
                        source = ToLong(value);
                    else if (value.IsUnsigned())
                        source = ToUlong(value);
                    else if (value.Type == GvmType.F32)
                        source = (float)value.Raw;
                    else
                        source = (double)value.Raw;

                    return target == GvmType.F32 ? GvmValue.Of((float)source) : GvmValue.Of(source);
                }

                if (GvmValue.IsSignedType(target))
                {
                    long source;
                    if (value.IsSigned())
                        source = ToLong(value);
                    else if (value.IsUnsigned())
                        source = (long)ToUlong(value);
                    else if (value.Type == GvmType.F32)
                        source = (long)(float)value.Raw;
                    else
                        source = (long)(double)value.Raw;

                    return GvmValue.WrapSigned(target, source);
                }

                ulong sourceU;
                if (value.IsSigned())
                    sourceU = (ulong)ToLong(value);
                else if (value.IsUnsigned())
                    sourceU = ToUlong(value);
                else if (value.Type == GvmType.F32)
                    sourceU = (ulong)(float)value.Raw;
                else
                    sourceU = (ulong)(double)value.Raw;

                return GvmValue.WrapUnsigned(target, sourceU);
            }
        }

        private GvmStruct ResolveStruct(GvmValue value, string opName)
        {
            if (value.Type == GvmType.Interface)
                value = ((GvmInterface)value.Raw).Dynamic;

            if (value.Type == GvmType.Nil)
                throw new InvalidOperationException("GVM nil dereference: " + opName + " on nil");

            if (value.Type != GvmType.Struct)
                throw new InvalidOperationException("GVM type error: " + opName + " requires Struct, got " + value.Type);

            return (GvmStruct)value.Raw;
        }

        private GvmFunc ResolveMethod(GvmValue receiver, string name)
        {
            GvmValue target = receiver;

            if (target.Type == GvmType.Interface)
            {
                target = ((GvmInterface)target.Raw).Dynamic;
            }

            if (target.Type == GvmType.Nil)
                throw new InvalidOperationException("GVM nil dereference: CallMethod '" + name + "' on nil");

            if (target.Type != GvmType.Struct)
                throw new InvalidOperationException("GVM type error: CallMethod '" + name + "' requires Struct receiver, got " + target.Type);

            var structure = (GvmStruct)target.Raw;
            if (!structure.Methods.TryGetValue(name, out GvmFunc func))
                throw new InvalidOperationException("GVM method not found: '" + name + "'");

            return func;
        }

        private GvmValue GetLocal(int index)
        {
            if (index >= _locals.Count)
                throw new InvalidOperationException("GVM local overflow: local " + index + " was never stored");

            return _locals[index];
        }

        private void SetLocal(int index, GvmValue value)
        {
            while (_locals.Count <= index)
                _locals.Add(GvmValue.Nil());

            _locals[index] = value;
        }

        private static int SliceIndex(GvmValue key, int count, string opName)
        {
            if (!key.IsInteger())
                throw new InvalidOperationException("GVM type error: " + opName + " requires integer index, got " + key.Type);

            long index = key.IsSigned() ? ToLong(key) : (long)ToUlong(key);

            if (index < 0 || index >= count)
                throw new InvalidOperationException("GVM index out of range: " + index + " (len " + count + ")");

            return (int)index;
        }

        private GvmValue PopValue()
        {
            if (_stack.Count == 0)
                throw new InvalidOperationException("GVM stack underflow.");

            return _stack.Pop();
        }

        private bool PopBool()
        {
            GvmValue value = PopValue();

            if (value.Type != GvmType.Bool)
                throw new InvalidOperationException("Expected Bool, got " + value.Type);

            return (bool)value.Raw;
        }

        private static long ToLong(GvmValue value)
        {
            switch (value.Type)
            {
                case GvmType.I8:
                    return (sbyte)value.Raw;

                case GvmType.I16:
                    return (short)value.Raw;

                case GvmType.I32:
                    return (int)value.Raw;

                case GvmType.I64:
                    return (long)value.Raw;
            }

            throw new InvalidOperationException("GVM type error: " + value.Type + " is not a signed integer");
        }

        private static ulong ToUlong(GvmValue value)
        {
            switch (value.Type)
            {
                case GvmType.U8:
                    return (byte)value.Raw;

                case GvmType.U16:
                    return (ushort)value.Raw;

                case GvmType.U32:
                    return (uint)value.Raw;

                case GvmType.U64:
                    return (ulong)value.Raw;
            }

            throw new InvalidOperationException("GVM type error: " + value.Type + " is not an unsigned integer");
        }
    }
}
