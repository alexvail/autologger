using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        private readonly IReadOnlyList<KeyCombinationHandlerAction> _keyCombinationHandlerActions;

        private readonly BlockingCollection<Action> _unprocessedKeyboardActions = new BlockingCollection<Action>(new ConcurrentQueue<Action>());

        private readonly ConcurrentStack<IDisposable> _subscriptions = new ConcurrentStack<IDisposable>();
        
        private readonly object _hookLock = new object();

        private int _hookThreadId;

        private int _executeReHook = 0;

        private readonly Totp _totp;

        private readonly AutologgerConfiguration _config;

        public Autologger(AutologgerConfiguration configuration)
        {
            this._config = configuration;
            this._totp = new Totp(
                Base32Encoding.ToBytes(configuration.Otp.Base32Secret),
                configuration.Otp.Step,
                OtpHashMode.Sha1,
                configuration.Otp.Size);

            this._keyCombinationHandlerActions = new List<KeyCombinationHandlerAction>
            {
                new KeyCombinationHandlerAction(KeyCombinationHandlerActionAssociationFactory.CreateKeyCombinationHandler(configuration.KeyCombinations.WritePassword), this.WritePasswordEvent),
                new KeyCombinationHandlerAction(KeyCombinationHandlerActionAssociationFactory.CreateKeyCombinationHandler(configuration.KeyCombinations.WriteUsernamePasswordOtp), this.WriteUsernamePasswordOtpEvent),
                new KeyCombinationHandlerAction(KeyCombinationHandlerActionAssociationFactory.CreateKeyCombinationHandler(configuration.KeyCombinations.WritePasswordOtp), this.WritePasswordOtpEvent),
                new KeyCombinationHandlerAction(KeyCombinationHandlerActionAssociationFactory.CreateKeyCombinationHandler(configuration.KeyCombinations.WriteOtp), this.WriteOtpEvent),
            };
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

        private void WritePasswordEvent(KeyboardHookEventArgs hookEventArgs)
        {
            this._unprocessedKeyboardActions.Add(this.WritePasswordAction);
        }

        private void WriteUsernamePasswordOtpEvent(KeyboardHookEventArgs hookEventArgs)
        {
            this._unprocessedKeyboardActions.Add(this.WriteUsernamePasswordOtpAction);
        }

        private void WritePasswordOtpEvent(KeyboardHookEventArgs hookEventArgs)
        {
            this._unprocessedKeyboardActions.Add(this.WritePasswordOtpAction);
        }
        private void WriteOtpEvent(KeyboardHookEventArgs hookEventArgs)
        {
            this._unprocessedKeyboardActions.Add(this.WriteOtp);
        }

        private void WriteOtp()
        {
            TypeStrings(this.ComputeTotp());
        }

        private void WritePasswordOtpAction()
        {
            TypeStrings(this._config.Credentials.Password, this.ComputeTotp());
        }

        private void WriteUsernamePasswordOtpAction()
        {
            TypeStrings(this._config.Credentials.Username, this._config.Credentials.Password, this.ComputeTotp());
        }

        private void WritePasswordAction()
        {
            TypeStrings(this._config.Credentials.Password);
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
                    Interlocked.Exchange(ref this._executeReHook, 1);
                    PostThreadMessage((uint)this._hookThreadId, (uint)WindowsMessages.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
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

                Debug.WriteLine($"Subscribed all hooks. ThreadId={this._hookThreadId}");

                this.SubscribeKeyHandlers();
            }
        }

        private void UnhookAll()
        {
            lock (this._hookLock)
            {
                this.DisposeSubscribedKeyHandlers();
                Debug.WriteLine($"Unsubscribed all hooks. ThreadId={Kernel32Api.GetCurrentThreadId()}");
            }
        }

        private void SubscribeKeyHandlers()
        {
            if (!this._subscriptions.IsEmpty)
            {
                throw new Exception("There should not be any subscriptions when subscribing key handlers.");
            }

            foreach (var keyCombinationHandlerAction in this._keyCombinationHandlerActions)
            {
                var sub = KeyboardHook.KeyboardEvents.Where(keyCombinationHandlerAction.KeyboardHookEventHandler)
                    .Subscribe(keyCombinationHandlerAction.Action);
                this._subscriptions.Push(sub);
            }
        }

        private void DisposeSubscribedKeyHandlers()
        {
            while (this._subscriptions.TryPop(out var s))
            {
                s.Dispose();
            }

            if (!this._subscriptions.IsEmpty)
            {
                throw new Exception("Should be empty...");
            }

            Debug.WriteLine("Disposed all subscribed key handlers.");
        }

        private static void TypeStrings(params string[] strings)
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

        private string ComputeTotp()
        {
            return this._totp.ComputeTotp(DateTime.UtcNow);
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