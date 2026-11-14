# Intro / Outro Game-Flow Integration (game_v2)

This folder owns only the entry and exit boundaries of one order cycle. It does not create orders,
calculate settlement values, change inventory, or replace scene transitions. The implementation is
attached to the existing `村口大爷` NPC in `WorldScene`; it does not create or reposition an NPC.

## Dialogue/order integration

`NpcDialogueInteraction` uses the existing `Player/Interact` Input System action,
`SceneTransitionManager.TryConsumeInteractPress()`, and `InteractionPromptUI`. It uses a configurable
horizontal interaction radius, so no trigger or Rigidbody is added to the NPC.

Assign a `MonoBehaviour` that implements `IDialogueEntryPoint` to **Dialogue Entry Point**:

```csharp
public sealed class DialogueBridge : MonoBehaviour, IDialogueEntryPoint
{
    public bool TryBeginDialogue(NpcDialogueInteraction source, System.Action onDialogueClosed)
    {
        // Open the teammate-owned dialogue/order UI for source.GetComponent<NpcCharacter>().
        dialogueSystem.OpenForNpc(source.gameObject, onDialogueClosed);
        return true;
    }
}
```

Return `false` if the dialogue cannot open. Invoke `onDialogueClosed` exactly once after the UI closes;
the adapter then restores the player's prior enabled state and releases shared interaction input.
`TemporaryDialogueEntryPoint` is the current development adapter. Replace its reference when the real
dialogue/order system is available.

## Settlement integration

Keep `SettlementExitController` on an always-active object, not on the panel it will deactivate.
The settlement owner may either assign a view root and call `OpenSettlement()`, or call
`BeginSettlement(yourSettlementRoot)`. Wire confirmation/close to `ConfirmAndExit()`.

The call is idempotent: repeated confirmations cannot start multiple transitions. Use
`CloseAndResume()` for an explicit close-only path. **Close And Resume** is the default and avoids an
unnecessary reload. **Load Configured Scene** delegates to the existing `SceneTransitionManager` and
`SceneTransitionFader`.

Valid game_v2 return pairs include:

- `WorldScene` / `WorkshopNorth`
- `WorldScene` / `WorkshopSouth`
- `WorldScene` / `HomeExterior`
- `WorldScene` / `HomeExteriorEast`
- `WorkshopScene` / `WorkshopEntrance` or `WorkshopEntranceSouth`
- `HomeScene` / `HomeEntrance` or `HomeEntranceEast`

The controller restores the previous `PlayerController.enabled`, cursor lock/visibility, settlement
view visibility, and shared interaction gate. It never enables a player that was disabled before the
settlement began.

`SettlementDevelopmentHarness` is active only in the Editor or Development builds. F9 simulates order
completion; Enter, Space, or Esc confirms. It contains no economy, inventory, scoring, or order logic.

## Exact manual Play Mode verification

Use Unity `6000.3.27f1` and open the `Game/` project.

1. Open `Assets/Scenes/WorldScene.unity`. Confirm the Player begins at `(72, 1, 76)` and the two
   existing NPCs (`舅舅` and `村口大爷`) are visible at their game_v2 positions.
2. Enter Play Mode and move with WASD/arrow keys. Approach `村口大爷` near `(77.4, 1, 71.2)`.
3. Confirm **Press E to talk** appears. Press E once; confirm the temporary dialogue opens and player
   movement stops.
4. Release E, then press E, Enter, or Esc. Confirm dialogue closes and movement resumes. Walk out of
   range and back, then repeat once to verify no stale state.
5. Press F9. Confirm the development settlement panel opens, movement stops, and the cursor is
   unlocked/visible. Press Enter, Space, or Esc repeatedly; confirm there is one close, movement and
   prior cursor state return, and F9 starts a second clean cycle.
6. Walk into each World door trigger without pressing E. Confirm the existing fade plays and the
   correct `WorkshopScene` or `HomeScene` entrance is used. Walk back through both variants and confirm
   automatic transitions still work.
7. During dialogue or settlement, confirm no door transition starts. After closing, confirm automatic
   door behavior resumes.
8. To test configured settlement return, change the controller to **Load Configured Scene**, choose a
   valid pair above, start from another gameplay scene, and repeat step 5. Restore **Close And Resume**
   afterward unless production flow requires a scene change.

The Editor menu item **Tools > Game Flow > Verify Intro Outro game_v2** performs compilation-safe scene
and configuration checks. It does not replace the manual movement/input/fade Play Mode pass.
