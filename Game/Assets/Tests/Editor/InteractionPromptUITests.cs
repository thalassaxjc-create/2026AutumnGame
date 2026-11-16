using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class InteractionPromptUITests
{
    readonly List<GameObject> objects = new List<GameObject>();
    Type promptType;
    FieldInfo instanceField;
    object previousInstance;
    Action<string> show;
    Action hide;
    Action hideImmediate;
    Action<Text> ensureFont;

    [SetUp]
    public void SetUp()
    {
        // Test assemblies cannot reference the predefined Assembly-CSharp directly.
        promptType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("InteractionPromptUI"))
            .First(type => type != null);
        instanceField = promptType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        previousInstance = instanceField.GetValue(null);
        show = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), promptType.GetMethod("Show"));
        hide = (Action)Delegate.CreateDelegate(typeof(Action), promptType.GetMethod("Hide"));
        hideImmediate = (Action)Delegate.CreateDelegate(typeof(Action), promptType.GetMethod("HideImmediate"));
        ensureFont = (Action<Text>)Delegate.CreateDelegate(typeof(Action<Text>), promptType.GetMethod("EnsureChineseFont"));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in objects)
            if (go != null)
                Object.DestroyImmediate(go);
        objects.Clear();
        instanceField.SetValue(null, previousInstance);
    }

    [Test]
    public void RepeatedShowAndHideKeepsNativeFontCountStable()
    {
        Text label = CreatePrompt();
        show("按空格键砍伐");
        Font font = label.font;
        Assert.That(font, Is.Not.Null);
        int fontCount = FontCount();

        for (int i = 0; i < 10000; i++)
        {
            hide();
            hideImmediate();
            Assert.That(label.enabled, Is.False);
            show("按空格键砍伐");
            Assert.That(label.enabled, Is.True);
            Assert.That(label.font, Is.SameAs(font), "Updating a prompt must reuse its native font.");
        }

        Assert.That(label.text, Is.EqualTo("按空格键砍伐"));
        Assert.That(FontCount(), Is.EqualTo(fontCount));
        CanvasGroup group = label.GetComponentInParent<CanvasGroup>();
        Assert.That(group.alpha, Is.EqualTo(1f));
        Assert.That(group.blocksRaycasts, Is.False);
        Assert.That(group.interactable, Is.False);
        hide();
        Assert.That(group.alpha, Is.Zero);
        Debug.Log($"Prompt regression: 10000 show/hide cycles, native Fonts {fontCount} -> {FontCount()}.");
    }

    [Test]
    public void HidingDoesNotCreateOrAssignFonts()
    {
        Text label = CreatePrompt();
        label.font = null;
        int fontCount = FontCount();
        for (int i = 0; i < 10000; i++)
        {
            hide();
            hideImmediate();
        }

        Assert.That(label.font, Is.Null);
        Assert.That(FontCount(), Is.EqualTo(fontCount));
    }

    [Test]
    public void LabelsShareFontWithoutChangingTheirFontSize()
    {
        Text prompt = CreatePrompt();
        Text nameplate = CreateObject("Nameplate", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        prompt.fontSize = 34;
        nameplate.fontSize = 36;
        ensureFont(nameplate);
        Font font = prompt.font;
        int fontCount = FontCount();

        for (int i = 0; i < 1000; i++)
            ensureFont(nameplate);

        Assert.That(nameplate.font, Is.SameAs(font));
        Assert.That(prompt.fontSize, Is.EqualTo(34));
        Assert.That(nameplate.fontSize, Is.EqualTo(36));
        Assert.That(FontCount(), Is.EqualTo(fontCount));
    }

    [Test]
    public void RecreatingPromptReusesFont()
    {
        Text first = CreatePrompt();
        Font font = first.font;
        Object.DestroyImmediate(first.transform.parent.gameObject);
        int fontCount = FontCount();

        Text second = CreatePrompt();
        show("按空格键拾取");

        Assert.That(second.font, Is.SameAs(font));
        Assert.That(FontCount(), Is.EqualTo(fontCount));
    }

    [UnityTest]
    public IEnumerator SharedFontSurvivesUnusedAssetUnload()
    {
        Text first = CreatePrompt();
        Font font = first.font;
        Object.DestroyImmediate(first.transform.parent.gameObject);
        yield return Resources.UnloadUnusedAssets();

        Assert.That(font != null, Is.True);
        Text second = CreatePrompt();
        Assert.That(second.font, Is.SameAs(font));
    }

    [Test]
    public void MissingLabelAndMissingInstanceAreSafe()
    {
        GameObject root = CreateObject("MissingLabel", typeof(RectTransform), typeof(CanvasGroup));
        Component prompt = root.AddComponent(promptType);
        InitializePrompt(prompt);
        Assert.DoesNotThrow(() => show("No label yet"));
        Assert.DoesNotThrow(() => hide());
        instanceField.SetValue(null, null);
        Assert.DoesNotThrow(() => show("No instance"));
        Assert.DoesNotThrow(() => hideImmediate());
        Assert.DoesNotThrow(() => ensureFont(null));
    }

    Text CreatePrompt()
    {
        GameObject root = CreateObject("Prompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        Text label = CreateObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(root.transform, false);
        Component prompt = root.AddComponent(promptType);
        InitializePrompt(prompt);
        return label;
    }

    void InitializePrompt(Component prompt)
    {
        // EditMode does not run Awake; SendMessage would assert ShouldRunBehaviour in Unity 6.
        promptType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(prompt, null);
    }

    GameObject CreateObject(string name, params Type[] components)
    {
        var go = new GameObject(name, components);
        objects.Add(go);
        return go;
    }

    static int FontCount() => Resources.FindObjectsOfTypeAll<Font>().Length;
}
