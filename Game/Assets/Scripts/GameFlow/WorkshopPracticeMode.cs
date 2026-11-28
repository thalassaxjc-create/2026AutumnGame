using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>An isolated Editor play session for exercising workstations with practice inputs.</summary>
public static class WorkshopPracticeMode
{
#if UNITY_EDITOR
    public const string PreferenceKey = "AutumnGame.WorkshopPractice";
    public static bool Enabled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void StartSession()
    {
        Enabled = UnityEditor.SessionState.GetBool(PreferenceKey, false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OpenWorkshop()
    {
        if (Enabled && SceneManager.GetActiveScene().name != SceneTransitionState.WorkshopSceneName)
            SceneManager.LoadSceneAsync(SceneTransitionState.WorkshopSceneName);
    }
#else
    public static bool Enabled => false;
#endif
}
