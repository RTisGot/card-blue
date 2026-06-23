using TMPro;
using UnityEngine;
public class CardView : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    public void SetCard(CardState state)
    {
        if (labelText != null)
        {
            labelText.text = state.cardType.ToString();
        }
        transform.localRotation = Quaternion.Euler(0f, 0f, state.rotated ? 180f : 0f);
    }
}