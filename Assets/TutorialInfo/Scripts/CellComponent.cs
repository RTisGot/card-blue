using UnityEngine;
using UnityEngine.UI;

public class CellComponent : MonoBehaviour
{
    public int x;
    public int y;

    private Image image;
    private RectTransform rectTransform;
    private RectTransform highlightRect;
    private Image highlightImage;
    private Color defaultColor;

    private void Awake()
    {
        CacheComponents();
    }

    public void SetPlacementHighlight(bool highlighted)
    {
        CacheComponents();
        EnsureHighlightOverlay();

        if (highlightRect == null)
        {
            if (image != null)
            {
                image.color = highlighted
                    ? new Color(1f, 0.88f, 0.12f, 0.8f)
                    : defaultColor;
            }

            return;
        }

        if (highlighted)
        {
            SyncHighlightOverlay();
            highlightRect.SetAsLastSibling();
        }

        highlightRect.gameObject.SetActive(highlighted);
    }

    private void CacheComponents()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
            if (image != null)
            {
                defaultColor = image.color;
            }
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    }

    private void EnsureHighlightOverlay()
    {
        if (highlightRect != null)
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject highlightObject = new GameObject($"PlacementHighlight_{x}_{y}");
        highlightObject.transform.SetParent(canvas.transform, false);
        highlightObject.layer = gameObject.layer;

        highlightRect = highlightObject.AddComponent<RectTransform>();
        highlightImage = highlightObject.AddComponent<Image>();
        highlightImage.color = new Color(1f, 0.88f, 0.05f, 0.75f);
        highlightImage.raycastTarget = false;

        Outline outline = highlightObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 0.35f, 1f);
        outline.effectDistance = new Vector2(5f, -5f);

        highlightObject.SetActive(false);
    }

    private void SyncHighlightOverlay()
    {
        if (rectTransform == null || highlightRect == null)
        {
            return;
        }

        highlightRect.position = rectTransform.position;
        highlightRect.rotation = rectTransform.rotation;
        highlightRect.localScale = rectTransform.lossyScale;
        highlightRect.sizeDelta = rectTransform.rect.size;
        highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
        highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
        highlightRect.pivot = rectTransform.pivot;
    }

    private void OnDestroy()
    {
        if (highlightRect != null)
        {
            Destroy(highlightRect.gameObject);
        }
    }
}