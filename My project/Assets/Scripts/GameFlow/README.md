# Intro / Outro Game-Flow Integration

This folder owns only the entry and exit boundaries of one order cycle. It does not create orders,
calculate settlement values, change inventory, or replace either minigame's result screen.

## Dialogue integration

`NpcDialogueInteraction` uses the project's existing `Player/Interact` action,
`SceneTransitionManager.TryConsumeInteractPress()`, and `InteractionPromptUI`. Assign a
`MonoBehaviour` that implements `IDialogueEntryPoint` to its **Dialogue Entry Point** field.

```csharp
public sealed class DialogueBridge : MonoBehaviour, IDialogueEntryPoint
{
    public bool TryBeginDialogue(NpcDialogueInteraction source, System.Action onDialogueClosed)
    {
        // Open the teammate-owned dialogue/order UI here.
        // Call onDialogueClosed exactly once after that UI closes.
        dialogueSystem.OpenForNpc(source.gameObject, onDialogueClosed);
        return true;
    }
}
```

Return `false` if the dialogue cannot open; the NPC interaction immediately restores the player and
shared interaction state. `TemporaryDialogueEntryPoint` is the current development adapter in
`WorldScene`; replace that component/reference when the real system is ready.

## Settlement integration

Keep `SettlementExitController` on an always-active scene object, not on the panel it will deactivate.
The order/settlement owner has two supported entry paths:

- Assign its view root in the Inspector and call `OpenSettlement()`.
- Call `BeginSettlement(yourSettlementRoot)` after creating/displaying a teammate-owned view.

Wire the settlement confirm/close button to `ConfirmAndExit()`. This call is idempotent: repeated
confirmations cannot start multiple transitions. Use `CloseAndResume()` for an explicit close-only
button. Choose **Close And Resume** to avoid a scene reload, or **Load Configured Scene** and set both
the scene name and spawn ID to return through `SceneTransitionManager`.

The controller records and restores the previous `PlayerController.enabled`, cursor lock/visibility,
settlement view visibility, and shared interaction gate. If the player was already disabled by another
system, it remains disabled rather than being incorrectly enabled.

`SettlementDevelopmentHarness` is compiled for Editor/Development behavior only. In `WorldScene`, F9
simulates order completion; Enter, Space, or Esc confirms the settlement exit. It performs no economy,
inventory, scoring, or order logic.

## Manual Play Mode verification

1. Open `Assets/Scenes/WorldScene.unity` in Unity 6000.3.27f1 and enter Play Mode.
2. Move with WASD/arrow keys. The green capsule at `(7, 1, -10)` is the temporary order NPC.
3. Approach it and confirm **Press E to talk** appears. Press E once; the placeholder dialogue opens
   and movement stops.
4. Press E, Enter, or Esc. Confirm the dialogue closes and movement immediately resumes. Walk away and
   approach again to verify the prompt/callback has no stale state.
5. Press F9 to simulate order completion. Confirm the settlement placeholder appears, movement stops,
   and the cursor is unlocked/visible.
6. Press Enter, Space, or Esc repeatedly. Confirm only one close occurs, the player can move again, and
   F9 can start a second clean settlement cycle.
7. Walk to the workshop entrance, press E, and verify `WorkshopScene` loads at `WorkshopEntrance`.
8. In `WorkshopScene`, verify both weaving and lacquer prompts still open their existing minigames,
   their result panels still work, and exiting restores movement. Verify either workshop door still
   returns to `WorldScene` at `WorkshopExterior`.

For a scene-return settlement test, temporarily set the controller's return mode to **Load Configured
Scene**, select an enabled scene and valid spawn ID, enter Play Mode from the other gameplay scene, and
repeat steps 5-6. Restore **Close And Resume** afterward unless the production order flow requires a load.
