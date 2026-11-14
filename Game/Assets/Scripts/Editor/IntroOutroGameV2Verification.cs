using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class IntroOutroGameV2Verification
{
    const string WorldPath = "Assets/Scenes/WorldScene.unity";
    const string WorkshopPath = "Assets/Scenes/WorkshopScene.unity";
    const string HomePath = "Assets/Scenes/HomeScene.unity";
    const float PositionTolerance = 0.01f;

    [MenuItem("Tools/Game Flow/Verify Intro Outro game_v2")]
    public static void RunFromMenu()
    {
        try
        {
            VerifyAll();
            Debug.Log("[IntroOutro game_v2] Verification passed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunBatch()
    {
        try
        {
            VerifyAll();
            Debug.Log("[IntroOutro game_v2] Batch verification passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static void VerifyAll()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            VerifyInputAndBuildSettings();
            VerifyWorld();
            VerifyWorkshop();
            VerifyHome();
        }
        finally
        {
            if (originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    static void VerifyInputAndBuildSettings()
    {
        InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        Require(input != null, "InputSystem_Actions.inputactions is missing.");
        InputAction interact = input.FindAction("Player/Interact", false);
        Require(interact != null, "Player/Interact action is missing.");
        Require(interact.bindings.Any(binding => binding.effectivePath == "<Keyboard>/e"),
            "Player/Interact must retain its E binding.");

        HashSet<string> enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToHashSet();
        Require(enabledScenes.Contains(WorldPath), "WorldScene is not enabled in Build Settings.");
        Require(enabledScenes.Contains(WorkshopPath), "WorkshopScene is not enabled in Build Settings.");
        Require(enabledScenes.Contains(HomePath), "HomeScene is not enabled in Build Settings.");
    }

    static void VerifyWorld()
    {
        Scene scene = OpenAndCheckScene(WorldPath);
        RequireCount<PlayerController>(1, "WorldScene PlayerController count");
        RequireCount<SceneTransitionManager>(1, "WorldScene SceneTransitionManager count");
        RequireCount<InteractionPromptUI>(1, "WorldScene InteractionPromptUI count");
        RequireCount<DoorTransition>(4, "WorldScene automatic door count");
        RequireCount<NpcCharacter>(2, "WorldScene NPC count");
        RequireCount<NpcDialogueInteraction>(1, "WorldScene dialogue interaction count");
        RequireCount<TemporaryDialogueEntryPoint>(1, "WorldScene temporary dialogue adapter count");
        RequireCount<SettlementExitController>(1, "WorldScene settlement exit count");
        RequireCount<SettlementDevelopmentHarness>(1, "WorldScene settlement test harness count");

        PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        RequirePosition(player.transform.position, new Vector3(72f, 1f, 76f), "WorldScene initial player");

        NpcCharacter elder = FindNpc("村口大爷");
        NpcCharacter uncle = FindNpc("舅舅");
        RequirePosition(elder.transform.position, new Vector3(77.4f, 1f, 71.2f), "村口大爷");
        RequirePosition(uncle.transform.position, new Vector3(28.6f, 1f, -33.6f), "舅舅");
        Require(elder.GetComponent<Renderer>() != null && elder.GetComponent<Renderer>().enabled,
            "村口大爷 renderer must remain enabled.");
        Require(uncle.GetComponent<Renderer>() != null && uncle.GetComponent<Renderer>().enabled,
            "舅舅 renderer must remain enabled.");

        NpcDialogueInteraction interaction = elder.GetComponent<NpcDialogueInteraction>();
        Require(interaction != null, "Dialogue interaction must be attached to the existing 村口大爷.");
        Require(interaction.DialogueEntryPointComponent == elder.GetComponent<TemporaryDialogueEntryPoint>(),
            "The development dialogue adapter reference is not configured.");
        Require(uncle.GetComponent<NpcDialogueInteraction>() == null,
            "The unrelated 舅舅 NPC must not receive order interaction wiring.");
        Require(elder.GetComponents<Collider>().Length == 1 && elder.GetComponent<CapsuleCollider>() != null,
            "NPC physics changed; only the original CapsuleCollider should remain.");
        Require(elder.GetComponent<Rigidbody>() == null, "NPC must not gain a Rigidbody.");

        VerifyAutomaticDoors(new[]
        {
            "HomeScene/HomeEntranceEast",
            "WorkshopScene/WorkshopEntranceSouth",
            "HomeScene/HomeEntrance",
            "WorkshopScene/WorkshopEntrance"
        });

        SettlementExitController settlement = UnityEngine.Object.FindFirstObjectByType<SettlementExitController>();
        Require(settlement.ReturnMode == SettlementReturnMode.CloseAndResume,
            "Settlement should avoid reloading the current scene by default.");
        Require(settlement.ReturnSceneName == SceneTransitionState.WorldSceneName,
            "Settlement return scene is not configurable from a valid default.");
        Require(settlement.ReturnSpawnId == SceneTransitionState.SpawnWorkshopNorth,
            "Settlement return spawn is not a valid game_v2 WorldScene spawn.");
        Require(!scene.isDirty, "WorldScene became dirty during read-only verification.");
    }

    static void VerifyWorkshop()
    {
        Scene scene = OpenAndCheckScene(WorkshopPath);
        RequireCount<PlayerController>(1, "WorkshopScene PlayerController count");
        RequireCount<SceneTransitionManager>(1, "WorkshopScene SceneTransitionManager count");
        RequireCount<InteractionPromptUI>(1, "WorkshopScene InteractionPromptUI count");
        RequireCount<DoorTransition>(2, "WorkshopScene automatic door count");
        RequireCount<NpcDialogueInteraction>(0, "WorkshopScene migrated dialogue interaction count");
        VerifyAutomaticDoors(new[] { "WorldScene/WorkshopNorth", "WorldScene/WorkshopSouth" });
        Require(!scene.isDirty, "WorkshopScene became dirty during read-only verification.");
    }

    static void VerifyHome()
    {
        Scene scene = OpenAndCheckScene(HomePath);
        RequireCount<PlayerController>(1, "HomeScene PlayerController count");
        RequireCount<SceneTransitionManager>(1, "HomeScene SceneTransitionManager count");
        RequireCount<InteractionPromptUI>(1, "HomeScene InteractionPromptUI count");
        RequireCount<DoorTransition>(2, "HomeScene automatic door count");
        RequireCount<NpcDialogueInteraction>(0, "HomeScene migrated dialogue interaction count");
        VerifyAutomaticDoors(new[] { "WorldScene/HomeExterior", "WorldScene/HomeExteriorEast" });
        Require(!scene.isDirty, "HomeScene became dirty during read-only verification.");
    }

    static Scene OpenAndCheckScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Require(scene.IsValid() && scene.isLoaded, "Could not open " + path);

        int missingScripts = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            missingScripts += CountMissingScripts(root);
        Require(missingScripts == 0, path + " contains " + missingScripts + " missing script(s).");
        return scene;
    }

    static int CountMissingScripts(GameObject gameObject)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
        foreach (Transform child in gameObject.transform)
            count += CountMissingScripts(child.gameObject);
        return count;
    }

    static void VerifyAutomaticDoors(IEnumerable<string> expectedPairs)
    {
        string[] actual = UnityEngine.Object.FindObjectsByType<DoorTransition>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Select(door => door.TargetSceneName + "/" + door.TargetSpawnId)
            .OrderBy(value => value)
            .ToArray();
        string[] expected = expectedPairs.OrderBy(value => value).ToArray();
        Require(actual.SequenceEqual(expected),
            "Automatic door destinations changed. Expected " + string.Join(", ", expected) +
            "; got " + string.Join(", ", actual));
        Require(UnityEngine.Object.FindObjectsByType<DoorTransition>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None).All(door => string.IsNullOrEmpty(door.PromptText)),
            "game_v2 doors must remain automatic and must not acquire an E prompt.");
    }

    static NpcCharacter FindNpc(string displayName)
    {
        NpcCharacter npc = UnityEngine.Object.FindObjectsByType<NpcCharacter>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None).FirstOrDefault(candidate => candidate.DisplayName == displayName);
        Require(npc != null, "Could not find existing NPC: " + displayName);
        return npc;
    }

    static void RequireCount<T>(int expected, string label) where T : UnityEngine.Object
    {
        int actual = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None).Length;
        Require(actual == expected, label + ": expected " + expected + ", got " + actual + ".");
    }

    static void RequirePosition(Vector3 actual, Vector3 expected, string label)
    {
        Require(Vector3.Distance(actual, expected) <= PositionTolerance,
            label + " position changed. Expected " + expected + ", got " + actual + ".");
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("[IntroOutro game_v2] " + message);
    }
}
