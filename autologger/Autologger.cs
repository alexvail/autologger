using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using autologger.Configuration;

using Dapplo.Windows.Desktop;
using Dapplo.Windows.Input.Keyboard;
using Dapplo.Windows.Kernel32;
using Dapplo.Windows.Messages;
using Dapplo.Windows.Messages.Enumerations;
using Dapplo.Windows.Messages.Structs;

using OtpNet;

namespace autologger
{
    public class Autologger
    {
        private const int ReHookIntervalMs = 2000;

        private const int DelayAfterTypingActionMs = 500;

        private const int DelayAfterTabMs = 500;

        private readonly IReadOnlyList<KeyCombinationHandlerAction> _keyCombinationHandlerActionAssociations;

        private readonly ConcurrentQueue<IDisposable> _keyCombinationHandlerSubscriptions = new ConcurrentQueue<IDisposable>();

        private readonly BlockingCollection<Action> _unprocessedKeyboardActions = new BlockingCollection<Action>(new ConcurrentQueue<Action>());

        private readonly object _hookLock = new object();

        private int _hookThreadId;

        private int _executeReHook = 0;

        public Autologger(AutologgerConfiguration configuration)
        {
            var totp = new Totp(
                Base32Encoding.ToBytes(configuration.Otp.Base32Secret),
                configuration.Otp.Step,
                OtpHashMode.Sha1,
                configuration.Otp.Size);

            this._keyCombinationHandlerActionAssociations = KeyCombinationHandlerActionAssociationFactory.Create(
                (configuration.KeyCombinations.WritePassword, this.WritePasswordAction(configuration.Credentials.Password)),
                (configuration.KeyCombinations.WriteUsernamePasswordOtp, this.WriteUsernamePasswordOtpAction(configuration.Credentials.Username, configuration.Credentials.Password, totp)),
                (configuration.KeyCombinations.WritePasswordOtp, this.WritePasswordOtpAction(configuration.Credentials.Password, totp)),
                (configuration.KeyCombinations.WriteOtp, this.WriteOtpAction(totp)));
        }

        public void Run()
        {
            this.HookAll();

            var keyboardActionsThread = new Thread(this.ProcessKeyboardActions) { IsBackground = true, Priority = ThreadPriority.Highest };
            keyboardActionsThread.Start();

            var reHookTimerThread = new Thread(this.ReHookTimer) { IsBackground = true};
            reHookTimerThread.Start();

            try
            {
                Console.WriteLine($"Main loop. ThreadId={Kernel32Api.GetCurrentThreadId()}");

                while (true)
                {
                    MessageLoop.ProcessMessages(ProcessMessageHandler);
                    if (Interlocked.CompareExchange(ref this._executeReHook, 0, 1) == 1)
                    {
                        this.UnhookAll();
                        this.HookAll();
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        private Action<KeyboardHookEventArgs> WriteOtpAction(Totp totp)
        {
            return x => this.TypeStrings(ComputeTotp(totp));
        }

        private Action<KeyboardHookEventArgs> WritePasswordOtpAction(string password, Totp totp)
        {
            return x => this.TypeStrings(password, ComputeTotp(totp));
        }

        private Action<KeyboardHookEventArgs> WriteUsernamePasswordOtpAction(string username, string password, Totp totp)
        {
            return x => this.TypeStrings(username, password, ComputeTotp(totp));
        }

        private Action<KeyboardHookEventArgs> WritePasswordAction(string password)
        {
            return x => this.TypeStrings(password);
        }

        private void ReHookTimer()
        {
            while (true)
            {
                var rdpWindowIsActive = InteropWindowQuery.GetTopLevelWindows()
                                            .FirstOrDefault(
                                                x => x.GetClassname()
                                                     == "TscShellContainerClass" && x.IsVisible(true)) != null;

                if (rdpWindowIsActive)
                {
                    if (Interlocked.CompareExchange(ref this._executeReHook, 1, 0) == 0)
                    {
                        PostThreadMessage((uint)this._hookThreadId, (uint)WindowsMessages.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
                    }
                }

                Thread.Sleep(ReHookIntervalMs);
            }
        }

        private void ProcessKeyboardActions()
        {
            while (true)
            {
                try
                {
                    var action = this._unprocessedKeyboardActions.Take();
                    action();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());

                    throw;
                }
            }
        }

        private void HookAll()
        {
            lock (this._hookLock)
            {
                Interlocked.Exchange(ref this._hookThreadId, Kernel32Api.GetCurrentThreadId());

                Console.WriteLine($"Subscribed all hooks. ThreadId={this._hookThreadId}");

                this.SubscribeKeyHandlers();
            }
        }

        private void UnhookAll()
        {
            lock (this._hookLock)
            {
                this.DisposeSubscribedKeyHandlers();
                Console.WriteLine($"Unsubscribed all hooks. ThreadId={Kernel32Api.GetCurrentThreadId()}");
            }
        }

        private void SubscribeKeyHandlers()
        {
            if (this._keyCombinationHandlerSubscriptions.Any())
            {
                throw new Exception("There should not be any subscriptions when subscribing key handlers.");
            }

            foreach (var keyCombinationHandlerActionAssociation in this._keyCombinationHandlerActionAssociations)
            {
                this._keyCombinationHandlerSubscriptions.Enqueue(
                    KeyboardHook.KeyboardEvents.Where(keyCombinationHandlerActionAssociation.KeyboardHookEventHandler)
                        .Subscribe(keyCombinationHandlerActionAssociation.Action));
            }
        }

        private void DisposeSubscribedKeyHandlers()
        {
            while (this._keyCombinationHandlerSubscriptions.TryDequeue(out var s))
            {
                s.Dispose();
            }

            Console.WriteLine("Disposed all subscribed key handlers.");
        }

        private void TypeStrings(params string[] strings)
        {
            this._unprocessedKeyboardActions.Add(
                () =>
                {
                    KeyboardInput.ReleaseModifierKeys();

                    for (var i = 0; i < strings.Length; i++)
                    {
                        TypeString(strings[i]);

                        if (ShouldTab(i, strings.Length))
                        {
                            TypeTab();
                        }
                    }
                });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ProcessMessageHandler(ref Msg msg)
        {
            return msg.Message != WindowsMessages.WM_QUIT;
        }

        private static bool ShouldTab(int currentIndex, int arrayLength)
        {
            return arrayLength > 1 && (currentIndex == 0 || currentIndex < arrayLength - 1);
        }

        private static string ComputeTotp(Totp totp)
        {
            return totp.ComputeTotp(DateTime.UtcNow);
        }

        private static void TypeString(string text)
        {
            KeyboardInput.Type(text);
            Thread.Sleep(DelayAfterTypingActionMs);
        }

        private static void TypeTab()
        {
            KeyboardInput.Type("\t");
            Thread.Sleep(DelayAfterTabMs);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessage(uint threadId, uint msg, UIntPtr wParam, IntPtr lParam);
    }
}