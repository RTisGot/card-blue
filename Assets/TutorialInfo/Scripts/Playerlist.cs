using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerDisplay : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameText;

    private void Awake()
    {
        if (nameText == null)
        {
            nameText = GetComponent<TMP_Text>();
        }
    }

    public void UpdateName(string playerName)
    {
        UpdateName(playerName, false);
    }

    public void UpdateName(string playerName, bool isCurrentTurn)
    {
        if (nameText == null)
        {
            nameText = GetComponent<TMP_Text>();
        }

        if (nameText != null)
        {
            nameText.richText = true;
            nameText.text = isCurrentTurn
                ? $"<color=#FFD54A>▶ {playerName} のターン</color>"
                : playerName;
        }
    }
}

