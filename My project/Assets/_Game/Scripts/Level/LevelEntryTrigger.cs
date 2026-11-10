using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class LevelEntryTrigger : MonoBehaviour
{
    [Header("Player Detection")]
    [SerializeField, Tooltip("Optional. When unset, any collider below a PlayerController is accepted.")]
    private PlayerController playerOverride;

    [Header("Lighting Transition")]
    [SerializeField] private Light primaryDirectionalLight;
    [SerializeField, Min(0.1f)] private float transitionDuration = 1.75f;
    [SerializeField, Min(0f)] private float targetDirectionalIntensity = 0.22f;
    [SerializeField] private Color targetAmbientColor = new Color(0.13f, 0.15f, 0.19f, 1f);

    [Header("Future Level Integration")]
    [SerializeField, Tooltip("Extension point for a future scene loader or level activator. No scene is loaded by this component.")]
    private UnityEvent transitionCompleted = new UnityEvent();

    private bool hasTriggered;
    private Coroutine transitionRoutine;

    public bool HasTriggered => hasTriggered;
    public UnityEvent TransitionCompleted => transitionCompleted;

    public void Configure(
        Light directionalLight,
        float duration = 1.75f,
        float minimumDirectionalIntensity = 0.22f)
    {
        primaryDirectionalLight = directionalLight;
        transitionDuration = Mathf.Max(0.1f, duration);
        targetDirectionalIntensity = Mathf.Max(0f, minimumDirectionalIntensity);
    }

    private void Reset()
    {
        ConfigurePhysicsComponents();
    }

    private void Awake()
    {
        ConfigurePhysicsComponents();
    }

    private void ConfigurePhysicsComponents()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !IsPlayerCollider(other))
        {
            return;
        }

        hasTriggered = true;
        transitionRoutine = StartCoroutine(DarkenLighting());
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        PlayerController candidate = other.GetComponentInParent<PlayerController>();
        if (candidate == null && other.attachedRigidbody != null)
        {
            candidate = other.attachedRigidbody.GetComponentInParent<PlayerController>();
        }

        return candidate != null && (playerOverride == null || candidate == playerOverride);
    }

    private IEnumerator DarkenLighting()
    {
        float startDirectionalIntensity = primaryDirectionalLight != null
            ? primaryDirectionalLight.intensity
            : 0f;
        Color startAmbientColor = RenderSettings.ambientLight;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / transitionDuration);
            float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);

            if (primaryDirectionalLight != null)
            {
                primaryDirectionalLight.intensity = Mathf.Lerp(
                    startDirectionalIntensity,
                    targetDirectionalIntensity,
                    easedTime);
            }

            RenderSettings.ambientLight = Color.Lerp(
                startAmbientColor,
                targetAmbientColor,
                easedTime);
            yield return null;
        }

        if (primaryDirectionalLight != null)
        {
            primaryDirectionalLight.intensity = targetDirectionalIntensity;
        }

        RenderSettings.ambientLight = targetAmbientColor;
        transitionRoutine = null;

        // Future integration point: attach a scene-loading/level-activation listener
        // to this event instead of adding scene-loading responsibilities here.
        transitionCompleted.Invoke();
        OnTransitionCompleted();
    }

    protected virtual void OnTransitionCompleted()
    {
    }

    private void OnDisable()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }
}
