using UnityEngine;
using UnityEngine.UI;

public class CellComponent : MonoBehaviour
{
    public int x;
    public int y;

    private Image image;
    private Color defaultColor;

    private void Awake()
    {
        image = GetComponent<Image>();
        if (image != null)
        {
            defaultColor = image.color;
        }
    }

    public void SetPlacementHighlight(bool highlighted)
    {
        if (image == null)
        {
            image = GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            defaultColor = image.color;
        }

        image.color = highlighted
            ? new Color(1f, 0.92f, 0.2f, 0.75f)
            : defaultColor;
    }
}
