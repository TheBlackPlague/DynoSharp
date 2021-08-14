using InputSimulatorStandard;
using InputSimulatorStandard.Native;

namespace DynoSharp
{

    internal static class KeyboardHandler
    {

        private static readonly KeyboardSimulator Simulator = new KeyboardSimulator();

        public static void PressSpace()
        {
            Simulator.KeyPress(VirtualKeyCode.SPACE);
        }

    }

}