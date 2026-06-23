using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerDisplay : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameText;

    private void Awake()
    {
        EnsureNameText();
        ShowFallbackName();
    }

    private void OnEnable()
    {
        EnsureNameText();
        ShowFallbackName();
    }

    public void UpdateName(ulong clientId)
    {
        UpdateName("Player " + clientId);
    }

    public void UpdateName(string playerName)
    {
        EnsureNameText();

        if (nameText != null)
        {
            nameText.text = playerName;
        }
    }

    private void EnsureNameText()
    {
        if (nameText == null)
        {
            nameText = GetComponent<TMP_Text>();
        }

        if (nameText == null)
        {
            nameText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void ShowFallbackName()
    {
        if (nameText == null)
        {
            return;
        }

        if (nameText.text == "Text" || nameText.text == "New Text" || string.IsNullOrWhiteSpace(nameText.text))
        {
            string playerName = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.SavedPlayerName
                : "Player";
            nameText.text = playerName;
        }
    }
}
