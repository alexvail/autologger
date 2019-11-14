using System;
using System.Collections.Generic;
using System.Linq;

using Dapplo.Windows.Input.Keyboard;

namespace autologger
{
    internal static class KeyCombinationHandlerActionAssociationFactory
    {
        public static IReadOnlyList<KeyCombinationHandlerAction> Create(
            params (string, Action<KeyboardHookEventArgs>)[] keyCombinationHandlerActionAssociations)
        {
            return keyCombinationHandlerActionAssociations.Select(
                    x => new KeyCombinationHandlerAction(
                        CreateKeyCombinationHandler(x.Item1),
                        x.Item2))
                .ToList()
                .AsReadOnly();
        }

        private static IKeyboardHookEventHandler CreateKeyCombinationHandler(string keyCombination)
        {
            return new KeyCombinationHandler(KeyHelper.VirtualKeyCodesFromString(keyCombination)) { IgnoreInjected = false };
        }
    }
}