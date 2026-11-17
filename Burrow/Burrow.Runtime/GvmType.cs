using System;
using System.Collections.Generic;

namespace Burrow.Runtime
{
    public enum GvmType
    {
        Nil,
        Bool,
        I8,
        I16,
        I32,
        I64,
        U8,
        U16,
        U32,
        U64,
        F32,
        F64,
        String,
        Slice,
        Map,
        Struct,
        Ref,
        Func,
        Interface
    }

    public class GvmValue
    {
        public GvmType Type;

        public object Raw;

        public GvmValue(GvmType type, object raw)
        {
            Type = type;
            Raw = raw;
        }

        // Factories
        public static GvmValue Nil()
        {
            return new GvmValue(GvmType.Nil, null);
        }

        public static GvmValue Of(bool value)
        {
            return new GvmValue(GvmType.Bool, value);
        }

        public static GvmValue Of(sbyte value)
        {
            return new GvmValue(GvmType.I8, value);
        }

        public static GvmValue Of(short value)
        {
            return new GvmValue(GvmType.I16, value);
        }

        public static GvmValue Of(int value)
        {
            return new GvmValue(GvmType.I32, value);
        }

        public static GvmValue Of(long value)
        {
            return new GvmValue(GvmType.I64, value);
        }

        public static GvmValue Of(byte value)
        {
            return new GvmValue(GvmType.U8, value);
        }

        public static GvmValue Of(ushort value)
        {
            return new GvmValue(GvmType.U16, value);
        }

        public static GvmValue Of(uint value)
        {
            return new GvmValue(GvmType.U32, value);
        }

        public static GvmValue Of(ulong value)
        {
            return new GvmValue(GvmType.U64, value);
        }

        public static GvmValue Of(float value)
        {
            return new GvmValue(GvmType.F32, value);
        }

        public static GvmValue Of(double value)
        {
            return new GvmValue(GvmType.F64, value);
        }

        public static GvmValue Of(string value)
        {
            return new GvmValue(GvmType.String, value);
        }

        public static GvmValue Of(GvmSlice value)
        {
            return new GvmValue(GvmType.Slice, value);
        }

        public static GvmValue Of(GvmMap value)
        {
            return new GvmValue(GvmType.Map, value);
        }

        public static GvmValue Of(GvmStruct value)
        {
            return new GvmValue(GvmType.Struct, value);
        }

        public static GvmValue Of(GvmRef value)
        {
            return new GvmValue(GvmType.Ref, value);
        }

        public static GvmValue Of(GvmFunc value)
        {
            return new GvmValue(GvmType.Func, value);
        }

        public static GvmValue Of(GvmInterface value)
        {
            return new GvmValue(GvmType.Interface, value);
        }

        public static GvmValue Zero(GvmType type)
        {
            switch (type)
            {
                case GvmType.Nil:
                    return Nil();

                case GvmType.Bool:
                    return Of(false);

                case GvmType.I8:
                    return Of((sbyte)0);

                case GvmType.I16:
                    return Of((short)0);

                case GvmType.I32:
                    return Of(0);

                case GvmType.I64:
                    return Of(0L);

                case GvmType.U8:
                    return Of((byte)0);

                case GvmType.U16:
                    return Of((ushort)0);

                case GvmType.U32:
                    return Of(0U);

                case GvmType.U64:
                    return Of(0UL);

                case GvmType.F32:
                    return Of(0F);

                case GvmType.F64:
                    return Of(0D);

                case GvmType.String:
                    return Of(string.Empty);

                case GvmType.Slice:
                case GvmType.Map:
                case GvmType.Struct:
                case GvmType.Ref:
                case GvmType.Func:
                case GvmType.Interface:
                    return Nil();
            }

            throw new InvalidOperationException("GVM type error: unknown type " + type);
        }

