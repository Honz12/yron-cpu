using Raylib_cs;

public static class InputHelper
{
    public static char ReadPressedChar()
    {
        // 1. Helper function to check both initial press AND repeat ticks
        bool IsKeyTriggered(KeyboardKey key) => 
            Raylib.IsKeyPressed(key) || Raylib.IsKeyPressedRepeat(key);

        // 2. Check control keys (Initial Press + Repeat)
        if (IsKeyTriggered(KeyboardKey.Enter) || IsKeyTriggered(KeyboardKey.KpEnter))
            return '\n';

        if (IsKeyTriggered(KeyboardKey.Backspace))
            return '\b';

        if (IsKeyTriggered(KeyboardKey.Tab))
            return '\t';

        if (IsKeyTriggered(KeyboardKey.Escape))
            return (char)27;

        for (int fkey = 0; fkey < 12; fkey++)
        {
            if (IsKeyTriggered(KeyboardKey.F1 + fkey))
                return (char)(fkey + 1);
        }

        // 3. Printable letters/numbers natively repeat via GetCharPressed
        int charPressed = Raylib.GetCharPressed();
        if (charPressed > 0)
        {
            return (char)charPressed;
        }

        return '\0'; // No key pressed
    }
}
