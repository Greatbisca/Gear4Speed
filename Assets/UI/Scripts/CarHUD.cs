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

    [Header("Velocímetro (ponteiro_v + número)")]
    [Tooltip("Calcula o fim da escala a partir do carro (topo do carro + boost).")]
    public bool autoMaxSpeed = true;
    public float maxSpeedKmh = 260f;
    public float speedMinAngle = -120f;
    public float speedMaxAngle = 120f;

    public float needleSmoothing = 12f;

    [Header("Barra de power-up (up_bar)")]
    [Tooltip("Largura da barra cheia, em px (a do up_bar no UXML).")]
    public float barFullWidth = 240f;
    [Tooltip("Velocidade a que a barra enche e esvazia.")]
    public float barSmoothing = 10f;
    [Tooltip("Cor da barra quando o power-up está pronto a usar.")]
    public Color barReadyColor = new Color(1f, 0.85f, 0.2f);

    [Header("Diagnóstico")]
    [Tooltip("Escreve na consola quantas mudanças o HUD vê e que slots mostra.")]
    public bool logLayout = true;

    VisualElement root, knob, pedal, upgrade5, upgrade6, needleRpm, needleSpeed, upBar;
    Label speedLabel, gearLabel;
    Gearbox gb;
    int lastGearCount = -1;
    int lastKnobRow = 99;
    int pedalState = -1;   // -1 = por aplicar, 0 = solto, 1 = premido
    string lastGearText;
    Vector2 knobPos;
    bool knobInit;
    float rpmAngle, speedAngle;

    // Barra de power-up
    float barFill;
    float turboElapsed;
    bool wasTurbo;
    Color barBaseColor = new Color(0.75f, 0.42f, 0.08f);

    void OnEnable()
    {
        QueryElements();

        rpmAngle = rpmMinAngle;
        speedAngle = speedMinAngle;
        ResetState();
    }

    void OnDisable()
    {
        if (gb != null) gb.GearsChanged -= RefreshLayout;
        gb = null;
    }

    // Vai buscar os elementos do UXML. Devolve false se a UI ainda não existe.
    bool QueryElements()
    {
        var doc = GetComponent<UIDocument>();
        root = doc != null ? doc.rootVisualElement : null;
        if (root == null) return false;

        knob = root.Q<VisualElement>("manipulo");
        pedal = root.Q<VisualElement>("acelarador");
        upgrade5 = root.Q<VisualElement>("upgrade_5");
        upgrade6 = root.Q<VisualElement>("upgrade_6");
        needleRpm = root.Q<VisualElement>("ponteiro_r");
        needleSpeed = root.Q<VisualElement>("ponteiro_v");
        upBar = root.Q<VisualElement>("up_bar");
        speedLabel = root.Q<Label>("display_velocimetro");
        gearLabel = root.Q<Label>("gear_display");

        if (upBar != null)
        {
            Color c = upBar.style.backgroundColor.value;   // cor definida no UXML
            if (c.a > 0f) barBaseColor = c;
        }
        return true;
    }

    void ResetState()
    {
        lastKnobRow = 99;
        pedalState = -1;
        knobInit = false;
        lastGearText = null;
        lastGearCount = -1;
    }

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || player.gearbox == null) return;

        // Se a UI foi reconstruída, as referências antigas ficam soltas: volta a buscá-las
        if (root == null || root.panel == null)
        {
            if (!QueryElements()) return;
            ResetState();
        }

        if (player.gearbox != gb) Bind(player.gearbox);
        else if (gb.GearCount != lastGearCount) RefreshLayout();   // rede de segurança

        float dt = Time.deltaTime;
        UpdateKnob(dt);
        UpdatePedal();
        UpdateNeedles(dt);
        UpdateGearLabel();
        UpdatePowerBar(dt);
    }

    void Bind(Gearbox g)
    {
        if (gb != null) gb.GearsChanged -= RefreshLayout;
        gb = g;
        gb.GearsChanged += RefreshLayout;
        RefreshLayout();
    }

    // Corre quando o número de mudanças (ou o carro) muda
    void RefreshLayout()
    {
        if (gb == null) return;

        int count = gb.GearCount;
        lastGearCount = count;

        if (upgrade5 != null) upgrade5.style.opacity = count >= 5 ? 1f : 0f;
        if (upgrade6 != null) upgrade6.style.opacity = count >= 6 ? 1f : 0f;

        if (autoMaxSpeed && gb.gears != null && gb.gears.Length > 0)
        {
            float topMs = gb.gears[gb.gears.Length - 1].maxSpeed;
            float turbo = player != null ? player.turboSpeedMultiplier : 1f;
            maxSpeedKmh = Mathf.Ceil(topMs * 3.6f * turbo / 10f) * 10f;
        }

        lastKnobRow = 99;   // força a actualizar o sprite do manipulo
        knobInit = false;   // e a posição, sem deslizar do carro anterior

        if (logLayout)
            Debug.Log($"[CarHUD] Gearbox '{gb.name}' tem {count} mudanças | " +
                      $"slot 5: {(upgrade5 == null ? "NÃO ENCONTRADO no UXML" : count >= 5 ? "visível" : "escondido")} | " +
                      $"slot 6: {(upgrade6 == null ? "NÃO ENCONTRADO no UXML" : count >= 6 ? "visível" : "escondido")} | " +
                      $"escala do velocímetro {maxSpeedKmh:F0} km/h", this);
    }

    void UpdateKnob(float dt)
    {
        if (knob == null) return;

        int col, row;
        if (gb.InClutch)
        {
            col = gb.StickColumn;          // posição real do stick (0 = neutro)
            row = gb.StickRow;
        }
        else if (gb.CurrentGear <= 0)
        {
            col = gb.StickColumn;          // motor calado: neutro
            row = 0;
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

        int state = gb.InClutch ? 1 : 0;
        if (state == pedalState) return;
        pedalState = state;

        Texture2D tex = state == 1 ? pedalPressed : pedalNormal;
        if (tex != null) pedal.style.backgroundImage = new StyleBackground(tex);
        pedal.style.bottom = state == 1 ? pedalBottomPressed : pedalBottomNormal;
    }

    void UpdateNeedles(float dt)
    {
        float k = 1f - Mathf.Exp(-needleSmoothing * dt);

        float rpmT = Mathf.InverseLerp(0f, gb.limiterRpm, gb.Rpm);
        rpmAngle = Mathf.Lerp(rpmAngle, Mathf.Lerp(rpmMinAngle, rpmMaxAngle, rpmT), k);
        SetRotation(needleRpm, rpmAngle);

        float kmh = Mathf.Abs(player.CurrentSpeed) * 3.6f;
        float speedT = Mathf.InverseLerp(0f, maxSpeedKmh, kmh);
        speedAngle = Mathf.Lerp(speedAngle, Mathf.Lerp(speedMinAngle, speedMaxAngle, speedT), k);
        SetRotation(needleSpeed, speedAngle);

        if (speedLabel != null) speedLabel.text = Mathf.RoundToInt(kmh).ToString();
    }

    // Mostra a mudança engatada; com a embraiagem premida, a posição do stick ("N" no neutro)
    void UpdateGearLabel()
    {
        if (gearLabel == null) return;

        string text;
        if (gb.InClutch)
        {
            if (gb.StickRow == 0) text = "N";
            else text = (gb.StickColumn * 2 + (gb.StickRow > 0 ? 1 : 2)).ToString();
        }
        else
        {
            text = gb.CurrentGear > 0 ? gb.CurrentGear.ToString() : "N";
        }

        if (text == lastGearText) return;
        lastGearText = text;
        gearLabel.text = text;
    }

    // Enche com cada mudança perfeita; esvazia durante o boost; amarela quando está pronta
    void UpdatePowerBar(float dt)
    {
        if (upBar == null) return;

        float target;
        bool ready = false;

        if (player.TurboActive)
        {
            if (!wasTurbo) turboElapsed = 0f;
            wasTurbo = true;
            turboElapsed += dt;
            target = 1f - Mathf.Clamp01(turboElapsed / Mathf.Max(0.1f, player.turboDuration));
        }
        else
        {
            wasTurbo = false;
            int need = Mathf.Max(1, player.chargesToActivate);
            target = Mathf.Clamp01(player.PowerUpCharges / (float)need);
            ready = player.PowerUpCharges >= need;
        }

        barFill = Mathf.Lerp(barFill, target, 1f - Mathf.Exp(-barSmoothing * dt));
        if (Mathf.Abs(barFill - target) < 0.002f) barFill = target;

        upBar.style.width = barFullWidth * barFill;
        upBar.style.backgroundColor = ready ? barReadyColor : barBaseColor;
    }

    static void SetRotation(VisualElement e, float degrees)
    {
        if (e != null)
            e.style.rotate = new StyleRotate(new Rotate(new Angle(degrees, AngleUnit.Degree)));
    }
}