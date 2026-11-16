using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>挂在 BambooAreaMat 上：区域内 E 进入、空格放料、鼠标加工、Esc 返回。</summary>
[DisallowMultipleComponent]
public class BambooWorkbench : MonoBehaviour
{
    [SerializeField] Transform workTable;
    [SerializeField] GameObject[] storedCanes = System.Array.Empty<GameObject>();
    PlayerController player;
    Camera cameraView;
    CameraFollow follow;
    BambooWorkbenchView view;
    bool playerEnabled, followEnabled, wasInArea, held;
    bool wasOrthographic;
    float oldFov, oldOrthographicSize;
    Vector3 cameraPosition, lastPoint;
    Quaternion cameraRotation;
    CursorLockMode oldCursorLock;
    bool oldCursorVisible;
    float lastSampleTime;
    public bool IsOpen { get; private set; }
    public BambooCraftProgress Progress => BambooCraftSession.Current;
    public BambooWorkbenchView View => view;

    public void Configure(Transform table, GameObject[] canes)
    {
        workTable = table; storedCanes = canes;
    }

    void OnEnable()
    {
        GreenBambooBag.Changed += UpdateStoredCanes;
        UpdateStoredCanes();
    }

    void OnDisable()
    {
        Close();
        GreenBambooBag.Changed -= UpdateStoredCanes;
        if (wasInArea) InteractionPromptUI.Hide();
        wasInArea = false;
    }

    void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }

    void UpdateStoredCanes()
    {
        for (int i = 0; i < storedCanes.Length; i++)
            if (storedCanes[i] != null) storedCanes[i].SetActive(i < GreenBambooBag.Count);
    }

    public bool Contains(Vector3 worldPosition)
    {
        Vector3 p = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(p.x) <= 0.5f && Mathf.Abs(p.z) <= 0.5f && Mathf.Abs(worldPosition.y - transform.position.y) < 3f;
    }

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (SceneTransitionState.IsLoading)
        {
            Close();
            return;
        }
        if (Time.timeScale <= 0)
        {
            CancelPointer();
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (!IsOpen)
        {
            bool inArea = player != null && Contains(player.transform.position);
            if (inArea) InteractionPromptUI.Show("按 E 进入篾竹工作台");
            else if (wasInArea) InteractionPromptUI.Hide();
            wasInArea = inArea;
            if (inArea && keyboard != null && keyboard.eKey.wasPressedThisFrame) Open();
            return;
        }
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            bool changed = Progress.Advance();
            if (changed) held = false;
            view.Refresh();
        }
        if (Progress.IsFinishingStage)
        {
            held = false;
            float vertical = keyboard == null ? 0 : (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            bool pulling = Mouse.current != null && Mouse.current.rightButton.isPressed;
            Progress.TickFinishing(Time.deltaTime, vertical, pulling);
            view.FinishingInput(pulling);
            return;
        }
        Mouse mouse = Mouse.current;
        if (mouse == null) { CancelPointer(); return; }
        Vector2 screen = mouse.position.ReadValue();
        view.ScreenToSurface(screen, out Vector2 point);
        if (mouse.leftButton.isPressed && !held)
        {
            held = true;
            Progress.BeginStroke(point);
            lastPoint = point;
            lastSampleTime = Time.unscaledTime;
        }
        int removed = 0;
        float speed = 0;
        if (held && mouse.leftButton.isPressed)
        {
            Vector2 delta = point - (Vector2)lastPoint;
            float distance = new Vector2(delta.x, delta.y * 0.18f).magnitude;
            if (distance > 0.0001f)
            {
                float dt = Mathf.Max(0.001f, Mathf.Min(Time.unscaledDeltaTime, Time.unscaledTime - lastSampleTime));
                speed = distance / dt;
                removed = Progress.Drag(point, dt);
                lastSampleTime = Time.unscaledTime;
                lastPoint = point;
            }
        }
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            // Include the release position even if no held frame arrived at the endpoint.
            if (held) Progress.Drag(point, Mathf.Max(0.001f, Time.unscaledDeltaTime));
            if (Progress.EndStroke()) view.PlaySplit();
            held = false;
        }
        view.Pointer(screen, held && mouse.leftButton.isPressed, speed, removed);
    }

    public bool Open()
    {
        if (IsOpen || player == null || !Contains(player.transform.position) || SceneTransitionState.IsLoading) return false;
        cameraView = player.GameplayCamera != null ? player.GameplayCamera : Camera.main;
        if (cameraView == null) return false;
        if (view == null) view = BambooWorkbenchView.Create(Progress);
        else view.gameObject.SetActive(true);
        playerEnabled = player.enabled;
        player.enabled = false;
        follow = cameraView.GetComponent<CameraFollow>();
        followEnabled = follow != null && follow.enabled;
        if (follow != null) follow.enabled = false;
        cameraPosition = cameraView.transform.position;
        cameraRotation = cameraView.transform.rotation;
        oldFov = cameraView.fieldOfView;
        wasOrthographic = cameraView.orthographic;
        oldOrthographicSize = cameraView.orthographicSize;
        Vector3 focus = workTable != null ? workTable.position + Vector3.up * 0.9f : transform.position + Vector3.up;
        cameraView.transform.position = focus + new Vector3(0, 3.1f, -1.3f);
        cameraView.transform.LookAt(focus);
        cameraView.orthographic = false;
        cameraView.fieldOfView = 48;
        oldCursorLock = Cursor.lockState; oldCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        IsOpen = true;
        InteractionPromptUI.Hide();
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        CancelPointer();
        if (view != null) view.gameObject.SetActive(false);
        if (player != null) player.enabled = playerEnabled;
        if (cameraView != null)
        {
            cameraView.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            cameraView.fieldOfView = oldFov;
            cameraView.orthographic = wasOrthographic;
            cameraView.orthographicSize = oldOrthographicSize;
        }
        if (follow != null) follow.enabled = followEnabled;
        Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible;
        IsOpen = false;
    }

    void CancelPointer()
    {
        held = false;
        Progress.CancelStroke();
        if (view != null && view.gameObject.activeSelf) view.Pointer(Vector2.zero, false, 0, 0);
    }

    void OnApplicationFocus(bool focused) { if (!focused) CancelPointer(); }
}
