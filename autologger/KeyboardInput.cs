using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

using Dapplo.Windows.Input;
using Dapplo.Windows.Input.Enums;
using Dapplo.Windows.Input.Keyboard;
using Dapplo.Windows.Input.Structs;

namespace autologger
{
    internal class KeyboardInput
    {
        public static void Type(string text)
        {
            const char CtrlSpace = '♫';
            const char OneSecondDelay = '…';

            foreach (var c in text)
            {
                switch (c)
                {
                    case '←':
                        KeyboardInputGenerator.KeyPresses(VirtualKeyCode.Left);
                        break;
                    case '→':
                        KeyboardInputGenerator.KeyPresses(VirtualKeyCode.Right);
                        break;
                    case '↑':
                        KeyboardInputGenerator.KeyPresses(VirtualKeyCode.Up);
                        break;
                    case '↓':
                        KeyboardInputGenerator.KeyPresses(VirtualKeyCode.Down);
                        break;
                    case '\r':
                        KeyboardInputGenerator.KeyPresses(VirtualKeyCode.Return);
                        break;
                    case CtrlSpace:
                        KeyboardInputGenerator.KeyCombinationPress(VirtualKeyCode.Space, VirtualKeyCode.LeftControl);
                        break;
                    case OneSecondDelay:
                        Thread.Sleep(1000);
                        break;
                    default:
                        SendScanCodeInput(c);

                        break;
                }
            }
        }

        public static void ReleaseModifierKeys()
        {
            var modifiers = new List<VirtualKeyCode>
            {
                VirtualKeyCode.LeftWin, VirtualKeyCode.RightWin,
                VirtualKeyCode.LeftShift, VirtualKeyCode.RightShift,
                VirtualKeyCode.Shift,
                VirtualKeyCode.LeftControl, VirtualKeyCode.RightControl,
                VirtualKeyCode.Control,
                VirtualKeyCode.LeftMenu, VirtualKeyCode.RightMenu,
                VirtualKeyCode.Menu,
                VirtualKeyCode.Capital
            };

            foreach (var vKey in modifiers.Where(vKey => GetKeyState(vKey) > 0))
            {
                // Cap locks is a toggle key, so it needs to be pressed normally..
                if (vKey == VirtualKeyCode.Capital)
                {
                    KeyboardInputGenerator.KeyPresses(vKey);
                }
                else
                {
                    // For all the other modifier keys we only need to release them with a key up.
                    KeyboardInputGenerator.KeyUp(vKey);
                }
            }
        }

        private static void SendScanCodeInput(params char[] keyCodes)
        {
            var keyboardInputs = new Dapplo.Windows.Input.Structs.KeyboardInput[keyCodes.Length * 2];
            var index = 0;
            foreach (var keyCode in keyCodes)
            {
                var forKeyDown = Dapplo.Windows.Input.Structs.KeyboardInput.ForKeyDown((VirtualKeyCode)keyCode);
                forKeyDown.VirtualKeyCode = VirtualKeyCode.None;
                forKeyDown.KeyEventFlags = KeyEventFlags.Unicode;
                forKeyDown.ScanCode = (ScanCodes)keyCode;

                var forKeyUp = Dapplo.Windows.Input.Structs.KeyboardInput.ForKeyUp((VirtualKeyCode)keyCode);
                forKeyUp.VirtualKeyCode = VirtualKeyCode.None;
                forKeyUp.KeyEventFlags = KeyEventFlags.KeyUp | KeyEventFlags.Unicode;
                forKeyUp.ScanCode = (ScanCodes)keyCode;

                if ((keyCode & 0xFF00) == 0xE000)
                {
                    forKeyDown.KeyEventFlags |= KeyEventFlags.ExtendedKey;
                    forKeyUp.KeyEventFlags |= KeyEventFlags.ExtendedKey;
                }

                keyboardInputs[index++] = forKeyDown;
                keyboardInputs[index++] = forKeyUp;
            }

            NativeInput.SendInput(Input.CreateKeyboardInputs(keyboardInputs));
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        [ResourceExposure(ResourceScope.None)]
        private static extern ushort GetKeyState(VirtualKeyCode keyCode);
    }
}