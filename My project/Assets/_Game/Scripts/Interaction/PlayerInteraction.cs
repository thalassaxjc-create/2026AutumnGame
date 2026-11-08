using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public sealed class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private ClimbableRope[] ropes = new ClimbableRope[0];
    private PlayerController player;
    public System.Func<Vector3, float> PullDistance { get; set; }
    public System.Func<bool> PullAction { get; set; }
    public string CurrentPrompt { get; private set; }
    private ClimbableRope nearest;
    private bool pullNearest;
    private void Awake() { player = GetComponent<PlayerController>(); }
    private void OnEnable() { player.InteractRequested += HandleInteract; }
    private void OnDisable() { player.InteractRequested -= HandleInteract; }
    public void Configure(ClimbableRope[] routes) { ropes = routes; }
    private void Update() { SelectTarget(); }
    private void SelectTarget()
    {
        CurrentPrompt = "";
        nearest = null;
        pullNearest = false;
        if (player.IsSwinging)
        {
            CurrentPrompt = "PLANK SWING: alternate W / S to move plank + rider    SPACE = leap    E = drop";
            return;
        }
        if (player.IsClimbing) { CurrentPrompt = "CLIMB: W / Up = ascend, S / Down = descend\nE = exit at a safe endpoint"; return; }
        if (player.MovementLocked) return;
        float best = float.PositiveInfinity;
        foreach (var rope in ropes)
        {
            if (rope == null || !rope.gameObject.activeInHierarchy) continue;
            float distance = rope.InteractionDistance(player.Position);
            if (distance < best) { best = distance; nearest = rope; }
        }
        float pull = PullDistance == null ? float.PositiveInfinity : PullDistance(player.Position);
        if (pull < best) { pullNearest = true; CurrentPrompt = "THROW [E] - face left and hold the plank, then SPACE inside green"; }
        else if (nearest != null)
            CurrentPrompt = nearest.SwingAvailable
                ? "GRAB ROPE [E] - plank + rider swing together; W / S pump, SPACE leap"
                : "CLIMB [E] - W / Up and S / Down";
    }
    public bool Interact()
    {
        SelectTarget();
        if (pullNearest) return PullAction != null && PullAction();
        return nearest != null && nearest.TryClimb(player);
    }
    private void HandleInteract() { Interact(); }
}
