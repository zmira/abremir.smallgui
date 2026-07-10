namespace abremir.MicroUISharp;

public struct MuWidths
{
    private int _0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15;

    public int this[int index]
    {
        get => index switch
        {
            0 => _0,
            1 => _1,
            2 => _2,
            3 => _3,
            4 => _4,
            5 => _5,
            6 => _6,
            7 => _7,
            8 => _8,
            9 => _9,
            10 => _10,
            11 => _11,
            12 => _12,
            13 => _13,
            14 => _14,
            15 => _15,
            _ => throw new IndexOutOfRangeException()
        };
        set
        {
            switch (index)
            {
                case 0: _0 = value; break;
                case 1: _1 = value; break;
                case 2: _2 = value; break;
                case 3: _3 = value; break;
                case 4: _4 = value; break;
                case 5: _5 = value; break;
                case 6: _6 = value; break;
                case 7: _7 = value; break;
                case 8: _8 = value; break;
                case 9: _9 = value; break;
                case 10: _10 = value; break;
                case 11: _11 = value; break;
                case 12: _12 = value; break;
                case 13: _13 = value; break;
                case 14: _14 = value; break;
                case 15: _15 = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    public void CopyFrom(int[] source, int count)
    {
        for (int i = 0; i < count && i < 16; i++)
        {
            this[i] = source[i];
        }
    }
}
