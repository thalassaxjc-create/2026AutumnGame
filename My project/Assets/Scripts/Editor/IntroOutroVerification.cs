using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Structural verification for the intro/outro integration. Play Mode behavior still requires manual input testing.</summary>
public static class IntroOutroVerification
{
    const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    const string WorkshopScenePath = "Assets/Scenes/WorkshopScene.unity";

    [MenuItem("Tools/Game Flow/Validate Intro-Outro Setup")]
    public static void ValidateFromMenu()
    {
        try
        {
            ValidateProject();
            EditorUtility.DisplayDialog("Intro / Outro Validation", "Structural validation passed.", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Intro / Outro Validation", exception.Message, "OK");
        }
    }

    /// <summary>Batch entry: -executeMethod IntroOutroVerification.RunBatch</summary>
    public static void RunBatch()
    {
        try
        {
            ValidateProject();
            Debug.Log("INTRO_OUTRO_VALIDATION_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static void ValidateProject()
    {
        ValidateInputAction();
        ValidateBuildScenes();
        ValidateWorldScene();
        ValidateWorkshopScene();
        ValidateModalGate();
    }

    static void ValidateInputAction()
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        Require(actions != null, "InputSystem_Actions.inputactions is missing.");

        InputAction interact = actions.FindAction("Player/Interact", false);
        Require(interact != null, "Player/Interact action is missing.");
        Require(interact.bindings.Any(binding => binding.effectivePath == "<Keyboard>/e"),
            "Player/Interact is not bound to keyboard E.");
    }

    static void ValidateBuildScenes()
    {
        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        Require(enabledScenes.Contains(WorldScenePath), "WorldScene is not enabled in Build Settings.");
        Require(enabledScenes.Contains(WorkshopScenePath), "WorkshopScene is not enabled in Build Settings.");
    }

    static void ValidateWorldScene()
    {
        Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
        Require(scene.IsValid(), "WorldScene could not be opened.");
        RequireNoMissingScripts(scene);

        Require(FindAll<SceneTransitionManager>().Length == 1, "WorldScene must contain one SceneTransitionManager.");
        PlayerController[] players = FindAll<PlayerController>();
        Require(players.Length == 1, "WorldScene must contain one PlayerController.");
        Require(FindAll<InteractionPromptUI>().Length == 1, "WorldScene must contain one InteractionPromptUI.");
        Require(FindAll<DoorTransition>().Length == 1, "WorldScene door integration changed unexpectedly.");

        NpcDialogueInteraction[] npcs = FindAll<NpcDialogueInteraction>();
        Require(npcs.Length == 1, "WorldScene must contain one development NPC interaction.");
        NpcDialogueInteraction npc = npcs[0];
        Require(npc.DialogueEntryPointComponent is IDialogueEntryPoint,
            "The NPC dialogue entry component does not implement IDialogueEntryPoint.");
        Require(npc.GetComponent<BoxCollider>() != null && npc.GetComponent<BoxCollider>().isTrigger,
            "The NPC requires a trigger BoxCollider.");
        Require(npc.GetComponent<Rigidbody>() != null && npc.GetComponent<Rigidbody>().isKinematic,
            "The NPC requires a kinematic Rigidbody for reliable trigger messages.");

        SettlementExitController[] settlementControllers = FindAll<SettlementExitController>();
        Require(settlementControllers.Length == 1,
            "WorldScene must contain one SettlementExitController.");
        Require(FindAll<SettlementDevelopmentHarness>().Length == 1,
            "WorldScene must contain one SettlementDevelopmentHarness.");

        ValidateDialogueLifecycle(npc, players[0]);
        ValidateSettlementLifecycle(settlementControllers[0], players[0]);
    }

    static void ValidateWorkshopScene()
    {
        Scene scene = EditorSceneManager.OpenScene(WorkshopScenePath, OpenSceneMode.Single);
        Require(scene.IsValid(), "WorkshopScene could not be opened.");
        RequireNoMissingScripts(scene);

        Require(FindAll<SceneTransitionManager>().Length == 1, "WorkshopScene must contain one SceneTransitionManager.");
        Require(FindAll<PlayerController>().Length == 1, "WorkshopScene must contain one PlayerController.");
        Require(FindAll<DoorTransition>().Length == 2, "WorkshopScene must retain both door transitions.");
        Require(FindAll<WeavingStationTrigger>().Length == 1, "Weaving station trigger is missing.");
        Require(FindAll<WeavingMinigameController>().Length == 1, "Weaving minigame controller is missing.");
        Require(FindAll<LacquerStationTrigger>().Length == 1, "Lacquer station trigger is missing.");
        Require(FindAll<LacquerMinigameController>().Length == 1, "Lacquer minigame controller is missing.");
    }

    static void ValidateModalGate()
    {
        object owner = new object();
        Require(GameFlowModalState.Acquire(owner), "Modal gate did not accept a new owner.");
        Require(GameFlowModalState.IsInteractionBlocked, "Modal gate did not block interaction.");
        GameFlowModalState.Release(owner, false);
        GameFlowModalState.NotifyInteractReleased();
        Require(!GameFlowModalState.IsInteractionBlocked, "Modal gate did not clear after release.");
    }

    static void ValidateDialogueLifecycle(NpcDialogueInteraction npc, PlayerController player)
    {
        TemporaryDialogueEntryPoint placeholder = npc.DialogueEntryPointComponent as TemporaryDialogueEntryPoint;
        Require(placeholder != null, "WorldScene development NPC is not using the temporary dialogue adapter.");

        FieldInfo playerInRange = typeof(NpcDialogueInteraction).GetField(
            "playerInRange", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(playerInRange != null, "Could not inspect NPC range state.");

        bool playerWasEnabled = player.enabled;
        playerInRange.SetValue(npc, player);
        for (int cycle = 0; cycle < 2; cycle++)
        {
            Require(npc.TryBeginInteraction(), "NPC dialogue did not begin on cycle " + (cycle + 1) + ".");
            Require(npc.IsDialogueOpen && placeholder.IsOpen, "Dialogue did not enter its open state.");
            Require(!player.enabled, "Dialogue did not disable player control.");
            Require(GameFlowModalState.IsInteractionBlocked, "Dialogue did not block world interaction.");

            placeholder.CloseDialogue();
            Require(!npc.IsDialogueOpen && !placeholder.IsOpen, "Dialogue did not close cleanly.");
            Require(player.enabled == playerWasEnabled, "Dialogue did not restore the previous player state.");
            GameFlowModalState.NotifyInteractReleased();
        }

        playerInRange.SetValue(npc, null);
        Transform generatedCanvas = npc.transform.Find("TemporaryDialogueCanvas");
        if (generatedCanvas != null)
            UnityEngine.Object.DestroyImmediate(generatedCanvas.gameObject);
    }

    static void ValidateSettlementLifecycle(SettlementExitController controller, PlayerController player)
    {
        bool playerWasEnabled = player.enabled;
        GameObject view = new GameObject("SettlementVerificationView");
        view.SetActive(false);

        for (int cycle = 0; cycle < 2; cycle++)
        {
            Require(controller.BeginSettlement(view), "Settlement did not begin on cycle " + (cycle + 1) + ".");
            Require(controller.IsSettlementActive && view.activeSelf, "Settlement view did not become active.");
            Require(!player.enabled, "Settlement did not disable player control.");
            Require(GameFlowModalState.IsInteractionBlocked, "Settlement did not block world interaction.");

            controller.ConfirmAndExit();
            controller.ConfirmAndExit();
            Require(!controller.IsSettlementActive && !view.activeSelf, "Settlement did not close cleanly.");
            Require(player.enabled == playerWasEnabled, "Settlement did not restore the previous player state.");
            GameFlowModalState.NotifyInteractReleased();
        }

        UnityEngine.Object.DestroyImmediate(view);
    }

    static T[] FindAll<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    static void RequireNoMissingScripts(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                Require(missing == 0, "Missing script on " + child.name + " in " + scene.name + ".");
            }
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
