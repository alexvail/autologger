using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace autologger
{
    public static class MessageLoop
    {
        public delegate bool MessageProc(ref MSG message);

        public static void Run(MessageProc handler = null)
        {
            MSG msg;

            if (PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                if (msg.Message == WM_QUIT)
                    return;

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);

                if (handler != null && !handler(ref msg))
                {
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr Hwnd;

            public uint Message;

            public IntPtr WParam;

            public IntPtr LParam;

            public uint Time;

            public Point Point;
        }

        const uint PM_NOREMOVE = 0;

        const uint PM_REMOVE = 1;

        const uint WM_QUIT = 0x0012;

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hwnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    }
}