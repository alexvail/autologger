using System;

using Dapplo.Windows.Input.Keyboard;

namespace autologger
{
    public class KeyCombinationHandlerAction
    {
        public KeyCombinationHandlerAction(IKeyboardHookEventHandler keyboardHookEventHandler, Action<KeyboardHookEventArgs> action)
        {
            this.KeyboardHookEventHandler = keyboardHookEventHandler;
            this.Action = action;
        }

        public IKeyboardHookEventHandler KeyboardHookEventHandler { get; set; }

        public Action<KeyboardHookEventArgs> Action { get; set; }
    }
}