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
        if (nameText == null)
        {
            nameText = GetComponent<TMP_Text>();
        }

        if (nameText != null)
        {
            nameText.text = playerName;
        }
    }
}

