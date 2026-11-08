using UnityEngine;
public sealed class BeatClock : MonoBehaviour
{
    [SerializeField] private float bpm = 120f;
    private float elapsed;
    private int lastBeat = -1;
    public bool Paused { get; private set; } = true;
    public float Elapsed => elapsed;
    public float BeatDuration => 60f / bpm;
    public int BeatIndex => Mathf.FloorToInt(elapsed / BeatDuration);
    public event System.Action<int> Beat;
    public void SetPaused(bool value)
    {
        if (Paused == value) return;
        Paused = value;
        if (!value) { elapsed = (Mathf.Floor(elapsed / BeatDuration) + 1f) * BeatDuration; lastBeat = BeatIndex - 1; }
    }
    public void ResetClock() { elapsed = 0f; lastBeat = -1; Paused = true; }
    private void Update()
    {
        if (Paused) return;
        elapsed += Time.deltaTime;
        int current = BeatIndex;
        if (current != lastBeat) { lastBeat = current; Beat?.Invoke(current); }
    }
}
