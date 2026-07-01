using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;



public class BoardManager : NetworkBehaviour
{
    //UI
    [Header("Board View")]
    [SerializeField] private Transform boardRoot;
    [SerializeField] private CardView cardPrefab;
    [SerializeField] private float cellSize = 120f;

    [Header("UI Settings")]
    [SerializeField] private PlayerDisplay playerEntryPrefab; 
    [SerializeField] private Transform playerListParent;

    [Header("Deck Settings")]
    [SerializeField, Min(1)] private int initialHandSize = 6;
    [SerializeField, Min(1)] private int copiesPerCardType = 10;

    private NetworkList<ulong> connectedPlayers;
    private NetworkList<CardState> placedCards;
    private NetworkList<PlayerInfo> players;
    private NetworkList<DealtCard> dealtCards;
    private readonly Dictionary<Vector2Int, CardView> spawnedCards = new Dictionary<Vector2Int, CardView>();
    private readonly List<PlayerDisplay> spawnedPlayerDisplays = new List<PlayerDisplay>();
    private readonly List<CardView> spawnedHandCards = new List<CardView>();
    private readonly List<CardType> deck = new List<CardType>();
    private Transform handRoot;
    private bool playerListPrepared;

    //初期化
    private void Awake() 
    {
        connectedPlayers = new NetworkList<ulong>();
        placedCards = new NetworkList<CardState>();
        players = new NetworkList<PlayerInfo>();
        dealtCards = new NetworkList<DealtCard>();
   
    }

