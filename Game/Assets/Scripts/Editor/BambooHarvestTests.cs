using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BambooHarvestTests
{
    const string EvidencePath = "Logs/BambooHarvest";

    [Test]
    public void WorldSceneHasExactlySixHarvestableStalks()
    {
        EditorSceneManager.OpenScene(SceneTransitionState.WorldScenePath);
        GameObject forest = GameObject.Find("BambooForest");
        Assert.That(forest, Is.Not.Null);
        Assert.That(forest.transform.childCount, Is.EqualTo(90));
        Assert.That(forest.GetComponent<BambooHarvest>(), Is.Not.Null);
        ChopableBamboo[] stalks = forest.GetComponentsInChildren<ChopableBamboo>();
        Assert.That(stalks.Length, Is.EqualTo(6));
        Assert.That(stalks.All(b => b.enabled && b.ChopsToFell == 8), Is.True);
        Assert.That(BambooHarvest.EnsureHarvestStalks(forest.transform, 6), Is.EqualTo(6));
        Assert.That(forest.GetComponentsInChildren<ChopableBamboo>().Length, Is.EqualTo(6));
    }

    [UnityTest]
    public IEnumerator SpaceInputHarvestsSixStalksAndSurvivesSceneTransitions()
    {
        // Start inside the house: this catches initialization that only runs at game startup.
        EditorSceneManager.OpenScene(SceneTransitionState.HomeScenePath);
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        var previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        var previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        SceneTransitionState.FailLoad();
        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorldSceneName);
        yield return null;
        yield return null;

        var keyboard = InputSystem.AddDevice<Keyboard>("BambooTestKeyboard");
        keyboard.MakeCurrent();
        var harvest = Object.FindFirstObjectByType<BambooHarvest>();
        var player = Object.FindFirstObjectByType<PlayerController>();
        Assert.That(harvest, Is.Not.Null, "Indoor-to-world entry must initialize bamboo interaction.");
        var stalks = harvest.GetComponentsInChildren<ChopableBamboo>()
            .OrderBy(b => Vector3.Distance(b.transform.position, BambooHarvest.PathAnchor)).ToArray();
        Assert.That(stalks.Length, Is.EqualTo(6));
        Assert.That(GreenBambooBag.Count, Is.Zero);

        MovePlayer(player, Vector3.zero);
        yield return null;
        Assert.That(InteractionPromptUI.IsVisible, Is.False);
        yield return PressSpace(keyboard);
        Assert.That(stalks.Sum(b => b.Chops), Is.Zero, "Space outside interaction range must do nothing.");

        ChopableBamboo first = stalks[0];
        Vector3 firstPosition = first.transform.position;
        MovePlayer(player, firstPosition - Vector3.forward * 1.5f);
        yield return null;
        yield return null;
        Assert.That(harvest.CurrentTarget, Is.SameAs(first));
        Assert.That(InteractionPromptUI.CurrentMessage, Is.EqualTo("按空格键砍伐"));
        Capture("01-approach.png");

        yield return SetKeyboardState(keyboard, new KeyboardState(Key.Space));
        Assert.That(first.Chops, Is.EqualTo(1));
        Assert.That(first.GetComponent<AudioSource>().isPlaying, Is.True, "Each press must trigger audio.");
        yield return WaitGameSeconds(0.4f);
        Assert.That(first.Chops, Is.EqualTo(1), "Holding Space must not repeat chops.");
        yield return SetKeyboardState(keyboard, new KeyboardState());

        // Overlapping interaction ranges: keep the started stalk, then release it on leaving range.
        ChopableBamboo other = stalks[1];
        Vector3 otherPosition = other.transform.position;
        other.transform.position = firstPosition + Vector3.right * 2.6f;
        MovePlayer(player, firstPosition + Vector3.right * 1.8f);
        yield return null;
        Assert.That(harvest.CurrentTarget, Is.SameAs(first));
        yield return PressSpace(keyboard);
        Assert.That(first.Chops, Is.EqualTo(2));
        Assert.That(other.Chops, Is.Zero);
        MovePlayer(player, firstPosition + Vector3.right * 4f);
        yield return null;
        Assert.That(harvest.CurrentTarget, Is.SameAs(other));
        other.transform.position = otherPosition;
        MovePlayer(player, firstPosition - Vector3.forward * 1.5f);
        yield return null;

        // Loading and pause must suppress both prompts and harvest input.
        SceneTransitionState.IsLoading = true;
        yield return PressSpace(keyboard);
        Assert.That(first.Chops, Is.EqualTo(2));
        Assert.That(InteractionPromptUI.IsVisible, Is.False);
        SceneTransitionState.IsLoading = false;
        Time.timeScale = 0f;
        yield return PressSpace(keyboard);
        Assert.That(first.Chops, Is.EqualTo(2));
        Time.timeScale = 1f;

        for (int count = 3; count <= 7; count++)
        {
            yield return PressSpace(keyboard);
            Assert.That(first.Chops, Is.EqualTo(count), "Every new press must count, including rapid presses.");
            Assert.That(first.CurrentStage, Is.EqualTo(ChopableBamboo.Stage.Standing));
        }
        Assert.That(InteractionPromptUI.CurrentMessage, Is.EqualTo("按空格键砍伐（7/8）"));
        Capture("02-seven-chops.png");
        yield return PressSpace(keyboard);
        Assert.That(first.Chops, Is.EqualTo(8));
        Assert.That(first.CurrentStage, Is.EqualTo(ChopableBamboo.Stage.Falling));
        Assert.That(first.TryPickup(player.transform.position), Is.False, "Cannot pick up before the fall finishes.");
        Assert.That(first.TryChop(player.transform.position), Is.False);
        yield return WaitGameSeconds(0.25f);
        Assert.That(Vector3.Dot(first.transform.Find("FallingStem").up, Vector3.forward), Is.GreaterThan(0f),
            "The stem must fall away from the player, toward its drop.");
        Capture("03-falling.png");
        yield return WaitGameSeconds(0.65f);
        Assert.That(first.CurrentStage, Is.EqualTo(ChopableBamboo.Stage.Dropped));
        Assert.That(first.transform.Find("FallingStem").gameObject.activeSelf, Is.False);
        Assert.That(first.transform.Find("BambooStump"), Is.Not.Null);
        Assert.That(GreenBambooBag.Count, Is.Zero, "Felling must not automatically grant the item.");
        Assert.That(first.TryPickup(first.InteractPoint + Vector3.right * 10f), Is.False);
        MovePlayer(player, first.InteractPoint - Vector3.forward * 0.8f);
        yield return null;
        Assert.That(InteractionPromptUI.CurrentMessage, Is.EqualTo("按空格键拾取完整青竹"));
        Capture("04-drop.png");
        yield return PressSpace(keyboard);
        Assert.That(GreenBambooBag.Count, Is.EqualTo(1));
        Assert.That(first.CurrentStage, Is.EqualTo(ChopableBamboo.Stage.Collected));
        Assert.That(first.TryPickup(player.transform.position), Is.False, "No duplicate rewards.");
        Assert.That(InteractionPromptUI.CurrentMessage, Is.EqualTo("获得1根完整青竹"));
        Capture("05-collected.png");

        // Exercise all six actual scene instances, with their original positions/colliders.
        for (int i = 1; i < stalks.Length; i++)
        {
            ChopableBamboo bamboo = stalks[i];
            MovePlayer(player, bamboo.transform.position - Vector3.forward * 1.5f);
            yield return null;
            for (int chop = 0; chop < 8; chop++)
                yield return PressSpace(keyboard);
            Assert.That(bamboo.Chops, Is.EqualTo(8), bamboo.name);
            yield return WaitGameSeconds(0.8f);
            MovePlayer(player, bamboo.InteractPoint - Vector3.forward * 0.8f);
            yield return null;
            yield return PressSpace(keyboard);
            Assert.That(GreenBambooBag.Count, Is.EqualTo(i + 1), bamboo.name);
        }
        Assert.That(stalks.All(b => b.CurrentStage == ChopableBamboo.Stage.Collected), Is.True);
        Assert.That(harvest.GetComponentsInChildren<ChopableBamboo>().Length, Is.EqualTo(6));
        yield return WaitGameSeconds(1.4f);
        Assert.That(InteractionPromptUI.IsVisible, Is.False);

        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorkshopSceneName);
        yield return null;
        Assert.That(GreenBambooBag.Count, Is.EqualTo(6));
        Assert.That(GameObject.Find("GreenBambooBagHud"), Is.Not.Null);
        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorldSceneName);
        yield return null;
        harvest = Object.FindFirstObjectByType<BambooHarvest>();
        Assert.That(harvest, Is.Not.Null);
        Assert.That(harvest.GetComponentsInChildren<ChopableBamboo>().Length, Is.EqualTo(6));
        Assert.That(GreenBambooBag.Count, Is.EqualTo(6));
        player = Object.FindFirstObjectByType<PlayerController>();
        MovePlayer(player, harvest.GetComponentsInChildren<ChopableBamboo>()[0].transform.position - Vector3.forward * 1.5f);
        yield return null;
        Assert.That(InteractionPromptUI.CurrentMessage, Is.EqualTo("按空格键砍伐"));

        InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        yield return new ExitPlayMode();
        EditorSceneManager.OpenScene(SceneTransitionState.WorldScenePath);
    }

    static IEnumerator PressSpace(Keyboard keyboard)
    {
        yield return SetKeyboardState(keyboard, new KeyboardState(Key.Space));
        yield return SetKeyboardState(keyboard, new KeyboardState());
    }

    static IEnumerator WaitGameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        double deadline = EditorApplication.timeSinceStartup + 15;
        while (Time.time < end)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Game time must keep advancing.");
            yield return null;
        }
    }

    static IEnumerator SetKeyboardState(Keyboard keyboard, KeyboardState state)
    {
        // EditMode coroutines also run on editor ticks. Queue into the actual game input update,
        // otherwise an editor input update can consume the event before MonoBehaviour.Update.
        bool queued = false;
        int frame = -1;
        System.Action beforeUpdate = () =>
        {
            if (queued || InputState.currentUpdateType != InputUpdateType.Dynamic)
                return;
            InputSystem.QueueStateEvent(keyboard, state);
            queued = true;
            frame = Time.frameCount;
        };
        InputSystem.onBeforeUpdate += beforeUpdate;
        double deadline = EditorApplication.timeSinceStartup + 10;
        try
        {
            while (!queued || Time.frameCount <= frame)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "The game must advance real frames.");
                yield return null;
            }
        }
        finally
        {
            InputSystem.onBeforeUpdate -= beforeUpdate;
        }
    }

    static void MovePlayer(PlayerController player, Vector3 position)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position + Vector3.up * 0.1f;
        controller.enabled = true;
        Physics.SyncTransforms();
        Camera.main.GetComponent<CameraFollow>().SnapToTarget();
    }

    static void Capture(string filename)
    {
        Directory.CreateDirectory(EvidencePath);
        ScreenCapture.CaptureScreenshot(Path.Combine(EvidencePath, filename));
    }
}
