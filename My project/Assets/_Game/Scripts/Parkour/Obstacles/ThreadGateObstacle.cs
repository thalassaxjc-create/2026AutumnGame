using UnityEngine;
public sealed class ThreadGateObstacle : ParkourObstacle
{
    [SerializeField] private Collider[] lanes = new Collider[0];
    [SerializeField] private Renderer[] visuals = new Renderer[0];
    [SerializeField] private Renderer[] indicators = new Renderer[0];
    private MaterialPropertyBlock propertyBlock;
    public int SafeLane { get; private set; } = 2;
    public bool WarningActive { get; private set; }
    public void SetGeometry(Collider[] colliders, Renderer[] renderers, Renderer[] cues)
    { lanes = colliders; visuals = renderers; indicators = cues; }
    private void Awake() { propertyBlock = new MaterialPropertyBlock(); }
    private void Update()
    {
        if (player == null || obstacle == null || clock == null) return;
        float distance = obstacle.z - player.Position.z;
        float eta = distance / Mathf.Max(.1f, player.AutoRunSpeed);
        WarningActive = distance >= 0 && eta <= obstacle.warningTime;
        int safe = -1, next = -1;
        bool changing = false;
        if (obstacle.type == ObstacleType.MovingGap && obstacle.gapSequence.Length > 0)
        {
            float period = Mathf.Max(.5f, obstacle.duration);
            float time = clock.Elapsed + obstacle.phaseOffset;
            int index = Mathf.FloorToInt(time / period);
            safe = obstacle.gapSequence[index % obstacle.gapSequence.Length];
            next = obstacle.gapSequence[(index + 1) % obstacle.gapSequence.Length];
            changing = period - time % period <= .45f;
            // Freeze the previewed opening during the actual crossing window.
            if (distance < 3.2f && distance > -.8f) { safe = SafeLane; next = safe; changing = false; }
            else SafeLane = safe;
        }
        for (int i = 0; i < lanes.Length; i++)
        {
            bool blocked = (obstacle.laneMask & (1 << i)) != 0;
            if (safe >= 0) blocked = i != safe && !(changing && i == next);
            lanes[i].enabled = blocked;
            if (i < visuals.Length && visuals[i] != null) visuals[i].enabled = blocked;
            Color color = blocked ? (WarningActive ? Color.yellow : new Color(1f,.28f,.07f)) : Color.cyan;
            if (i < visuals.Length && visuals[i] != null) Tint(visuals[i], color);
            if (i < indicators.Length && indicators[i] != null)
            {
                indicators[i].enabled = distance < obstacle.activationDistance && distance > -1f;
                Tint(indicators[i], changing && i == next ? Color.yellow : color);
            }
        }
        Pass(100);
    }
    private void Tint(Renderer renderer, Color color)
    {
        propertyBlock.SetColor("_BaseColor", color); propertyBlock.SetColor("_Color", color);
        propertyBlock.SetColor("_EmissionColor", color * (WarningActive ? 1.2f : .3f));
        renderer.SetPropertyBlock(propertyBlock);
    }
}
