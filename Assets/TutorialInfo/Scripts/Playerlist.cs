using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerDisplay : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameText; // 

    public void UpdateName(ulong clientId)
    {
        nameText.text = "Player: " + clientId.ToString();
    }
}
