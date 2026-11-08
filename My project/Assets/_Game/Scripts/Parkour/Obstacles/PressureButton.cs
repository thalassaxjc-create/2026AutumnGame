using UnityEngine;
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class PressureButton : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform cap;
    [SerializeField] private float pressDepth = .055f;
    [SerializeField] private float pressDuration = .12f;
    [SerializeField] private float footTolerance = .16f;
    private Collider avatar;
    private Collider trigger;
    private Vector3 rest;
    private float pressedAt;
    public bool IsPressed { get; private set; }
    public event System.Action<PressureButton> Pressed;
    public void Configure(PlayerController controller,Transform visual)
    {player=controller;cap=visual;}
    private void Awake()
    {
        avatar=player.GetComponent<Collider>();trigger=GetComponent<Collider>();
        var body=GetComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
        trigger.isTrigger=true;
        rest=cap.localPosition;
    }
    public void ResetState(bool pressed=false)
    {
        IsPressed=pressed;pressedAt=Time.time-pressDuration;
        cap.localPosition=rest+(pressed?Vector3.down*pressDepth:Vector3.zero);
    }
    private void TryStep(Collider other)
    {
        if(IsPressed||other!=avatar||player.GameplayMode!=PlayerGameplayMode.AutoRun||!player.IsGrounded)return;
        float foot=avatar.bounds.min.y;
        Bounds area=trigger.bounds;
        if(foot<area.min.y-footTolerance||foot>area.min.y+footTolerance)return;
        Vector3 position=player.Position;
        if(position.x<area.min.x||position.x>area.max.x||position.z<area.min.z||position.z>area.max.z)return;
        IsPressed=true;pressedAt=Time.time;Pressed?.Invoke(this);
    }
    private void OnTriggerEnter(Collider other){TryStep(other);}
    private void OnTriggerStay(Collider other){TryStep(other);}
    private void Update()
    {
        if(!IsPressed)return;
        float t=Mathf.Clamp01((Time.time-pressedAt)/pressDuration);
        cap.localPosition=rest+Vector3.down*(pressDepth*Mathf.SmoothStep(0f,1f,t));
    }
}
