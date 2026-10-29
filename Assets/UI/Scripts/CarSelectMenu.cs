using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CarSelectMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CarCatalog catalog;
    [SerializeField] Transform turntable;
    [SerializeField] Text nameText;

    [Header("Stat bars (empty RectTransforms, the script fills them)")]
    [SerializeField] RectTransform speedBar;
    [SerializeField] RectTransform accelerationBar;
    [SerializeField] RectTransform handlingBar;

    [Header("Bar look")]
    [SerializeField] int segments = 10;
    [SerializeField] Vector2 segmentSize = new Vector2(24f, 36f);
    [SerializeField] float segmentGap = 6f;
    [SerializeField] Color onColor  = new Color(.2f, .9f, 1f);
    [SerializeField] Color offColor = new Color(.15f, .25f, .35f, .7f);

    [Header("Stat ranges (a full bar = this value)")]
    [SerializeField] float maxTopSpeed = 250f;
    [SerializeField] float maxAcceleration = 10f;

    [Header("Showroom")]
    [SerializeField] float spinSpeed = 30f;
    [SerializeField] string raceScene = "Player";
    [SerializeField] string menuScene = "Menus";

    Image[] speedPips, accPips, handlingPips;
    GameObject current;
    int index;

    void Awake()
    {
        speedPips    = BuildBar(speedBar);
        accPips      = BuildBar(accelerationBar);
        handlingPips = BuildBar(handlingBar);
    }

    void Start()
    {
        index = Mathf.Clamp(GameSelection.CarIndex, 0, Mathf.Max(0, catalog.cars.Length - 1));
        Show();
    }

    void Update()
    {
        if (turntable != null) turntable.Rotate(0f, spinSpeed * Time.deltaTime, 0f);
    }

    public void Next()     { Step(+1); }
    public void Previous() { Step(-1); }

    void Step(int dir)
    {
        if (catalog.cars.Length == 0) return;
        index = (index + dir + catalog.cars.Length) % catalog.cars.Length;
        Show();
    }

    void Show()
    {
        if (current != null) Destroy(current);
        if (catalog.cars.Length == 0) return;

        var e = catalog.cars[index];
        if (e.prefab != null)
        {
            current = Instantiate(e.prefab, Vector3.zero, Quaternion.identity);
            CarFit.CenterAtOrigin(current);                 // fix the off-centre pivot
            current.transform.SetParent(turntable, false);  // keep the local offset
        }

        nameText.text = e.displayName.ToUpper();
        SetBar(speedPips,    e.topSpeed / maxTopSpeed);
        SetBar(accPips,      e.acceleration / maxAcceleration);
        SetBar(handlingPips, e.handling);
    }

    // ---------- stat bars ----------
    Image[] BuildBar(RectTransform parent)
    {
        var pips = new Image[segments];
        if (parent == null) return pips;

        for (int i = 0; i < segments; i++)
        {
            var go = new GameObject("Pip" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, .5f);
            rt.pivot = new Vector2(0f, .5f);
            rt.sizeDelta = segmentSize;
            rt.anchoredPosition = new Vector2(i * (segmentSize.x + segmentGap), 0f);
            pips[i] = img;
        }
        return pips;
    }

    void SetBar(Image[] pips, float value01)
    {
        int lit = Mathf.Clamp(Mathf.RoundToInt(value01 * segments), 0, segments);
        for (int i = 0; i < pips.Length; i++)
            if (pips[i] != null) pips[i].color = i < lit ? onColor : offColor;
    }

    // ---------- buttons ----------
    public void Race()
    {
        GameSelection.CarIndex = index;
        SceneManager.LoadScene(raceScene);
    }

    public void Back() => SceneManager.LoadScene(menuScene);
}