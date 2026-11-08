using UnityEngine;
public sealed class HangingWeightPendulum : ParkourObstacle
{
    [SerializeField] private MovingSurface surface;
    [SerializeField] private RopeVisual rope;
    [SerializeField] private Transform hazardSensor;
    [SerializeField] private float ropeLength = 6f;
    [SerializeField] private float angle = 35f;
    [SerializeField] private float period = 2.4f;
    [SerializeField] private bool standable = true;
    [SerializeField] private Renderer cue;
    private Vector3 anchor;
    private readonly Vector3[] endpoints = new Vector3[2];
    private MaterialPropertyBlock propertyBlock;
    public bool NearMiss { get; private set; }
    public bool Ridden { get; private set; }
    public override void ResetAttempt(float checkpointZ)
    { base.ResetAttempt(checkpointZ); NearMiss=false; Ridden=false; }
    public void SetGeometry(MovingSurface moving, RopeVisual visual, Transform sensor, Renderer warning, float swingAngle, float cycle)
    { surface = moving; rope = visual; hazardSensor = sensor; cue = warning; angle = swingAngle; period = cycle; }
    private void Awake() { anchor = transform.position; propertyBlock = new MaterialPropertyBlock(); }
    private void FixedUpdate()
    {
        if (surface == null || clock == null || obstacle == null) return;
        float theta = Mathf.Sin((clock.Elapsed + obstacle.phaseOffset) * Mathf.PI * 2f / period) * angle * Mathf.Deg2Rad;
        Vector3 tie = anchor + new Vector3(Mathf.Sin(theta)*ropeLength,-Mathf.Cos(theta)*ropeLength,0f);
        surface.MoveTo(tie - Vector3.up * .73f);
        hazardSensor.position = tie - Vector3.up * .15f;
        endpoints[0] = anchor; endpoints[1] = tie; rope.SetPoints(endpoints);
    }
    private void Update()
    {
        if (player == null || obstacle == null) return;
        float distance = obstacle.z - player.Position.z;
        bool warning = distance >= 0f && distance / Mathf.Max(.1f,player.AutoRunSpeed) <= obstacle.warningTime;
        if (cue != null) { propertyBlock.SetColor("_BaseColor",warning ? Color.yellow : Color.cyan); cue.SetPropertyBlock(propertyBlock); }
        if (Running && surface != null && Mathf.Abs(distance) < 1.2f)
        {
            float separation = Mathf.Abs(player.Position.x - surface.transform.position.x);
            if (separation > 1.85f && separation < 2.6f) NearMiss=true;
            if (standable && player.IsGrounded && separation < 1.4f &&
                Mathf.Abs(player.Position.y-surface.transform.position.y-1.35f)<.25f) Ridden=true;
        }
        Pass(100);
    }
    protected override void OnTriggerEnter(Collider other)
    {
        if (standable && player != null && surface != null && player.Position.y > surface.transform.position.y + 1.1f) return;
        base.OnTriggerEnter(other);
    }
}
