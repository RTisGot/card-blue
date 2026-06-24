using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class CardGameManager : NetworkBehaviour
{
    [SerializeField] private TMP_Text statusText;

    [Header("UI Slots (0 = Main Player, 1~3 = Other Players)")]
    [SerializeField] private List<TMP_Text> seatNameTexts = new List<TMP_Text>();

    private NetworkList<FixedString64Bytes> connectedPlayerNames;

    private void Awake()
    {
        connectedPlayerNames = new NetworkList<FixedString64Bytes>();
        
    }

    public override void OnNetworkSpawn()
    {
        
        connectedPlayerNames.OnListChanged += OnPlayerListChanged;

        if (IsServer)
        {
            string hostName = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.SavedPlayerName
                : "HostPlayer";

           
                AddPlayerName(hostName);
            
        }
        else
        {
            StartCoroutine(RegisterNameDelayed());
        }

        RefreshSeatUI();
    }

    private IEnumerator RegisterNameDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        string clientName = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.SavedPlayerName
            : "GuestPlayer";

        RegisterPlayerNameServerRpc(clientName);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegisterPlayerNameServerRpc(FixedString64Bytes nameOfPlayer)
    {
        AddPlayerName(nameOfPlayer);
    }

    private void AddPlayerName(FixedString64Bytes nameOfPlayer)
    {
        if (!IsServer || connectedPlayerNames.Contains(nameOfPlayer))
        {
            return;
        }

        connectedPlayerNames.Add(nameOfPlayer);
        Debug.Log($"Server registered player name: {nameOfPlayer}");
    }

    private void OnPlayerListChanged(NetworkListEvent<FixedString64Bytes> changeEvent)
    {
        RefreshSeatUI();
    }

    private void RefreshSeatUI()
    {
        if (statusText != null)
        {
            statusText.text = connectedPlayerNames.Count >= 2
                ? "Ready"
                : "Waiting for players...";
        }

        for (int i = 0; i < seatNameTexts.Count; i++)
        {
            if (seatNameTexts[i] == null)
            {
                GameObject found = GameObject.Find($"playerText{i}");
                if (found != null)
                {
                    seatNameTexts[i] = found.GetComponent<TextMeshProUGUI>();
                }
            }

            if (seatNameTexts[i] != null)
            {
                seatNameTexts[i].text = "Waiting...";
            }
        }

        for (int i = 0; i < connectedPlayerNames.Count && i < seatNameTexts.Count; i++)
        {
            if (seatNameTexts[i] != null)
            {
                seatNameTexts[i].text = connectedPlayerNames[i].ToString();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        connectedPlayerNames.OnListChanged -= OnPlayerListChanged;
    }

    private void OnDestroy()
    {
        connectedPlayerNames?.Dispose();
    }
}

