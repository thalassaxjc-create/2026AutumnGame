using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

[InitializeOnLoad]
internal static class OverworldPlayModeVerification
{
    private const string StateKey = "OverworldSmoke.State";
    private const string LogKey = "OverworldSmoke.Log";
    private const string ResultKey = "OverworldSmoke.Result";

    private static PlayerController player;
    private static LevelEntryTrigger trigger;
    private static Light sun;
    private static float phaseStarted;
    private static float testStarted;
    private static float movementStartZ;
    private static float initialLightIntensity;
    private static int stage;

    static OverworldPlayModeVerification()
    {
        string state = SessionState.GetString(StateKey, "Idle");
        if (state == "Waiting")
        {
            EditorApplication.update += EnterPlayModeWhenReady;
        }
        else if (state == "Entering")
        {
            EditorApplication.playModeStateChanged += OnEnteredPlayMode;
            if (EditorApplication.isPlaying)
            {
                StartRuntimeVerification();
            }
        }
        else if (state == "Exiting")
        {
            EditorApplication.playModeStateChanged += OnExitedPlayMode;
        }
    }

    public static void RunFromCommandLine()
    {
        try
        {
            SessionState.SetString(LogKey, string.Empty);
            SessionState.SetBool(ResultKey, false);
            EditorSceneManager.OpenScene(OverworldSceneGenerator.ScenePath, OpenSceneMode.Single);
            SessionState.SetString(StateKey, "Waiting");
            EditorApplication.update += EnterPlayModeWhenReady;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void EnterPlayModeWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        EditorApplication.update -= EnterPlayModeWhenReady;
        SessionState.SetString(StateKey, "Entering");
        EditorApplication.playModeStateChanged += OnEnteredPlayMode;
        EditorApplication.EnterPlaymode();
    }

    private static void OnEnteredPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= OnEnteredPlayMode;
        StartRuntimeVerification();
    }

    private static void StartRuntimeVerification()
    {
        SessionState.SetString(StateKey, "Running");
        Application.runInBackground = true;
        stage = 0;
        testStarted = Time.realtimeSinceStartup;
        phaseStarted = testStarted;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        try
        {
            if (Time.realtimeSinceStartup - testStarted > 12f)
            {
                throw new TimeoutException("Play Mode smoke test exceeded 12 seconds at stage " + stage + ".");
            }

            if (stage == 0 && Time.realtimeSinceStartup - phaseStarted > 0.35f)
            {
                player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                trigger = UnityEngine.Object.FindFirstObjectByType<LevelEntryTrigger>();
                sun = GameObject.Find("Primary Directional Light")?.GetComponent<Light>();
                OverworldCameraController cameraController = UnityEngine.Object.FindFirstObjectByType<OverworldCameraController>();

                Check(player != null, "Existing PlayerController is active");
                Check(player.GetComponent<Rigidbody>() != null && player.GetComponent<Collider>() != null, "Player uses 3D Rigidbody and Collider");
                Check(trigger != null && trigger.GetComponent<BoxCollider>().isTrigger, "LevelEntryTrigger uses a 3D trigger volume");
                Check(sun != null, "Primary directional light is assigned");
                Check(Camera.main != null && cameraController != null, "Overworld follow camera is active");
                Check(UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length > 40, "Whitebox static collision geometry is present");

                PlayerInput input = player.GetComponent<PlayerInput>();
                Check(input != null && input.actions.FindActionMap("Gameplay", false) != null, "Existing Gameplay input map is assigned");
                Check(input.actions.FindAction("Gameplay/Move", false) != null && input.actions.FindAction("Gameplay/Jump", false) != null, "Existing Move and Jump actions are available");
                movementStartZ = player.Position.z;
                SetMoveInput(Vector2.up);
                phaseStarted = Time.realtimeSinceStartup;
                stage = 1;
            }
            else if (stage == 1 && Time.realtimeSinceStartup - phaseStarted > 0.55f)
            {
                SetMoveInput(Vector2.zero);
                Check(player.Position.z > movementStartZ + 1f, "Existing PlayerController advances its Rigidbody on Z");
                Check(player.IsGrounded, "Player remains grounded on the generated walkable path");

                initialLightIntensity = sun.intensity;
                player.Respawn(new Vector3(9f, 0.6f, 11.55f));
                phaseStarted = Time.realtimeSinceStartup;
                stage = 2;
            }
            else if (stage == 2 && Time.realtimeSinceStartup - phaseStarted > 0.55f)
            {
                Check(trigger.HasTriggered, "Entering with the player starts the transition");
                Check(sun.intensity < initialLightIntensity && sun.intensity > 0.22f, "Directional light is smoothly transitioning");
                stage = 3;
            }
            else if (stage == 3 && Time.realtimeSinceStartup - phaseStarted > 2.15f)
            {
                Check(Mathf.Abs(sun.intensity - 0.22f) < 0.025f, "Directional light reaches the safe non-black target");
                Check(RenderSettings.ambientLight.maxColorComponent > 0.1f, "Ambient light remains above black");

                float completedIntensity = sun.intensity;
                player.Respawn(new Vector3(9f, 0.6f, 7.5f));
                player.Respawn(new Vector3(9f, 0.6f, 11.55f));
                Check(trigger.HasTriggered && Mathf.Abs(sun.intensity - completedIntensity) < 0.001f, "Transition does not restart on re-entry");
                Complete(true, string.Empty);
            }
        }
        catch (Exception exception)
        {
            Complete(false, exception.ToString());
        }
    }

    private static void SetMoveInput(Vector2 value)
    {
        FieldInfo field = typeof(PlayerController).GetField("moveInput", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new MissingFieldException(typeof(PlayerController).FullName, "moveInput");
        }

        field.SetValue(player, value);
    }

    private static void Check(bool condition, string description)
    {
        AppendLog(description + ": " + condition);
        if (!condition)
        {
            throw new InvalidOperationException(description);
        }
    }

    private static void AppendLog(string line)
    {
        string log = SessionState.GetString(LogKey, string.Empty);
        SessionState.SetString(LogKey, log + line + "\n");
        Debug.Log("[Overworld Smoke] " + line);
    }

    private static void Complete(bool success, string error)
    {
        EditorApplication.update -= Tick;
        if (!string.IsNullOrEmpty(error))
        {
            AppendLog(error);
            Debug.LogError(error);
        }

        SessionState.SetBool(ResultKey, success);
        SessionState.SetString(StateKey, "Exiting");
        EditorApplication.playModeStateChanged += OnExitedPlayMode;
        EditorApplication.ExitPlaymode();
    }

    private static void OnExitedPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= OnExitedPlayMode;
        bool success = SessionState.GetBool(ResultKey, false);
        string log = SessionState.GetString(LogKey, string.Empty);
        SessionState.SetString(StateKey, "Idle");
        Debug.Log("Overworld Play Mode smoke test " + (success ? "PASSED" : "FAILED") + "\n" + log);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
