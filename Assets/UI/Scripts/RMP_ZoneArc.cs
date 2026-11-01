using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument), typeof(CarHUD))]
public class RpmZoneArc : MonoBehaviour
{
    [Header("Forma")]
    public float radius = 58f;
    public float thickness = 8f;
    [Tooltip("Deslocamento do centro do arco em relação ao centro do conta-rotações. O pivô da agulha fica ~29px abaixo.")]
    public Vector2 centerOffset = new Vector2(0f, 29f);
    public float elementSize = 256f;
    [Tooltip("Desligado = atrás da imagem do conta-rotações. Ligado = por cima da imagem, atrás da agulha.")]
    public bool inFrontOfGauge = false;

    [Header("Semicírculo")]
    [Tooltip("Obriga a agulha do CarHUD a usar -90..+90 para coincidir com o arco.")]
    public bool syncNeedleToArc = true;

    [Header("Cores")]
    public Color idleColor = Color.white;
    public Color redColor = new Color(0.9f, 0.15f, 0.15f);
    public Color yellowColor = new Color(1f, 0.85f, 0.1f);
    public Color greenColor = new Color(0.2f, 0.85f, 0.3f);

    const float ArcStart = -90f;   // 0 rpm, na esquerda
    const float ArcEnd = 90f;   // rpm máximas, na direita

    CarHUD hud;
    VisualElement gauge, arc;

    // Estado usado no desenho (só redesenha quando muda)
    bool sZones;
    float sCenter, sGreen, sYellow, sPrompt, sLimiter;

    void Awake()
    {
        hud = GetComponent<CarHUD>();
        if (syncNeedleToArc)
        {
            hud.rpmMinAngle = ArcStart;
            hud.rpmMaxAngle = ArcEnd;
        }
    }

    void OnDisable()
    {
        arc?.RemoveFromHierarchy();
        arc = null;
        gauge = null;
    }

    void Update()
    {
        if (arc == null) TryBuild();
        if (arc == null || hud.player == null || hud.player.gearbox == null) return;

        Gearbox gb = hud.player.gearbox;

        // Embraiagem premida: cores logo, com a zona prevista até escolheres a mudança
        bool zones = gb.InClutch;
        float center = 0f;
        if (zones) center = gb.GearSelected ? gb.ZoneCenter : gb.PreviewZoneCenter;

        if (zones != sZones || !Mathf.Approximately(center, sCenter) ||
            gb.greenHalfWidth != sGreen || gb.yellowHalfWidth != sYellow ||
            gb.shiftPromptRpm != sPrompt || gb.limiterRpm != sLimiter)
        {
            sZones = zones; sCenter = center;
            sGreen = gb.greenHalfWidth; sYellow = gb.yellowHalfWidth;
            sPrompt = gb.shiftPromptRpm; sLimiter = gb.limiterRpm;
            arc.MarkDirtyRepaint();
        }
    }

    void TryBuild()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        gauge = root?.Q<VisualElement>("contarotacoes");
        if (gauge == null) return;

        arc = new VisualElement { pickingMode = PickingMode.Ignore };
        arc.style.position = Position.Absolute;
        arc.style.width = elementSize;
        arc.style.height = elementSize;
        arc.generateVisualContent += Draw;

        if (inFrontOfGauge) gauge.Insert(0, arc);
        else gauge.parent.Insert(gauge.parent.IndexOf(gauge), arc);

        gauge.RegisterCallback<GeometryChangedEvent>(_ => Reposition());
        Reposition();
    }

    void Reposition()
    {
        if (arc == null || gauge == null || float.IsNaN(gauge.layout.width)) return;

        Vector2 center = inFrontOfGauge
            ? new Vector2(gauge.layout.width, gauge.layout.height) * 0.5f
            : gauge.layout.center;
        center += centerOffset;

        arc.style.left = center.x - elementSize * 0.5f;
        arc.style.top = center.y - elementSize * 0.5f;
        arc.MarkDirtyRepaint();
    }

    void Draw(MeshGenerationContext mgc)
    {
        if (sLimiter <= 0f) return;

        var p = mgc.painter2D;
        p.lineWidth = thickness;
        Vector2 c = new Vector2(arc.contentRect.width, arc.contentRect.height) * 0.5f;

        if (sZones)
        {
            float yMin = sCenter - sYellow, gMin = sCenter - sGreen;
            float gMax = sCenter + sGreen, yMax = sCenter + sYellow;

            Seg(p, c, 0f, yMin, redColor);
            Seg(p, c, yMin, gMin, yellowColor);
            Seg(p, c, gMin, gMax, greenColor);
            Seg(p, c, gMax, yMax, yellowColor);
            Seg(p, c, yMax, sLimiter, redColor);
        }
        else
        {
            Seg(p, c, 0f, sPrompt, idleColor);
            Seg(p, c, sPrompt, sLimiter, redColor);
        }
    }

    void Seg(Painter2D p, Vector2 center, float fromRpm, float toRpm, Color color)
    {
        fromRpm = Mathf.Clamp(fromRpm, 0f, sLimiter);
        toRpm = Mathf.Clamp(toRpm, 0f, sLimiter);
        if (toRpm - fromRpm < 0.01f) return;

        p.BeginPath();
        p.strokeColor = color;
        p.Arc(center, radius,
              new Angle(RpmToDeg(fromRpm), AngleUnit.Degree),
              new Angle(RpmToDeg(toRpm), AngleUnit.Degree));
        p.Stroke();
    }

    // 0 rpm = esquerda, rpm máximas = direita, a passar pelo topo (180° exatos).
    // A agulha tem 0° para cima; no Painter2D 0° é para a direita, daí o -90.
    float RpmToDeg(float rpm)
    {
        float t = Mathf.Clamp01(rpm / sLimiter);
        return Mathf.Lerp(ArcStart, ArcEnd, t) - 90f;   // -180° .. 0°
    }
}