    //ネットワーク処理
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 接続されている全員をリストに追加
            foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
            {
                connectedPlayers.Add(client);
            }
        }
        placedCards.OnListChanged += OnPlacedCardsChanged;
        players.OnListChanged += OnPlayersChanged;
        dealtCards.OnListChanged += OnDealtCardsChanged;

        if (IsServer && placedCards.Count == 0)
        {
            placedCards.Add(new CardState(0, 0, CardType.Start, false, NetworkManager.ServerClientId));
            BuildAndShuffleDeck();
        }

        StartCoroutine(RegisterLocalPlayerWhenReady());
        RebuildBoardView();
        RefreshPlayerList();
        RefreshLocalHand();
    }

    private IEnumerator RegisterLocalPlayerWhenReady()
    {
        while (IsSpawned && NetworkManager.Singleton != null)
        {
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            bool hasFinalName = false;

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].clientId == localClientId)
                {
                    string registeredName = players[i].playerName.ToString();
                    hasFinalName = !IsPlaceholderName(registeredName);
                    break;
                }
            }

            if (hasFinalName)
            {
                yield break;
            }

            string playerName = GetLocalPlayerName(localClientId);

            RegisterPlayerServerRpc(playerName);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private string GetLocalPlayerName(ulong localClientId)
    {
        if (NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent(
                out PlayerNetworkData playerData))
        {
            string networkName = playerData.PlayerInfoVariable.Value.playerName.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(networkName) &&
                networkName != "Guest" &&
                networkName != "Player")
            {
                return networkName;
            }
        }

        string savedName = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.SavedPlayerName.Trim()
            : string.Empty;

        return string.IsNullOrWhiteSpace(savedName)
            ? $"Player {localClientId}"
            : savedName;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterPlayerServerRpc(
        FixedString64Bytes playerName,
        ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string safeName = playerName.ToString().Trim();

        if (RelayManager.TryGetPlayerName(clientId, out string approvedName) &&
            !string.IsNullOrWhiteSpace(approvedName))
        {
            safeName = approvedName.Trim();
        }

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = $"Player {clientId}";
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId != clientId)
            {
                continue;
            }

            if (players[i].playerName.ToString() != safeName)
            {
                players[i] = new PlayerInfo(clientId, safeName);
                Debug.Log($"Player name updated: {safeName} ({clientId})");
            }
            return;
        }

        players.Add(new PlayerInfo(clientId, safeName));
        DealInitialHand(clientId);
        Debug.Log($"Player registered: {safeName} ({clientId})");
    }

    private static bool IsPlaceholderName(string playerName)
    {
        return string.IsNullOrWhiteSpace(playerName) ||
               playerName == "Player" ||
               playerName == "Guest" ||
               playerName.StartsWith("Player ");
    }

    private void BuildAndShuffleDeck()
    {
        deck.Clear();

        CardType[] drawableTypes =
        {
           CardType.PathStraight,
            CardType.PathCorner,
            CardType.PathTJunction,
            CardType.PathCross,
            CardType.DeadEnd,
            CardType.ActionRepair,
            CardType.ActionSabotage,
            CardType.ActionMap
        };

        // Start以外のカードを山札へ追加する。
        foreach (CardType cardType in drawableTypes)
        {
            for (int i = 0; i < copiesPerCardType; i++)
            {
                deck.Add(cardType);
            }
        }
    }

    private void DealInitialHand(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        int currentCardCount = 0;
        for (int i = 0; i < dealtCards.Count; i++)
        {
            if (dealtCards[i].ownerClientId == clientId)
            {
                currentCardCount++;
            }
        }

        while (currentCardCount < initialHandSize && deck.Count > 0)
        {
            int randomIndex = Random.Range(0, deck.Count);
            CardType cardType = deck[randomIndex];
            deck.RemoveAt(randomIndex);
            dealtCards.Add(new DealtCard(clientId, cardType));
            currentCardCount++;
        }

        Debug.Log($"Dealt {currentCardCount} random cards to client {clientId}. Deck remaining: {deck.Count}");
    }

    private void OnPlayersChanged(NetworkListEvent<PlayerInfo> changeEvent)
    {
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        if (playerListParent == null || playerEntryPrefab == null)
        {
            return;
        }

        if (!playerListPrepared)
        {
            for (int i = 0; i < playerListParent.childCount; i++)
            {
                Transform child = playerListParent.GetChild(i);
                if (child.GetComponent<PlayerDisplay>() == null)
                {
                    child.gameObject.SetActive(false);
                }
            }

            playerListPrepared = true;
        }

        foreach (PlayerDisplay display in spawnedPlayerDisplays)
        {
            if (display != null)
            {
                Destroy(display.gameObject);
            }
        }
        spawnedPlayerDisplays.Clear();

        playerEntryPrefab.gameObject.SetActive(players.Count > 0);

        for (int i = 0; i < players.Count; i++)
        {
            PlayerDisplay display = i == 0
                ? playerEntryPrefab
                : Instantiate(playerEntryPrefab, playerListParent);

            display.UpdateName(players[i].playerName.ToString());

            RectTransform displayRect = display.GetComponent<RectTransform>();
            if (displayRect != null)
            {
                displayRect.anchorMin = new Vector2(1f, 1f);
                displayRect.anchorMax = new Vector2(1f, 1f);
                displayRect.pivot = new Vector2(1f, 1f);
                displayRect.anchoredPosition = new Vector2(-24f, -24f - (i * 60f));
            }

            if (i > 0)
            {
                spawnedPlayerDisplays.Add(display);
            }
        }
    }

    private void OnDealtCardsChanged(NetworkListEvent<DealtCard> changeEvent)
    {
        RefreshLocalHand();
    }

    private void RefreshLocalHand()
    {
        if (cardPrefab == null || NetworkManager.Singleton == null)
        {
            return;
        }

        EnsureHandRoot();
        if (handRoot == null)
        {
            return;
        }

        foreach (CardView card in spawnedHandCards)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }
        spawnedHandCards.Clear();

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < dealtCards.Count; i++)
        {
            if (dealtCards[i].ownerClientId != localClientId)
            {
                continue;
            }

            CardView card = Instantiate(cardPrefab, handRoot);
            card.SetCard(dealtCards[i].cardType);
            spawnedHandCards.Add(card);
        }
    }

    private void EnsureHandRoot()
    {
        if (handRoot != null)
        {
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject rootObject = new GameObject(
            "LocalHand",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup));
        rootObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = rootObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(720f, 130f);

        HorizontalLayoutGroup layout = rootObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        handRoot = rect;
    }

    //カード配置
    public void TryPlaceCardFromUI(int x, int y)
    {
        RequestPlaceCardServerRpc(x, y, CardType.PathStraight, false);
    }

    public void TryPlaceCardFromUI(int x, int y, CardType cardType, bool rotated)
    {
        RequestPlaceCardServerRpc(x, y, cardType, rotated);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceCardServerRpc(int x, int y, CardType cardType, bool rotated, ServerRpcParams rpcParams = default)
    {
        Vector2Int position = new Vector2Int(x, y);

        if (!CanPlaceCard(position, cardType, rotated))
        {
            return;
        }

        placedCards.Add(new CardState(
            x,
            y,
            cardType,
            rotated,
            rpcParams.Receive.SenderClientId));
    }

    private bool CanPlaceCard(Vector2Int position, CardType cardType, bool rotated)
    {
       
        if (HasCardAt(position))
        {
            return false;
        }

      
        bool hasNeighbor = HasCardAt(position + Vector2Int.up)
                        || HasCardAt(position + Vector2Int.down)
                        || HasCardAt(position + Vector2Int.left)
                        || HasCardAt(position + Vector2Int.right);

        if (!hasNeighbor) return false;

        return CardRules.CanPlaceCard(position, cardType, rotated, placedCards);
    }

    private bool HasCardAt(Vector2Int position)
    {
        for (int i = 0; i < placedCards.Count; i++)
        {
            if (placedCards[i].x == position.x && placedCards[i].y == position.y)
            {
                return true;
            }
        }

        return false;
    }

    private void OnPlacedCardsChanged(NetworkListEvent<CardState> changeEvent)
    {
        if (changeEvent.Type == NetworkListEvent<CardState>.EventType.Add)
        {
            SpawnCardView(changeEvent.Value);
            return;
        }

        RebuildBoardView();
    }

    private void RebuildBoardView()
    {
        foreach (CardView cardView in spawnedCards.Values)
        {
            if (cardView != null)
            {
                Destroy(cardView.gameObject);
            }
        }

        spawnedCards.Clear();

        for (int i = 0; i < placedCards.Count; i++)
        {
            SpawnCardView(placedCards[i]);
        }
    }

    private void SpawnCardView(CardState state)
    {
        if (boardRoot == null || cardPrefab == null)
        {
            return;
        }

        Vector2Int position = new Vector2Int(state.x, state.y);
        if (spawnedCards.ContainsKey(position))
        {
            return;
        }

        CardView cardView = Instantiate(cardPrefab, boardRoot);
        RectTransform rectTransform = cardView.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(state.x * cellSize, state.y * cellSize);
        }
        else
        {
            cardView.transform.localPosition = new Vector3(state.x * cellSize, state.y * cellSize, 0f);
        }

        cardView.SetCard(state.cardType);
        spawnedCards.Add(position, cardView);
    }

    public override void OnNetworkDespawn()
    {
        placedCards.OnListChanged -= OnPlacedCardsChanged;
        players.OnListChanged -= OnPlayersChanged;
        dealtCards.OnListChanged -= OnDealtCardsChanged;
    }

    private void OnDestroy()
    {
        connectedPlayers?.Dispose();
        placedCards?.Dispose();
        players?.Dispose();
        dealtCards?.Dispose();
    }
}
