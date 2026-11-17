using System;
using System.Collections.Generic;

namespace Burrow.Runtime
{
    public class GvmSlice
    {
        public readonly List<GvmValue> Items = new List<GvmValue>();
    }

    public class GvmMap
    {
        public readonly Dictionary<string, GvmValue> Items = new Dictionary<string, GvmValue>();

        public static string KeyString(GvmValue key)
        {
            switch (key.Type)
            {
                case GvmType.Bool:
                case GvmType.String:
                case GvmType.I8:
                case GvmType.I16:
                case GvmType.I32:
                case GvmType.I64:
                case GvmType.U8:
                case GvmType.U16:
                case GvmType.U32:
                case GvmType.U64:
                    return key.Type + ":" + key.Raw;
            }

            throw new InvalidOperationException("GVM type error: unsupported map key type " + key.Type);
        }
    }

    public class GvmStruct
    {
        public readonly Dictionary<string, GvmValue> Fields = new Dictionary<string, GvmValue>();

        public readonly Dictionary<string, GvmFunc> Methods = new Dictionary<string, GvmFunc>();
    }

    public class GvmFunc
    {
        public readonly int Address;

        public readonly List<GvmValue> Env;

        public GvmFunc(int address)
        {
            Address = address;
        }

        public GvmFunc(int address, List<GvmValue> env)
        {
            Address = address;
            Env = env;
        }
    }

    public class GvmInterface
    {
        public readonly GvmValue Dynamic;

        public GvmInterface(GvmValue dynamic)
        {
            Dynamic = dynamic;
        }
    }

    public class DeferredCall
    {
        public readonly GvmFunc Func;

        public DeferredCall(GvmFunc func)
        {
            Func = func;
        }
    }

    public class GvmRef
    {
        private readonly Func<GvmValue> _getter;
        private readonly Action<GvmValue> _setter;

        public GvmRef(Func<GvmValue> getter, Action<GvmValue> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public GvmValue Load()
        {
            return _getter();
        }

        public void Store(GvmValue value)
        {
            _setter(value);
        }
    }

    public class GvmPanicException : Exception
    {
        public GvmValue Value { get; private set; }

        public GvmPanicException(GvmValue value)
            : base("GVM panic: " + value)
        {
            Value = value;
        }
    }
}
