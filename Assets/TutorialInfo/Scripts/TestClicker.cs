using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



public class TestClicker : NetworkBehaviour
{
    //Ui
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private TMP_InputField xInput;
    [SerializeField] private TMP_InputField yInput;
    [SerializeField] private Button placeButton;
    private void Start()
    {
        if (placeButton != null)
        {
            placeButton.onClick.AddListener(PlaceTestCard);
        }
    }
    public void PlaceTestCard()
    {
        int x = ParseInput(xInput, 1);
        int y = ParseInput(yInput, 0);
        if (boardManager != null)
        {
            boardManager.TryPlaceCardFromUI(x, y);
        }
    }
    private static int ParseInput(TMP_InputField inputField, int fallback)
    {
        if (inputField == null || !int.TryParse(inputField.text, out int value))
        {
            return fallback;
        }
        return value;
    }
}