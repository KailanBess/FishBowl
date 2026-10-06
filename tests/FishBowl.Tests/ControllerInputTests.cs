using System;
using System.Linq;
using EmulatorHub;

public static class ControllerInputTests
{
    private static byte[] Frame(int kind, int number, int value)
    { return new byte[] { 0, 0, 0, 0, unchecked((byte)value), unchecked((byte)(value >> 8)), (byte)kind, (byte)number }; }
    public static void Run(Action<string, bool> check)
    {
        var input = new JoystickInput();
        check("controller initialization never activates a button", input.Read(Frame(0x81, 0, 1), 0).Count == 0);
        input.Read(Frame(1, 0, 0), 10);
        check("controller A activates on press", input.Read(Frame(1, 0, 1), 20).SequenceEqual(new[] { ControllerAction.Activate }));
        check("controller held A does not repeat launches", input.Read(Frame(1, 0, 1), 30).Count == 0 && input.Repeat(1000).Count == 0);
        check("controller B goes back", input.Read(Frame(1, 1, 1), 40).SequenceEqual(new[] { ControllerAction.Back }));
        check("controller stick deadzone suppresses drift", input.Read(Frame(2, 0, JoystickInput.DeadZone), 100).Count == 0);
        check("controller left stick moves", input.Read(Frame(2, 0, -32768), 200).SequenceEqual(new[] { ControllerAction.Left }));
        check("controller repeat has initial delay", input.Repeat(549).Count == 0);
        check("controller held direction repeats after delay", input.Repeat(550).SequenceEqual(new[] { ControllerAction.Left }) && input.Repeat(669).Count == 0 && input.Repeat(670).SequenceEqual(new[] { ControllerAction.Left }));
        input.Read(Frame(2, 0, 0), 700);
        check("controller centered stick stops repeating", input.Repeat(2000).Count == 0);
        check("controller directional pad supports up", input.Read(Frame(2, 7, -32767), 2100).SequenceEqual(new[] { ControllerAction.Up }));
        input.Reset();
        check("controller reset discards background navigation", input.Repeat(10000).Count == 0);
        check("controller unsupported axis cannot navigate", input.Read(Frame(2, 3, -32768), 11000).Count == 0);
        check("controller malformed packet is ignored", input.Read(new byte[] { 1, 2 }, 12000).Count == 0);
    }
}
