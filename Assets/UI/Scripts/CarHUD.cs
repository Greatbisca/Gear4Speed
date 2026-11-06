using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CarHUD : MonoBehaviour
{
    [Header("Referências (vazio = procura na cena)")]
    public PlayerController player;

    [Header("Sprites do manipulo")]
    public Texture2D manipuloNormal;
    public Texture2D manipuloUp;
    public Texture2D manipuloDown;

    [Header("Sprites do pedal (embraiagem)")]
    public Texture2D pedalNormal;
    public Texture2D pedalPressed;
    public float pedalBottomNormal = -27f;
    public float pedalBottomPressed = -36f;

    [Header("Sprites do ecrã de mudanças")]
    public Texture2D gearScreenNormal;
    public Texture2D gearScreenUp5;
    public Texture2D gearScreenUp6;
    public float gearPromptBlinkSpeed = 5f;

    [Header("Posições do manipulo (left / bottom)")]
    [Tooltip("x da coluna 0 (mudanças 1 e 2). Cada coluna seguinte soma 'columnStep'.")]
    public float column0X = -18f;
    public float columnStep = 36f;
    public float neutralY = 44f;
    public float topY = 88f;       // mudanças 1, 3, 5
    public float bottomY = -42f;   // mudanças 2, 4, 6
    public float knobSmoothing = 30f;

    [Header("Conta-rotações (ponteiro_r)")]
    public float rpmMinAngle = -120f;
    public float rpmMaxAngle = 120f;
    public float rpmNeedleAngleOffset = -5f;

    [Header("Velocímetro (ponteiro_v + número)")]
    public float maxSpeedKmh = 160f;
    public float speedMinAngle = -120f;
    public float speedMaxAngle = 120f;

    [Header("Barra do power-up")]
    [Min(0f)]
    public float powerUpBarWidth = 240f;

    public float needleSmoothing = 12f;

    VisualElement knob, pedal, gearScreen, upgrade5, upgrade6, powerUpBar, needleRpm, needleSpeed;
    Label speedLabel, gearLabel;
    Gearbox gb;
    int lastGearCount = -1;
    int lastKnobRow = 99;
    bool lastPedal;
    Vector2 knobPos;
    bool knobInit;
    float rpmAngle, speedAngle;
    float gearPromptBlinkTimer;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        knob = root.Q<VisualElement>("manipulo");
        pedal = root.Q<VisualElement>("acelarador");
        gearScreen = root.Q<VisualElement>("gearscreen");
        upgrade5 = root.Q<VisualElement>("upgrade_5");
        upgrade6 = root.Q<VisualElement>("upgrade_6");
        powerUpBar = root.Q<VisualElement>("up_bar");
        needleRpm = root.Q<VisualElement>("ponteiro_r");
        needleSpeed = root.Q<VisualElement>("ponteiro_v");
        speedLabel = root.Q<Label>("display_velocimetro");
        gearLabel = root.Q<Label>("gear_display");

        rpmAngle = rpmMinAngle;
        speedAngle = speedMinAngle;
        lastKnobRow = 99;
        lastGearCount = -1;
        knobInit = false;
    }

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || player.gearbox == null) return;
        gb = player.gearbox;
        float dt = Time.deltaTime;

        UpdateUpgrades();
        UpdateKnob(dt);
        UpdatePedal();
        UpdateNeedles(dt);
        UpdateGearScreen(dt);
        UpdatePowerUpBar();
    }

    void UpdateGearScreen(float dt)
    {
        if (gearScreen != null)
        {
            Texture2D screen = gb.GearCount >= 6
                ? gearScreenUp6
                : gb.GearCount >= 5
                    ? gearScreenUp5
                    : gearScreenNormal;
            if (screen != null)
                gearScreen.style.backgroundImage = new StyleBackground(screen);
        }

        if (gearLabel == null)
            return;

        if (gb.ShiftPromptActive)
        {
            gearPromptBlinkTimer += dt * Mathf.Max(0.1f, gearPromptBlinkSpeed);
            bool visible = Mathf.FloorToInt(gearPromptBlinkTimer) % 2 == 0;
            gearLabel.text = visible ? gb.NextGear.ToString() : string.Empty;
        }
        else
        {
            gearPromptBlinkTimer = 0f;
            gearLabel.text = gb.CurrentGear > 0
                ? gb.CurrentGear.ToString()
                : "N";
        }
    }

    void UpdatePowerUpBar()
    {
        if (powerUpBar == null)
            return;

        int maxCharges = Mathf.Max(1, player.chargesToActivate);
        float progress = Mathf.Clamp01((float)player.PowerUpCharges / maxCharges);
        float barWidth = powerUpBarWidth > 0f ? powerUpBarWidth : 240f;
        powerUpBar.style.width = barWidth;
        powerUpBar.style.scale = new StyleScale(
            new Scale(new Vector3(progress, 1f, 1f)));
    }

    // 5ª e 6ª aparecem conforme o carro
    void UpdateUpgrades()
    {
        if (gb.GearCount == lastGearCount) return;
        lastGearCount = gb.GearCount;
        if (upgrade5 != null) upgrade5.style.opacity = gb.GearCount >= 5 ? 1f : 0f;
        if (upgrade6 != null) upgrade6.style.opacity = gb.GearCount >= 6 ? 1f : 0f;
    }

    void UpdateKnob(float dt)
    {
        if (knob == null) return;

        int col, row;
        if (gb.InClutch)
        {
            col = gb.StickColumn;   // posição real do stick (0 = neutro)
            row = gb.StickRow;
        }
        else
        {
            col = (gb.CurrentGear - 1) / 2;                // assenta na mudança engatada
            row = gb.CurrentGear % 2 == 1 ? 1 : -1;
        }

        float x = column0X + col * columnStep;
        float y = row > 0 ? topY : (row < 0 ? bottomY : neutralY);
        Vector2 target = new Vector2(x, y);

        if (!knobInit) { knobPos = target; knobInit = true; }
        else knobPos = Vector2.Lerp(knobPos, target, 1f - Mathf.Exp(-knobSmoothing * dt));

        knob.style.left = knobPos.x;
        knob.style.bottom = knobPos.y;

        // sprite: só muda quando a linha muda
        if (row != lastKnobRow)
        {
            lastKnobRow = row;
            Texture2D tex = row > 0 ? manipuloUp : (row < 0 ? manipuloDown : manipuloNormal);
            if (tex != null) knob.style.backgroundImage = new StyleBackground(tex);
        }
    }

    // O pedal representa a embraiagem (espaço)
    void UpdatePedal()
    {
        if (pedal == null) return;
        bool pressed = gb.InClutch;
        if (pressed == lastPedal && pedal.style.backgroundImage.value.texture != null) return;
        lastPedal = pressed;

        Texture2D tex = pressed ? pedalPressed : pedalNormal;
        if (tex != null) pedal.style.backgroundImage = new StyleBackground(tex);
        pedal.style.bottom = pressed ? pedalBottomPressed : pedalBottomNormal;
    }

    void UpdateNeedles(float dt)
    {
        float k = 1f - Mathf.Exp(-needleSmoothing * dt);

        float rpmT = Mathf.InverseLerp(0f, gb.limiterRpm, gb.Rpm);
        rpmAngle = Mathf.Lerp(rpmAngle, Mathf.Lerp(rpmMinAngle, rpmMaxAngle, rpmT), k);
        SetRotation(needleRpm, rpmAngle + rpmNeedleAngleOffset);

        float kmh = Mathf.Abs(player.CurrentSpeed) * 3.6f;
        float speedT = Mathf.InverseLerp(0f, maxSpeedKmh, kmh);
        speedAngle = Mathf.Lerp(speedAngle, Mathf.Lerp(speedMinAngle, speedMaxAngle, speedT), k);
        SetRotation(needleSpeed, speedAngle);

        if (speedLabel != null) speedLabel.text = Mathf.RoundToInt(kmh).ToString();
    }

    static void SetRotation(VisualElement e, float degrees)
    {
        if (e != null)
            e.style.rotate = new StyleRotate(new Rotate(new Angle(degrees, AngleUnit.Degree)));
    }
}