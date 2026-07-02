using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class CardGameManager : NetworkBehaviour
{
    [SerializeField] private TMP_Text statusText;

    //player名のUI表示用のTextMeshProUGUIのリスト
    [Header("UI Slots (0 = Main Player, 1~3 = Other Players)")]
    [SerializeField] private List<TMP_Text> seatNameTexts = new List<TMP_Text>();

    private NetworkList<FixedString64Bytes> connectedPlayerNames;

    private List<CardType> deck = new List<CardType>();

    // 全playerに同期する手札リスト
    public NetworkList<DealtCard> dealtCards;

    //初期化処理
    private void Awake()
    {
        connectedPlayerNames = new NetworkList<FixedString64Bytes>();
        dealtCards = new NetworkList<DealtCard>();

    }

    //ネットワークがスポーンしたときに呼ばれる処理
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
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
        dealtCards.OnListChanged += OnDealtCardsChanged;
        RefreshSeatUI();

        foreach (var card in dealtCards)
        {
            if (card.ownerClientId == NetworkManager.Singleton.LocalClientId)
            {
                CardUIHandler.Instance?.AddCardToHand(card.cardType);
            }
        }
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

    public void OnClick_StartGame()
    {
        Debug.Log("【デバッグ】ボタンが押されました！");
        StartGameServerRpc();
    }

   

    // サーバー側で呼び出すゲーム開始処理
    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void StartGameServerRpc()
    {
        Debug.Log("【デバッグ】StartGameServerRpc が受信されました！IsServer: " + IsServer);
        if (!IsServer) return;

        // 以下、処理...
        Debug.Log("【デバッグ】処理を開始します");
        // デッキの初期化
        deck.Clear();

        //CardType.cs
        // デッキにカードを追加
        for (int i = 0; i < 10; i++) deck.Add(CardType.PathStraight);
        for (int i = 0; i < 5; i++) deck.Add(CardType.PathCorner);
        for (int i = 0; i < 5; i++) deck.Add(CardType.PathTJunction);
        for (int i = 0; i < 5; i++) deck.Add(CardType.PathCross);
        for (int i = 0; i < 5; i++) deck.Add(CardType.DeadEnd);
        for (int i = 0; i < 5; i++) deck.Add(CardType.ActionRepair);
        for (int i = 0; i < 5; i++) deck.Add(CardType.ActionSabotage);
        for (int i = 0; i < 5; i++) deck.Add(CardType.ActionMap);
        for (int i = 0; i < 5; i++) deck.Add(CardType.ActionFallingRocks);

        // シャッフル
        for (int i = 0; i < deck.Count; i++)
        {
            CardType temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }

        //各プレイヤーに6枚ずつ配る
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            for (int i = 0; i < 6; i++)
            {
                if (deck.Count > 0)
                {
                    CardType drawnCard = deck[0];
                    deck.RemoveAt(0);
                    dealtCards.Add(new DealtCard(clientId, drawnCard));
                }
            }
        }
    }

    private void OnDealtCardsChanged(NetworkListEvent<DealtCard> changeEvent)
    {
        // 追加された時のみ処理
        if (changeEvent.Type == NetworkListEvent<DealtCard>.EventType.Add)
        {
            // 自分宛てのカードならUIに追加
            if (changeEvent.Value.ownerClientId == NetworkManager.Singleton.LocalClientId)
            {
                // シングルトン経由でUI生成を呼び出す
                if (CardUIHandler.Instance != null)
                {
                    CardUIHandler.Instance.AddCardToHand(changeEvent.Value.cardType);
                }
                else
                {
                    Debug.LogError("CardUIHandler.Instance is null! シーンに設置されていますか？");
                }
            }
        }
    }

    public class CardUIHandler : MonoBehaviour
    {
        public static CardUIHandler Instance;
        [SerializeField] private GameObject cardPrefab; // カードの見た目を持つプレハブ
        [SerializeField] private Transform handLayoutGroup; // 手札を並べる親オブジェクト（HorizontalLayoutGroupなど）

        private void Awake() => Instance = this;

        public void AddCardToHand(CardType type)
        {
            // プレハブからカードUIを生成
            GameObject cardObj = Instantiate(cardPrefab, handLayoutGroup);
            // 生成したカードに種類をセット（画像を変更するなど）
            CardState state = new CardState(0, 0, type, false, NetworkManager.Singleton.LocalClientId);
            cardObj.GetComponent<CardView>().SetCard(type);
        }
    }

    private void OnDestroy()
    {
        connectedPlayerNames?.Dispose();
    }
}

