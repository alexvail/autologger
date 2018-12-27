// Visual Studio Shared Project
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Runtime.InteropServices;

namespace autologger
{
    /// <summary>
    ///     Unmanaged API wrappers.
    /// </summary>
    public static class NativeMethods
    {
        //User32 wrappers cover API's used for Mouse input

        #region User32

        // Two special bitmasks we define to be able to grab
        // shift and character information out of a VKey.
        public const int VKeyShiftMask = 0x0100;

        public const int VKeyCharMask = 0x00FF;

        // Various Win32 constants
        public const int KeyeventfExtendedkey = 0x0001;

        public const int KeyeventfKeyup = 0x0002;

        public const int KeyeventfScancode = 0x0008;

        public const int MouseeventfVirtualdesk = 0x4000;

        public const int SMXvirtualscreen = 76;

        public const int SMYvirtualscreen = 77;

        public const int SMCxvirtualscreen = 78;

        public const int SMCyvirtualscreen = 79;

        public const int XButton1 = 0x0001;

        public const int XButton2 = 0x0002;

        public const int WheelDelta = 120;

        public const int InputMouse = 0;

        public const int InputKeyboard = 1;

        // Various Win32 data structures
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;

            public INPUTUNION union;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mouseInput;

            [FieldOffset(0)]
            public KEYBDINPUT keyboardInput;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;

            public int dy;

            public int mouseData;

            public int dwFlags;

            public int time;

            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public short wVk;

            public short wScan;

            public int dwFlags;

            public int time;

            public IntPtr dwExtraInfo;
        }

        [Flags]
        public enum SendMouseInputFlags
        {
            Move = 0x0001,

            LeftDown = 0x0002,

            LeftUp = 0x0004,

            RightDown = 0x0008,

            RightUp = 0x0010,

            MiddleDown = 0x0020,

            MiddleUp = 0x0040,

            XDown = 0x0080,

            XUp = 0x0100,

            Wheel = 0x0800,

            Absolute = 0x8000
        }

        // Importing various Win32 APIs that we need for input
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int MapVirtualKey(int nVirtKey, int nMapType);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SendInput(int nInputs, ref INPUT mi, int cbSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern short VkKeyScan(char ch);

        #endregion
    }
}