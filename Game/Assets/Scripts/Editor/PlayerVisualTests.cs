using System.Collections;
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

public sealed class PlayerVisualTests
{
    [TestCase(SceneTransitionState.WorkshopScenePath)]
    [TestCase(SceneTransitionState.HomeScenePath)]
    [TestCase(SceneTransitionState.WorldScenePath)]
    public void SceneKeepsPlayerControlsAndUsesAlignedSharedVisual(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var players = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).ToArray();
            Assert.That(players.Length, Is.EqualTo(1));
            PlayerController player = players[0];
            CharacterController controller = player.GetComponent<CharacterController>();
            Assert.That(controller.enabled && player.enabled, Is.True);
            Assert.That(player.GameplayCamera, Is.Not.Null);
            Assert.That(player.CompareTag("Player"), Is.True);
            Assert.That(player.GetComponent<MeshRenderer>().enabled, Is.False);
            Transform visual = player.transform.Find("ProtagonistVisual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.gameObject), Is.EqualTo(PlayerVisualSetup.PrefabPath));
            var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skins.Length, Is.EqualTo(1));
            Assert.That(skins[0].sharedMesh, Is.Not.Null);
            Assert.That(skins[0].bones.All(b => b != null), Is.True);
            Assert.That(skins[0].sharedMaterials.All(m => m != null && m.shader.isSupported), Is.True);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, "人物外观不能增加阻碍移动的碰撞体。");
            Assert.That(visual.GetComponentsInChildren<Animator>(true).All(a => !a.applyRootMotion && a.runtimeAnimatorController == null), Is.True);
            Bounds bounds = PlayerVisualSetup.MeshBounds(visual.gameObject);
            Assert.That(bounds.size.y, Is.EqualTo(controller.height).Within(.02f));
            Assert.That(bounds.min.y, Is.EqualTo(controller.center.y - controller.height * .5f).Within(.02f));
            Assert.That(((ModelImporter)AssetImporter.GetAtPath(PlayerVisualSetup.ModelPath)).isReadable, Is.False);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator ImportedCharacterMovesWithExistingKeyboardControls()
    {
        EditorSceneManager.OpenScene(SceneTransitionState.WorkshopScenePath);
        yield return new EnterPlayMode();
        var oldBackground = InputSystem.settings.backgroundBehavior;
        var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        bool oldRunInBackground = Application.runInBackground;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var visual = player.transform.Find("ProtagonistVisual");
            Assert.That(visual, Is.Not.Null);
            Vector3 before = player.transform.position;
            yield return InputForSeconds(keyboard, new KeyboardState(Key.W), .25f);
            yield return InputForSeconds(keyboard, new KeyboardState(), .05f);
            Vector3 moved = player.transform.position - before;
            moved.y = 0;
            Assert.That(moved.magnitude, Is.GreaterThan(.2f), "导入外观后仍须通过真实 W 键移动。");
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(visual.GetComponentInChildren<SkinnedMeshRenderer>().enabled, Is.True);
            ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/PlayerVisual/protagonist-in-workshop.png");
            yield return InputForSeconds(keyboard, new KeyboardState(), .1f);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            Application.runInBackground = oldRunInBackground;
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    static IEnumerator InputForSeconds(Keyboard keyboard, KeyboardState state, float duration)
    {
        float start = -1;
        System.Action queue = () =>
        {
            if (start >= 0 || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            InputSystem.QueueStateEvent(keyboard, state);
            start = Time.time;
        };
        InputSystem.onBeforeUpdate += queue;
        double deadline = EditorApplication.timeSinceStartup + 15;
        try
        {
            while (start < 0 || Time.time - start < duration)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待人物移动游戏帧。");
                yield return null;
            }
        }
        finally { InputSystem.onBeforeUpdate -= queue; }
    }
}
