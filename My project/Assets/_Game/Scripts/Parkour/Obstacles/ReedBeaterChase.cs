using UnityEngine;
public sealed class ReedBeaterChase : ParkourObstacle
{
    [SerializeField] private Transform beater;
    [SerializeField] private float startZ = 140f;
    [SerializeField] private float stopZ = 170f;
    private bool chasing;
    public float DistanceBehind => player != null && beater != null ? player.Position.z-beater.position.z : 8f;
    public bool Chasing => chasing;
    public void SetGeometry(Transform beam) { beater = beam; }
    public override void ResetAttempt(float checkpointZ) { base.ResetAttempt(checkpointZ); chasing=false;beater.gameObject.SetActive(false); }
    private void Update()
    {
        if(player==null||beater==null)return;
        if(!Running||player.Position.z<startZ||player.Position.z>stopZ){chasing=false;beater.gameObject.SetActive(false);return;}
        if(!chasing){chasing=true;beater.position=new Vector3(0,.8f,player.Position.z-7f);beater.gameObject.SetActive(true);}
        float speed=Mathf.Lerp(5.3f,6.9f,Mathf.InverseLerp(startZ,stopZ,player.Position.z));
        Vector3 p=beater.position;p.z+=speed*Time.deltaTime;
        p.z=Mathf.Max(p.z,player.Position.z-8f);beater.position=p;
        if(DistanceBehind<.8f)Hit();
        Pass(100);
    }
}