        public static GvmValue WrapSigned(GvmType type, long value)
        {
            unchecked
            {
                switch (type)
                {
                    case GvmType.I8:
                        return Of((sbyte)value);

                    case GvmType.I16:
                        return Of((short)value);

                    case GvmType.I32:
                        return Of((int)value);

                    case GvmType.I64:
                        return Of(value);
                }
            }

            throw new InvalidOperationException("GVM type error: " + type + " is not a signed integer");
        }

        public static GvmValue WrapUnsigned(GvmType type, ulong value)
        {
            unchecked
            {
                switch (type)
                {
                    case GvmType.U8:
                        return Of((byte)value);

                    case GvmType.U16:
                        return Of((ushort)value);

                    case GvmType.U32:
                        return Of((uint)value);

                    case GvmType.U64:
                        return Of(value);
                }
            }

            throw new InvalidOperationException("GVM type error: " + type + " is not an unsigned integer");
        }

        // Type helpers
        public static bool IsNumericType(GvmType type)
        {
            switch (type)
            {
                case GvmType.I8:
                case GvmType.I16:
                case GvmType.I32:
                case GvmType.I64:
                case GvmType.U8:
                case GvmType.U16:
                case GvmType.U32:
                case GvmType.U64:
                case GvmType.F32:
                case GvmType.F64:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsIntegerType(GvmType type)
        {
            return IsSignedType(type) || IsUnsignedType(type);
        }

        public static bool IsSignedType(GvmType type)
        {
            switch (type)
            {
                case GvmType.I8:
                case GvmType.I16:
                case GvmType.I32:
                case GvmType.I64:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsUnsignedType(GvmType type)
        {
            switch (type)
            {
                case GvmType.U8:
                case GvmType.U16:
                case GvmType.U32:
                case GvmType.U64:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsReferenceType(GvmType type)
        {
            switch (type)
            {
                case GvmType.Slice:
                case GvmType.Map:
                case GvmType.Struct:
                case GvmType.Ref:
                case GvmType.Func:
                case GvmType.Interface:
                    return true;

                default:
                    return false;
            }
        }

        public static int Width(GvmType type)
        {
            switch (type)
            {
                case GvmType.I8:
                case GvmType.U8:
                    return 8;

                case GvmType.I16:
                case GvmType.U16:
                    return 16;

                case GvmType.I32:
                case GvmType.U32:
                    return 32;

                case GvmType.I64:
                case GvmType.U64:
                    return 64;
            }

            throw new InvalidOperationException("GVM type error: " + type + " has no bit width");
        }

        public bool IsNumeric()
        {
            return IsNumericType(Type);
        }

        public bool IsInteger()
        {
            return IsIntegerType(Type);
        }

        public bool IsSigned()
        {
            return IsSignedType(Type);
        }

        public bool IsUnsigned()
        {
            return IsUnsignedType(Type);
        }

        public override string ToString()
        {
            switch (Type)
            {
                case GvmType.Nil:
                    return "nil";

                case GvmType.Slice:
                    return "[" + string.Join(" ", ((GvmSlice)Raw).Items) + "]";

                case GvmType.Map:
                {
                    var map = (GvmMap)Raw;
                    var parts = new List<string>();

                    foreach (var pair in map.Items)
                        parts.Add(pair.Key.Substring(pair.Key.IndexOf(':') + 1) + ":" + pair.Value);

                    return "map[" + string.Join(" ", parts) + "]";
                }

                case GvmType.Struct:
                {
                    var structure = (GvmStruct)Raw;
                    var parts = new List<string>();

                    foreach (var pair in structure.Fields)
                        parts.Add(pair.Key + ":" + pair.Value);

                    return "{" + string.Join(" ", parts) + "}";
                }

                case GvmType.Ref:
                    return "&" + ((GvmRef)Raw).Load();

                case GvmType.Func:
                    return "func@" + ((GvmFunc)Raw).Address;

                case GvmType.Interface:
                    return ((GvmInterface)Raw).Dynamic.ToString();
            }

            return Raw == null ? "nil" : Raw.ToString();
        }
    }
}
