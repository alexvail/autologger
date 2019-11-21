using System;

using Dapplo.Windows.Input.Keyboard;

namespace autologger
{
    internal class KeyCombinationHandlerAction
    {
        public KeyCombinationHandlerAction(IKeyboardHookEventHandler keyboardHookEventHandler, Action<KeyboardHookEventArgs> action)
        {
            this.KeyboardHookEventHandler = keyboardHookEventHandler;
            this.Action = action;
        }

        public IKeyboardHookEventHandler KeyboardHookEventHandler { get; }

        public Action<KeyboardHookEventArgs> Action { get; }
    }
}