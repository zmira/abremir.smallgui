namespace abremir.MicroUISharp;

// ========================================================================
// Stack Implementation (Pre-allocated, Zero Garbage collection)
// ========================================================================

public class MuStack<T>
{
    public int Idx;
    public readonly T[] Items;

    public MuStack(int capacity)
    {
        Items = new T[capacity];
        Idx = 0;
    }

    public void Push(T val)
    {
        if (Idx >= Items.Length)
            throw new InvalidOperationException("MicroUI Stack overflow error.");
        Items[Idx++] = val;
    }

    public T Pop()
    {
        if (Idx <= 0)
            throw new InvalidOperationException("MicroUI Stack underflow error.");
        return Items[--Idx];
    }

    public T Peek()
    {
        if (Idx <= 0)
            throw new InvalidOperationException("MicroUI Stack is empty.");
        return Items[Idx - 1];
    }

    public void Clear() => Idx = 0;
}

