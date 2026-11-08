using UnityEngine;
public sealed class ButtonThreadObstacle : ParkourObstacle
{
    [SerializeField] private PressureButton whiteButton;
    [SerializeField] private PressureButton yellowButton;
    [SerializeField] private GameObject whiteThread;
    [SerializeField] private GameObject yellowThread;
    public bool WhiteOpen { get; private set; }
    public bool YellowOpen { get; private set; }
    public float ButtonZ => obstacle.interactionZ;
    public event System.Action<ButtonThreadObstacle, bool> ThreadOpened;
    public void SetGeometry(PressureButton white,PressureButton yellow,GameObject rightWhite,GameObject leftYellow)
    {whiteButton=white;yellowButton=yellow;whiteThread=rightWhite;yellowThread=leftYellow;}
    private void Awake(){WhiteOpen=false;YellowOpen=false;whiteThread.SetActive(true);yellowThread.SetActive(true);}
    private void OnEnable(){whiteButton.Pressed+=OnWhite;yellowButton.Pressed+=OnYellow;}
    private void OnDisable(){if(whiteButton!=null)whiteButton.Pressed-=OnWhite;if(yellowButton!=null)yellowButton.Pressed-=OnYellow;}
    private void OnWhite(PressureButton button)
    {if(WhiteOpen)return;WhiteOpen=true;whiteThread.SetActive(false);ThreadOpened?.Invoke(this,true);}
    private void OnYellow(PressureButton button)
    {if(YellowOpen)return;YellowOpen=true;yellowThread.SetActive(false);ThreadOpened?.Invoke(this,false);}
    public override void ResetAttempt(float checkpointZ)
    {
        base.ResetAttempt(checkpointZ);
        WhiteOpen=passed;YellowOpen=passed;
        whiteThread.SetActive(!WhiteOpen);yellowThread.SetActive(!YellowOpen);
        whiteButton.ResetState(passed);yellowButton.ResetState(passed);
    }
    private void Update(){if(player!=null&&obstacle!=null)Pass(100);}
}
