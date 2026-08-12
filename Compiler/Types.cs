namespace Compiler
{
    public abstract class Type
    {
        public abstract int Size { get; }
        public abstract int Align { get; }
        public virtual bool IsInteger => false;
        public virtual bool IsSigned => false;
        public override string ToString() => "type";
    }

    public sealed class PrimType : Type
    {
        public static readonly PrimType Void = new("void", 0, false, false);
        public static readonly PrimType U8 = new("u8", 1, true, false);
        public static readonly PrimType U16 = new("u16", 2, true, false);
        public static readonly PrimType U32 = new("u32", 4, true, false);
        public static readonly PrimType I8 = new("i8", 1, true, true);
        public static readonly PrimType I16 = new("i16", 2, true, true);
        public static readonly PrimType I32 = new("i32", 4, true, true);

        public string Name { get; }
        public override int Size { get; }
        public override int Align => Size;
        public override bool IsInteger { get; }
        public override bool IsSigned { get; }

        private PrimType(string name, int size, bool isInteger, bool isSigned)
        {
            Name = name;
            Size = size;
            IsInteger = isInteger;
            IsSigned = isSigned;
        }

        public override string ToString() => Name;
    }

    public sealed class PtrType : Type
    {
        public Type Pointee { get; }
        public override int Size => 4;
        public override int Align => 4;
        public override bool IsInteger => false;

        public PtrType(Type pointee) => Pointee = pointee;

        public override string ToString() => $"{Pointee}*";
    }

    public sealed class ArrayType : Type
    {
        public Type Elem { get; }
        public int Length { get; }
        public override int Size => checked(Elem.Size * Length);
        public override int Align => Elem.Align;
        public override bool IsInteger => false;

        public ArrayType(Type elem, int length)
        {
            Elem = elem;
            Length = length;
        }

        public override string ToString() => $"{Elem}[{Length}]";
    }

    public sealed class StructField
    {
        public string Name = "";
        public Type Type = null!;
        public int Offset;
    }

    public sealed class StructLayout
    {
        public string Name = "";
        public List<StructField> Fields = new();
        public int Size;
        public int Align;
    }

    public sealed class StructType : Type
    {
        public string Name { get; }
        public StructLayout? Layout { get; set; }

        public override int Size => Layout?.Size ?? 0;
        public override int Align => Layout?.Align ?? 1;
        public override bool IsInteger => false;

        public StructType(string name) => Name = name;

        public override string ToString() => Name;
    }
}
