using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates modal game-flow screens with the shared world interaction input.
/// Owners must release their lock when their modal flow closes.
/// </summary>
public static class GameFlowModalState
{
    static readonly HashSet<object> Owners = new HashSet<object>();

    public static bool IsModalOpen => Owners.Count > 0;
    public static bool WaitForInteractRelease { get; private set; }
    public static bool IsInteractionBlocked => IsModalOpen || WaitForInteractRelease;

    public static bool Acquire(object owner)
    {
        if (owner == null)
            return false;

        bool added = Owners.Add(owner);
        if (added)
            InteractionPromptUI.HideImmediate();
        return added;
    }

    public static void Release(object owner, bool waitForInteractRelease = true)
    {
        if (owner == null || !Owners.Remove(owner))
            return;

        if (waitForInteractRelease)
            WaitForInteractRelease = true;
        InteractionPromptUI.HideImmediate();
    }

    /// <summary>Called after the shared Interact action is released.</summary>
    public static void NotifyInteractReleased()
    {
        WaitForInteractRelease = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        Owners.Clear();
        WaitForInteractRelease = false;
    }
}
