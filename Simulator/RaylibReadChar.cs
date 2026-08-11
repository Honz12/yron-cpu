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
