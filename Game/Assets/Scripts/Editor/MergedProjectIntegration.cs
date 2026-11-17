#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Installs the xiao minigames into the feature branch workshop and verifies that
/// both the minigames and the feature branch computer interaction survived the merge.
/// </summary>
public static class MergedProjectIntegration
{
    const string WorkshopScenePath = "Assets/Scenes/WorkshopScene.unity";

    public static void InstallAndVerify()
    {
        Scene scene = EditorSceneManager.OpenScene(WorkshopScenePath, OpenSceneMode.Single);
        Require(scene.IsValid() && scene.isLoaded, "Could not open WorkshopScene.");

        WeavingMinigameSetup.InstallOrUpdate();
        LacquerMinigameSetup.InstallOrUpdate();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        VerifyStation<WeavingMinigameController, WeavingStationTrigger>(
            "WeavingInteractionPoint", "WeavingMinigameCanvas");
        VerifyStation<LacquerMinigameController, LacquerStationTrigger>(
            "LacquerInteractionPoint", "LacquerMinigameCanvas");

        AudioClip weavingMusic = Resources.Load<AudioClip>("Audio/Weaving_11_15");
        Require(weavingMusic != null, "Weaving music could not be loaded from Resources.");
        Require(weavingMusic.length > 66.4f && weavingMusic.length < 66.7f,
            "Weaving music duration is unexpected: " + weavingMusic.length + " seconds.");
        Require(WeavingMinigameController.MusicChartNoteCount == 100,
            "Weaving music chart must contain exactly 100 notes.");
        Require(WeavingMinigameController.MusicChartEndTime < weavingMusic.length - .5f,
            "Weaving music chart must finish before the shortened music ends.");

        var lacquerController = FindDeep("LacquerInteractionPoint").GetComponent<LacquerMinigameController>();
        Require(LacquerMinigameController.MarksPerScanPass == 6,
            "Lacquer difficulty must use six marks per scan pass.");
        Require(Mathf.Approximately(lacquerController.ScanPassDuration, 5.6f),
            "Lacquer scan pass duration must use the balanced 5.6 second preset.");
        Require(Mathf.Approximately(lacquerController.PreShowTime, 1.05f),
            "Lacquer mark lead time must use the balanced 1.05 second preset.");

        GameObject computerPoint = FindDeep("ComputerInteractionPoint");
        Require(computerPoint != null, "ComputerInteractionPoint was lost during the merge.");
        WorkshopWhiteboxBuilder.EnsureWorkshopComputer();
        Require(computerPoint.GetComponent<WorkshopComputer>() != null,
            "WorkshopComputer was lost during the merge.");
        var computer = computerPoint.GetComponent<WorkshopComputer>();
        var computerSerialized = new SerializedObject(computer);
        Require(computerSerialized.FindProperty("interfacePrefab").objectReferenceValue != null,
            "WorkshopComputer is missing the ComputerUI prefab.");
        Require(computerSerialized.FindProperty("player").objectReferenceValue != null,
            "WorkshopComputer is missing the player reference.");

        int missingScripts = scene.GetRootGameObjects().Sum(CountMissingScripts);
        Require(missingScripts == 0, "WorkshopScene contains " + missingScripts + " missing script(s).");
        Require(!scene.isDirty, "WorkshopScene still has unsaved changes after installation.");

        Debug.Log("[Merged Project] Weaving, lacquer, computer UI, and WorkshopScene verification passed.");
    }

    static void VerifyStation<TController, TTrigger>(string stationName, string canvasName)
        where TController : Component
        where TTrigger : Component
    {
        GameObject station = FindDeep(stationName);
        Require(station != null, stationName + " is missing.");

        BoxCollider trigger = station.GetComponent<BoxCollider>();
        Require(trigger != null && trigger.isTrigger, stationName + " needs a trigger BoxCollider.");

        Rigidbody body = station.GetComponent<Rigidbody>();
        Require(body != null && body.isKinematic && !body.useGravity,
            stationName + " needs a kinematic Rigidbody without gravity.");
        Require(station.GetComponent<TController>() != null,
            stationName + " is missing " + typeof(TController).Name + ".");
        Require(station.GetComponent<TTrigger>() != null,
            stationName + " is missing " + typeof(TTrigger).Name + ".");

        GameObject canvasObject = FindDeep(canvasName);
        Require(canvasObject != null, canvasName + " is missing.");
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        Require(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay,
            canvasName + " is not a screen-space overlay Canvas.");
        Require(canvasObject.GetComponent<CanvasScaler>() != null, canvasName + " is missing CanvasScaler.");
        Require(canvasObject.GetComponent<GraphicRaycaster>() != null,
            canvasName + " is missing GraphicRaycaster.");
        Require(!canvasObject.activeSelf, canvasName + " must start hidden.");
    }

    static GameObject FindDeep(string objectName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);
            if (match != null)
                return match.gameObject;
        }

        return null;
    }

    static int CountMissingScripts(GameObject gameObject)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
        foreach (Transform child in gameObject.transform)
            count += CountMissingScripts(child.gameObject);
        return count;
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("[Merged Project] " + message);
    }
}
#endif
