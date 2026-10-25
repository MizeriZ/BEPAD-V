using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates pads only inside its own runtime child. Existing panel children are untouched.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class MultiplayPadBoard : MonoBehaviour
{
    [SerializeField, Min(0)] private float padding = 8;
    [SerializeField, Min(0)] private float spacing = 6;
    [SerializeField] private Color idleColor = new Color(0.055f, 0.08f, 0.13f);
    private RectTransform gridRoot;
    private GridLayoutGroup grid;
    private Image[] pads;

    private void Start() => Initialize();

    public void Initialize()
    {
        if (pads != null) return;
        gridRoot = new GameObject("Generated Multiplayer Pads", typeof(RectTransform)).GetComponent<RectTransform>();
        gridRoot.gameObject.layer = gameObject.layer;
        gridRoot.SetParent(transform, false);
        gridRoot.anchorMin = Vector2.zero;
        gridRoot.anchorMax = Vector2.one;
        gridRoot.offsetMin = gridRoot.offsetMax = Vector2.zero;
        grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 8;
        grid.childAlignment = TextAnchor.MiddleCenter;
        pads = new Image[64];
        for (int i = 0; i < 64; i++)
        {
            var pad = new GameObject($"Pad {(8 - i / 8) * 10 + i % 8 + 1}", typeof(RectTransform), typeof(Image));
            pad.layer = gameObject.layer;
            pad.transform.SetParent(gridRoot, false);
            pads[i] = pad.GetComponent<Image>();
            pads[i].color = idleColor;
            pads[i].raycastTarget = false;
        }
        Resize();
    }

    private void OnRectTransformDimensionsChange() { if (grid != null) Resize(); }
    private void Resize()
    {
        Rect rect = ((RectTransform)transform).rect;
        float cell = Mathf.Max(0, (Mathf.Min(rect.width, rect.height) - 2 * padding - 7 * spacing) / 8);
        grid.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        grid.spacing = new Vector2(spacing, spacing);
        grid.cellSize = new Vector2(cell, cell);
    }

    public void SetPad(int note, Color color)
    {
        Initialize();
        int row = note / 10, column = note % 10;
        if (row >= 1 && row <= 8 && column >= 1 && column <= 8)
            pads[(8 - row) * 8 + column - 1].color = color;
    }

    public void Clear()
    {
        Initialize();
        foreach (Image pad in pads) pad.color = idleColor;
    }

    private void OnDestroy() { if (gridRoot != null) Destroy(gridRoot.gameObject); }
}
