using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Owns menu input and hands a new game to the existing story entry point.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] Button startButton;
    [SerializeField] Button loadButton;
    [SerializeField] Button quitButton;
    bool loading;

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        loadButton.interactable = false;
        startButton.onClick.AddListener(StartGame);
        quitButton.onClick.AddListener(QuitGame);
    }

    void OnDisable()
    {
        startButton.onClick.RemoveListener(StartGame);
        quitButton.onClick.RemoveListener(QuitGame);
    }

    public void StartGame()
    {
        if (loading)
            return;
        if (SceneUtility.GetBuildIndexByScenePath(SceneTransitionState.WorldScenePath) < 0)
        {
            Debug.LogError("正式起始场景 WorldScene 未加入构建场景列表。", this);
            return;
        }

        loading = true;
        startButton.interactable = quitButton.interactable = false;
        SceneManager.sceneLoaded += StartExistingStory;
        SceneManager.LoadSceneAsync(SceneTransitionState.WorldScenePath, LoadSceneMode.Single);
    }

    static void StartExistingStory(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= StartExistingStory;
        if (scene.path == SceneTransitionState.WorldScenePath && WorkshopOrder.Current == null)
            WorkshopOrderGuide.StartStory();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCallbacks() => SceneManager.sceneLoaded -= StartExistingStory;

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
