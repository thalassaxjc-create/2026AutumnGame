using UnityEngine;

/// <summary>Displays today's sabotage using shared materials; scene reloads read the session state.</summary>
[DisallowMultipleComponent]
public sealed class XiaoPiBucketVisual : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColor = Shader.PropertyToID("_Color");
    [SerializeField] Renderer water;
    [SerializeField] Renderer curtain;
    MaterialPropertyBlock colors;
    Color cleanWater;
    Color cleanCurtain;
    bool initialized;
    bool hasRendered;
    bool displayedSabotage;
    public bool IsBleached => hasRendered && displayedSabotage;

    public void Configure(Renderer waterSurface, Renderer todaysCurtain)
    {
        water = waterSurface;
        curtain = todaysCurtain;
    }

    void OnEnable()
    {
        if (!initialized)
        {
            colors = new MaterialPropertyBlock();
            cleanWater = SharedColor(water);
            cleanCurtain = SharedColor(curtain);
            initialized = true;
        }
        hasRendered = false;
        Refresh();
    }

    void Update() => Refresh();

    void Refresh()
    {
        bool bleached = WorkshopOrder.Current?.XiaoPiSabotaged == true;
        if (hasRendered && displayedSabotage == bleached) return;
        displayedSabotage = bleached;
        hasRendered = true;
        Paint(water, bleached ? new Color32(238, 241, 229, 255) : cleanWater);
        Paint(curtain, bleached ? new Color32(250, 250, 241, 255) : cleanCurtain);
    }

    static Color SharedColor(Renderer surface)
    {
        var material = surface != null ? surface.sharedMaterial : null;
        return material != null ? material.color : Color.white;
    }

    void Paint(Renderer surface, Color color)
    {
        if (surface == null) return;
        surface.GetPropertyBlock(colors);
        colors.SetColor(BaseColor, color);
        colors.SetColor(LegacyColor, color);
        surface.SetPropertyBlock(colors);
    }

    void OnDisable()
    {
        if (!initialized) return;
        Paint(water, cleanWater);
        Paint(curtain, cleanCurtain);
        hasRendered = false;
    }
}
