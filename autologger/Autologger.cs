using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;

using Dapplo.Windows.Desktop;
using Dapplo.Windows.Input.Keyboard;

using Microsoft.Extensions.Configuration;

using OtpNet;

using Timer = System.Timers.Timer;

namespace autologger
{
    public class Autologger
    {
        public static string Username;

        public static string Password;

        public static string Base32Secret;

        private readonly IDictionary<IKeyboardHookEventHandler, Action<KeyboardHookEventArgs>> _keyCombinationHandlers;

        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        private readonly HashSet<IDisposable> _subscriptions = new HashSet<IDisposable>();

        private readonly Totp _totp;

        private bool _rdpWindowIsActive;

        public Autologger(IConfiguration configuration)
        {
            Username = configuration.GetSection("username").Value;
            Password = configuration.GetSection("password").Value;
            Base32Secret = configuration.GetSection("base32secret").Value;

            this._totp = new Totp(Base32Encoding.ToBytes(Base32Secret), 30);

            this._keyCombinationHandlers = new Dictionary<IKeyboardHookEventHandler, Action<KeyboardHookEventArgs>>
            {
                { new KeyCombinationHandler(KeyHelper.VirtualKeyCodesFromString("CTRL+1")) { IgnoreInjected = false }, this.WritePassword },
                { new KeyCombinationHandler(KeyHelper.VirtualKeyCodesFromString("CTRL+2")) { IgnoreInjected = false }, this.WriteUsernamePasswordOtp },
                { new KeyCombinationHandler(KeyHelper.VirtualKeyCodesFromString("CTRL+3")) { IgnoreInjected = false }, this.WritePasswordOtp }
            };
        }

        public void Main()
        {
            this.ReHook();

            var timer = new Timer(500);

            try
            {
                Console.WriteLine($"Main loop. ThreadId={Thread.CurrentThread.ManagedThreadId}");
                
                timer.Elapsed += this.MstscReHookTimer;
                timer.AutoReset = true;
                timer.Enabled = true;

                while (true)
                {
                    MessageLoop.Run();

                    if (this._queue.TryDequeue(out var action))
                    {
                        action?.Invoke();
                    }
                }
            }
            finally
            {
                timer.Elapsed -= this.MstscReHookTimer;
                this.DisposeSubscribedHandlers();
            }
        }

        private void MstscReHookTimer(object sender, ElapsedEventArgs args)
        {
            var rdpWindow = InteropWindowQuery.GetTopLevelWindows()
                .FirstOrDefault(
                    x => x.GetClassname()
                         == "TscShellContainerClass" && x.GetInfo(true).IsActive);

            if (rdpWindow != null)
            {
                if (!this._rdpWindowIsActive)
                {
                    this._rdpWindowIsActive = true;

                    // Queue a rehook on main thread
                    this._queue.Enqueue(this.ReHook);
                }
            }
            else
            {
                this._rdpWindowIsActive = false;
            }
        }

        private void ReHook()
        {
            Console.WriteLine($"Rehooked. ThreadId={Thread.CurrentThread.ManagedThreadId}");
            this.DisposeSubscribedHandlers();
            this.SubscribeHandlers(this._keyCombinationHandlers);
        }

        private void WritePassword(KeyboardHookEventArgs x)
        {
            this._queue.Enqueue(Keyboard.Reset);
            this._queue.Enqueue(() => Task.Run(() => WritePassword()).Wait());
        }

        private void WriteUsernamePasswordOtp(KeyboardHookEventArgs x)
        {
            this._queue.Enqueue(Keyboard.Reset);
            this._queue.Enqueue(() => Task.Run(() => WriteUsername()).Wait());
            this._queue.Enqueue(() => Task.Run(() => Tab()).Wait());
            this._queue.Enqueue(() => Task.Run(() => WritePassword()).Wait());
            this._queue.Enqueue(() => Task.Run(() => Tab()).Wait());
            this._queue.Enqueue(() => Task.Run(() => this.WriteOtp()).Wait());
        }

        private void WritePasswordOtp(KeyboardHookEventArgs x)
        {
            this._queue.Enqueue(Keyboard.Reset);
            this._queue.Enqueue(() => Task.Run(() => WritePassword()).Wait());
            this._queue.Enqueue(() => Task.Run(() => Tab()).Wait());
            this._queue.Enqueue(() => Task.Run(() => this.WriteOtp()).Wait());
        }

        private void SubscribeHandlers(IDictionary<IKeyboardHookEventHandler, Action<KeyboardHookEventArgs>> keyHandlers)
        {
            var keyboardEvents = KeyboardHook.KeyboardEvents;
            foreach (var keyValuePair in keyHandlers)
            {
                var keyCombinationHandler = keyValuePair.Key;
                var action = keyValuePair.Value;

                this._subscriptions.Add(keyboardEvents.Where(keyCombinationHandler).Subscribe(action));
            }
        }

        private void DisposeSubscribedHandlers()
        {
            Console.WriteLine("Disposing subscribed handlers");

            foreach (var s in this._subscriptions)
            {
                s.Dispose();
            }

            this._subscriptions?.Clear();
        }

        private void WriteOtp()
        {
            Keyboard.Type(this.ComputeTotp());
            Thread.Sleep(350);
        }

        private string ComputeTotp()
        {
            return this._totp.ComputeTotp(DateTime.UtcNow);
        }

        private static void WritePassword()
        {
            Keyboard.Type(Password);
            Thread.Sleep(350);
        }

        private static void WriteUsername()
        {
            Keyboard.Type(Username);
            Thread.Sleep(350);
        }

        private static void Tab()
        {
            Keyboard.Type(Key.Tab);
            Thread.Sleep(450);
        }
    }
}