using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class GearMenu : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string gear = "1";
        [Tooltip("Column 0-2, row -1 (up) or 1 (down)")]
        public Vector2Int slot = new Vector2Int(0, -1);
        public string title = "PLAY";
        public bool hideOnWebGL;
        public UnityEvent onEngage = new UnityEvent();
    }

    [Header("Layout")]
    [SerializeField] RectTransform shifter;
    [SerializeField] float spacingX = 220f;
    [SerializeField] float spacingY = 140f;
    [SerializeField] float lineWidth = 8f;

    [Header("Colors")]
    [SerializeField] Color lineColor   = new Color(.55f, .55f, .6f);
    [SerializeField] Color idleColor   = new Color(.7f, .7f, .75f);
    [SerializeField] Color activeColor = new Color(.3f, .9f, .3f);
    [SerializeField] Color knobColor   = new Color(1f, .55f, .2f);
    [SerializeField] Color movingColor = new Color(.3f, .7f, 1f);

    [Header("Behaviour")]
    [SerializeField] float knobSpeed = 900f;          // pixels per second
    [SerializeField] float engageDelay = 0.15f;       // pause before the option fires
    [SerializeField] TMP_Text hintLabel;
    [Tooltip("Info panels. While one is open the stick is locked; when it closes the knob returns to neutral.")]
    [SerializeField] GameObject[] panels;

    [SerializeField] Entry[] entries =
    {
        new Entry { gear = "1", slot = new Vector2Int(0, -1), title = "PLAY" },
        new Entry { gear = "2", slot = new Vector2Int(1,  1), title = "HOW TO PLAY" },
        new Entry { gear = "3", slot = new Vector2Int(1, -1), title = "SETTINGS" },
        new Entry { gear = "4", slot = new Vector2Int(2, -1), title = "CREDITS" },
        new Entry { gear = "R", slot = new Vector2Int(0,  1), title = "QUIT" },
    };

    class Slot { public Entry e; public TMP_Text label; public bool hover; }

    readonly List<Slot> slots = new List<Slot>();
    Image knob;
    int col = 1, row = 0;          // stick position: starts in neutral (centre)
    bool moving;
    bool panelWasOpen;
    Slot engaged;

    Vector2 P(int c, int r) => new Vector2((c - 1) * spacingX, -r * spacingY);

    // ---------------- Build the shifter graphics ----------------
    void Awake()
    {
        foreach (var e in entries)
        {
            if (e.hideOnWebGL && Application.platform == RuntimePlatform.WebGLPlayer) continue;
            slots.Add(new Slot { e = e });
        }

        // gate lines
        var minRow = new Dictionary<int, int>();
        var maxRow = new Dictionary<int, int>();
        int minCol = 2, maxCol = 0;
        foreach (var s in slots)
        {
            int c = s.e.slot.x, r = s.e.slot.y;
            minRow[c] = Mathf.Min(minRow.ContainsKey(c) ? minRow[c] : 0, r);
            maxRow[c] = Mathf.Max(maxRow.ContainsKey(c) ? maxRow[c] : 0, r);
            minCol = Mathf.Min(minCol, c);
            maxCol = Mathf.Max(maxCol, c);
        }
        Line(P(minCol, 0), P(maxCol, 0), "GateNeutral");
        foreach (var c in minRow.Keys)
            Line(P(c, minRow[c]), P(c, maxRow[c]), "Gate" + c);

        // clickable labels
        foreach (var s in slots)
        {
            var go = new GameObject("Gear " + s.e.gear, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(shifter, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(spacingX - 20f, 110f);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.anchoredPosition = P(s.e.slot.x, s.e.slot.y) + new Vector2(0, s.e.slot.y < 0 ? 80f : -80f);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = 26;
            t.text = $"<size=64><b>{s.e.gear}</b></size>\n{s.e.title}";
            s.label = t;

            var slot = s;
            var b = go.AddComponent<Button>();
            b.targetGraphic = t;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Choose(slot));

            var hover = go.AddComponent<GearMenuHover>();
            hover.onEnter = () => slot.hover = true;
            hover.onExit  = () => slot.hover = false;
        }

        // knob (created last = drawn on top)
        var kgo = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        kgo.transform.SetParent(shifter, false);
        knob = kgo.GetComponent<Image>();
        knob.raycastTarget = false;
        knob.rectTransform.sizeDelta = new Vector2(46f, 46f);
        knob.rectTransform.anchoredPosition = P(col, row);
        knob.color = knobColor;
    }

    void Line(Vector2 a, Vector2 b, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(shifter, false);
        var img = go.GetComponent<Image>();
        img.color = lineColor;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.anchoredPosition = (a + b) * .5f;
        rt.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) + lineWidth, Mathf.Abs(b.y - a.y) + lineWidth);
    }

    // ---------------- Logic ----------------
    bool AnyPanelOpen()
    {
        foreach (var p in panels) if (p != null && p.activeSelf) return true;
        return false;
    }

    void Choose(Slot s)
    {
        if (moving || AnyPanelOpen()) return;
        StartCoroutine(GoToGear(s));
    }

    // Slide the knob along the gate: out of the slot, along neutral, into the new slot
    IEnumerator GoToGear(Slot s)
    {
        moving = true;
        knob.color = movingColor;

        var path = new List<Vector2>();
        int tc = s.e.slot.x, tr = s.e.slot.y;
        if (row != 0) path.Add(P(col, 0));
        if (col != tc) path.Add(P(tc, 0));
        path.Add(P(tc, tr));
        yield return Travel(path);

        col = tc; row = tr;
        knob.color = knobColor;
        engaged = s;

        if (engageDelay > 0f) yield return new WaitForSeconds(engageDelay);
        moving = false;
        s.e.onEngage.Invoke();
    }

    // Slide the knob back to the centre (neutral)
    IEnumerator GoToNeutral()
    {
        moving = true;
        knob.color = movingColor;

        var path = new List<Vector2>();
        if (row != 0) path.Add(P(col, 0));
        path.Add(P(1, 0));
        yield return Travel(path);

        col = 1; row = 0;
        engaged = null;
        knob.color = knobColor;
        moving = false;
    }

    IEnumerator Travel(List<Vector2> path)
    {
        foreach (var target in path)
        {
            while (((Vector2)knob.rectTransform.anchoredPosition - target).sqrMagnitude > 0.5f)
            {
                knob.rectTransform.anchoredPosition = Vector2.MoveTowards(
                    knob.rectTransform.anchoredPosition, target, knobSpeed * Time.unscaledDeltaTime);
                yield return null;
            }
            knob.rectTransform.anchoredPosition = target;
        }
    }

    void Update()
    {
        // A panel just closed (Back button): return the knob to neutral
        bool open = AnyPanelOpen();
        if (panelWasOpen && !open && !moving && (col != 1 || row != 0))
            StartCoroutine(GoToNeutral());
        panelWasOpen = open;

        foreach (var s in slots)
        {
            bool isCurrent = s == engaged;
            s.label.color = (isCurrent || (s.hover && !moving && !open)) ? activeColor : idleColor;
        }

        if (hintLabel != null)
            hintLabel.text = "Click a gear to select it";
    }
}

// Tells GearMenu when the mouse is over a label
public class GearMenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action onEnter, onExit;
    public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData e)  => onExit?.Invoke();
}