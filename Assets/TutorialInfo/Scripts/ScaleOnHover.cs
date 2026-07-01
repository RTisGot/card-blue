using UnityEngine;
using UnityEngine.EventSystems;

public class ScaleOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("ägëÂèkè¨Ç≥ÇπÇÈUI")]
    public RectTransform targetImage;

    [Header("ç≈è¨î{ó¶")]
    public float minScale = 1.0f;

    [Header("ç≈ëÂî{ó¶")]
    public float maxScale = 1.2f;

    [Header("ïœâªë¨ìx")]
    public float speed = 2.0f;

    private bool isHover = false;
    private Vector3 baseScale;

    private void Start()
    {
        if (targetImage != null)
        {
            baseScale = targetImage.localScale;
        }
    }

    private void Update()
    {
        if (targetImage == null) return;

        if (isHover)
        {
            float scale = Mathf.Lerp(
                minScale,
                maxScale,
                (Mathf.Sin(Time.time * speed) + 1f) * 0.5f
            );

            targetImage.localScale = baseScale * scale;
        }
        else
        {
            targetImage.localScale = baseScale;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHover = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHover = false;
    }
}