using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

static class WorkshopPracticeMenu
{
    const string MenuPath = "宣纸帘/工位试玩/";
    const string SwitchPath = MenuPath + "启用无限试玩";

    [MenuItem(SwitchPath, false, 0)]
    static void TogglePractice() => SessionState.SetBool(WorkshopPracticeMode.PreferenceKey,
        !SessionState.GetBool(WorkshopPracticeMode.PreferenceKey, false));

    [MenuItem(SwitchPath, true)]
    static bool ValidateToggle()
    {
        Menu.SetChecked(SwitchPath, SessionState.GetBool(WorkshopPracticeMode.PreferenceKey, false));
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(MenuPath + "劈竹", false, 20)]
    static void Split() => OpenBamboo(BambooCraftProgress.Step.Splitting);
    [MenuItem(MenuPath + "剐竹青", false, 21)]
    static void Scrape() => OpenBamboo(BambooCraftProgress.Step.Scraping);
    [MenuItem(MenuPath + "过剑门", false, 22)]
    static void Gate() => OpenBamboo(BambooCraftProgress.Step.GatePassing);
    [MenuItem(MenuPath + "抽竹丝", false, 23)]
    static void Draw() => OpenBamboo(BambooCraftProgress.Step.Drawing);
    [MenuItem(MenuPath + "编帘", false, 24)]
    static void Weave() => OpenWeaving(1);
    [MenuItem(MenuPath + "编帘 · 第二天", false, 25)]
    static void WeaveDayTwo() => OpenWeaving(2);
    [MenuItem(MenuPath + "编帘 · 第三天", false, 26)]
    static void WeaveDayThree() => OpenWeaving(3);
    [MenuItem(MenuPath + "刷大漆", false, 27)]
    static void Lacquer() => OpenLacquer(1);
    [MenuItem(MenuPath + "刷大漆 · 第二天", false, 28)]
    static void LacquerDayTwo() => OpenLacquer(2);
    [MenuItem(MenuPath + "刷大漆 · 第三天", false, 29)]
    static void LacquerDayThree() => OpenLacquer(3);
    [MenuItem(MenuPath + "纹样", false, 30)]
    static void Pattern() => Open(() => Object.FindFirstObjectByType<PatternStationTrigger>()?.OpenForPractice());
    [MenuItem(MenuPath + "上漆边", false, 31)]
    static void Trim() => Open(() => Object.FindFirstObjectByType<VermilionTrimStation>()?.OpenForPractice());

    [MenuItem(MenuPath + "劈竹", true)]
    [MenuItem(MenuPath + "剐竹青", true)]
    [MenuItem(MenuPath + "过剑门", true)]
    [MenuItem(MenuPath + "抽竹丝", true)]
    [MenuItem(MenuPath + "编帘", true)]
    [MenuItem(MenuPath + "编帘 · 第二天", true)]
    [MenuItem(MenuPath + "编帘 · 第三天", true)]
    [MenuItem(MenuPath + "刷大漆", true)]
    [MenuItem(MenuPath + "刷大漆 · 第二天", true)]
    [MenuItem(MenuPath + "刷大漆 · 第三天", true)]
    [MenuItem(MenuPath + "纹样", true)]
    [MenuItem(MenuPath + "上漆边", true)]
    static bool CanOpen() => EditorApplication.isPlaying && !EditorApplication.isPaused &&
        WorkshopPracticeMode.Enabled && SceneManager.GetActiveScene().name == SceneTransitionState.WorkshopSceneName &&
        !SceneTransitionState.IsLocked && !GameFlowModalState.IsInteractionBlocked;

    static void OpenBamboo(BambooCraftProgress.Step step) =>
        Open(() => Object.FindFirstObjectByType<BambooWorkbench>()?.OpenForPractice(step));

    static void OpenWeaving(int day) =>
        Open(() => Object.FindFirstObjectByType<WeavingStationTrigger>()?.OpenForPractice(day));

    static void OpenLacquer(int day) =>
        Open(() => Object.FindFirstObjectByType<LacquerStationTrigger>()?.OpenForPractice(day));

    static void Open(Action launch)
    {
        if (!CanOpen())
            return;
        launch();
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
    }
}
