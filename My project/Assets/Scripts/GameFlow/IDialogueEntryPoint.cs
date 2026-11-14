using System;

/// <summary>
/// Adapter implemented by the teammate-owned dialogue/order system.
/// Return true after accepting the request, and invoke onDialogueClosed exactly once when its UI closes.
/// </summary>
public interface IDialogueEntryPoint
{
    bool TryBeginDialogue(NpcDialogueInteraction source, Action onDialogueClosed);
}
