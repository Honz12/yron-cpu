using Raylib_cs;

public static class InputHelper
{
    private struct KeyCollection()
    {
        public required KeyboardKey StartKeyboard;
        public required char StartAscii;
        public required int Lenght;
        public bool CanBeWithShift = false;
        public char ShiftStartAscii = '\0';
        public bool CanBeWithControl = false;
        public char ControlStartAscii = '\0';
    }

    private static KeyCollection[] collections =
    [
        new()
        {
            StartKeyboard=KeyboardKey.A,
            StartAscii='a',
            Lenght=KeyboardKey.Z-KeyboardKey.A,
            CanBeWithShift=true,
            ShiftStartAscii='A',
            CanBeWithControl=true,
            ControlStartAscii='\x01'
        },
        new()
        {
            StartKeyboard=KeyboardKey.Zero,
            StartAscii='0',
            Lenght=10,
        },
        new()
        {
            StartKeyboard=KeyboardKey.Kp0,
            StartAscii='0',
            Lenght=10,
        },
        new()
        {
            StartKeyboard=KeyboardKey.Escape,
            StartAscii='\x1b',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.Enter,
            StartAscii='\n',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.KpEnter,
            StartAscii='\n',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.Space,
            StartAscii=' ',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.Apostrophe,
            StartAscii='\'',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='"',
        },
        new()
        {
            StartKeyboard=KeyboardKey.Comma,
            StartAscii=',',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='<',
        },
        new()
        {
            StartKeyboard=KeyboardKey.Minus,
            StartAscii='-',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='_',
        },
        new()
        {
            StartKeyboard=KeyboardKey.Period,
            StartAscii='.',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='>',
        },
        new()
        {
            StartKeyboard=KeyboardKey.Slash,
            StartAscii='/',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='?',
        },
        new()
        {
            StartKeyboard=KeyboardKey.Semicolon,
            StartAscii=';',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii=':',
        },
        new()
        {
            StartKeyboard=KeyboardKey.Equal,
            StartAscii='=',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='+',
        },
        new()
        {
            StartKeyboard=KeyboardKey.LeftBracket,
            StartAscii='[',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='{',
        },
        new()
        {
            StartKeyboard=KeyboardKey.Backslash,
            StartAscii='\\',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='|',
        },
        new()
        {
            StartKeyboard=KeyboardKey.RightBracket,
            StartAscii=']',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='}',
        },
        new()
        {
            StartKeyboard=KeyboardKey.Grave,
            StartAscii='`',
            Lenght=1,
            CanBeWithShift=true,
            ShiftStartAscii='~',
        },
        new()
        {
            StartKeyboard=KeyboardKey.KpDecimal,
            StartAscii='.',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.KpDivide,
            StartAscii='/',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.KpMultiply,
            StartAscii='*',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.KpSubtract,
            StartAscii='-',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.KpAdd,
            StartAscii='+',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.KpEqual,
            StartAscii='=',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.Backspace,
            StartAscii='\b',
            Lenght=1,
        },
        new()
        {
            StartKeyboard=KeyboardKey.Tab,
            StartAscii='\t',
            Lenght=1,
        },
    ];

    public static char ReadPressedChar()
    {
        foreach (KeyCollection keyCollection in collections)
        {
            for (int i = 0; i < keyCollection.Lenght; i++)
            {
                KeyboardKey key = keyCollection.StartKeyboard + i;
                char c = (char) (keyCollection.StartAscii + i);

                if (keyCollection.CanBeWithShift && (Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift)))
                {
                    c = (char) (keyCollection.ShiftStartAscii + i);
                }

                if (keyCollection.CanBeWithControl && (Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl)))
                {
                    c = (char) (keyCollection.ControlStartAscii + i);
                }

                if (Raylib.IsKeyPressed(key) || Raylib.IsKeyPressedRepeat(key))
                {
                    return c;
                }
            }
        }

        return '\0';
    }
}
