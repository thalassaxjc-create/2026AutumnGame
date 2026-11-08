using UnityEngine;
[RequireComponent(typeof(Rigidbody))]
public sealed class MovingSurface : MonoBehaviour
{
    private Rigidbody body;
    private Vector3 previous;
    private static readonly System.Collections.Generic.Dictionary<Rigidbody, MovingSurface> surfaces = new System.Collections.Generic.Dictionary<Rigidbody, MovingSurface>();
    public Vector3 Velocity { get; private set; }
    public Vector3 TargetPosition => previous;
    public void ResetTo(Vector3 position)
    {
        previous = position;
        Velocity = Vector3.zero;
        body.position = position;
    }
    public static bool TryGet(Rigidbody rigidbody, out MovingSurface surface)
    {
        surface = null;
        return rigidbody != null && surfaces.TryGetValue(rigidbody, out surface);
    }
    private void Awake() { body = GetComponent<Rigidbody>(); previous = body.position; }
    private void OnEnable() { surfaces[body] = this; }
    public void MoveTo(Vector3 position)
    {
        Velocity = (position - previous) / Time.fixedDeltaTime;
        previous = position;
        body.MovePosition(position);
    }
    private void OnDisable() { Velocity = Vector3.zero; if (body != null) surfaces.Remove(body); }
}
