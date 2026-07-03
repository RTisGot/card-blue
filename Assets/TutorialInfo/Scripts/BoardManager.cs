using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;



public class BoardManager : NetworkBehaviour
{
    private const int StartCardX = 1;
    private const int StartCardY = 3;

    // UI
    [Header("Board View")]
    [SerializeField] private Transform boardRoot;
    [SerializeField] private CardView cardPrefab;
    [SerializeField] private float cellSize = 120f;

    [Header("UI Settings")]
    [SerializeField] private PlayerDisplay playerEntryPrefab; 
    [SerializeField] private Transform playerListParent;
    [SerializeField] private Canvas mainCanvas;

    [Header("Deck Settings")]
    [SerializeField, Min(1)] private int initialHandSize = 6;
    [SerializeField, Min(1)] private int copiesPerCardType = 10;

    [Header("Turn UI")]
    [SerializeField] private TMP_Text turnText;

    // Deck composition entry for the inspector.
    [System.Serializable]
    public struct CardDistribution
    {
        public CardType cardType;
        public int count;
    }

    private NetworkList<ulong> connectedPlayers;
    private NetworkList<CardState> placedCards;
    private NetworkList<PlayerInfo> players;
    private NetworkList<DealtCard> dealtCards;
    private readonly Dictionary<Vector2Int, CardView> spawnedCards = new Dictionary<Vector2Int, CardView>();
    private readonly List<PlayerDisplay> spawnedPlayerDisplays = new List<PlayerDisplay>();
    private readonly List<CardView> spawnedHandCards = new List<CardView>();
    private readonly List<CardType> deck = new List<CardType>();
    private readonly NetworkVariable<int> currentPlayerIndex = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> roundNumber = new NetworkVariable<int>(1);
    public List<CardDistribution> deckComposition;
    private Transform handRoot;
    private bool playerListPrepared;
    public static BoardManager Instance;

    // Initialize network-backed state.
    private void Awake() 
    {
        Instance = this;
        connectedPlayers = new NetworkList<ulong>();
        placedCards = new NetworkList<CardState>();
        players = new NetworkList<PlayerInfo>();
        dealtCards = new NetworkList<DealtCard>();
   
    }

    // Network setup.
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
            {
                connectedPlayers.Add(client);
            }
        }
        placedCards.OnListChanged += OnPlacedCardsChanged;
        players.OnListChanged += OnPlayersChanged;
        dealtCards.OnListChanged += OnDealtCardsChanged;
        currentPlayerIndex.OnValueChanged += OnTurnChanged;
        roundNumber.OnValueChanged += OnTurnChanged;

        if (IsServer && placedCards.Count == 0)
        {
            placedCards.Add(new CardState(StartCardX, StartCardY, CardType.Start, false, NetworkManager.ServerClientId));
            BuildAndShuffleDeck();
        }

        if (IsServer)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                RegisterOrUpdatePlayer(clientId, $"Player {clientId}");
            }
        }

        StartCoroutine(RegisterLocalPlayerWhenReady());
        RebuildBoardView();
        RefreshPlayerList();
        RefreshLocalHand();
        RefreshTurnUI();
    }

    // Keep trying until the local player has a final display name.
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
        RegisterOrUpdatePlayer(clientId, playerName.ToString());
    }

   
    private void RegisterOrUpdatePlayer(ulong clientId, string requestedName)
    {
        if (!IsServer)
        {
            return;
        }

        string safeName = requestedName.Trim();

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

            DealInitialHand(clientId);
            RefreshTurnAfterPlayerChange();
            return;
        }

        players.Add(new PlayerInfo(clientId, safeName));
        DealInitialHand(clientId);
        RefreshTurnAfterPlayerChange();
        Debug.Log($"Player registered: {safeName} ({clientId})");
    }

    private void RefreshTurnAfterPlayerChange()
    {
        if (!IsServer || players.Count == 0)
        {
            return;
        }

        if (currentPlayerIndex.Value >= players.Count)
        {
            currentPlayerIndex.Value = 0;
        }
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


        AddCardsToDeck(CardType.LRdeadend,1);
        AddCardsToDeck(CardType.LDdeadend,1);
        AddCardsToDeck(CardType.UDLRdeadend,1);
        AddCardsToDeck(CardType.UDLdeadend,1);
        AddCardsToDeck(CardType.RDdeadend,1);
        AddCardsToDeck(CardType.Ldeadend,1);
        AddCardsToDeck(CardType.Udeadend,1);
        AddCardsToDeck(CardType.ULRdeadend,1);
        AddCardsToDeck(CardType.UDLload,2);
        AddCardsToDeck(CardType.DLRload,2);
        AddCardsToDeck(CardType.ULRload,4);
        AddCardsToDeck(CardType.LRload,3);
        AddCardsToDeck(CardType.UDLRload,4);
        AddCardsToDeck(CardType.RDload,2);
        AddCardsToDeck(CardType.Lanternrepaire,2);
        AddCardsToDeck(CardType.Lanternban,3);
        AddCardsToDeck(CardType.Pickaxerepaire,2);
        AddCardsToDeck(CardType.Pickaxeban,3);
        AddCardsToDeck(CardType.Railcarrepaire,2);
        AddCardsToDeck(CardType.Railcarban,3);
        AddCardsToDeck(CardType.Treasuremap,6);
        AddCardsToDeck(CardType.Fallingrocks,3);
        
    }

    // Add cards to the draw deck.
    private void AddCardsToDeck(CardType type, int count)
    {
        for (int i = 0; i < count; i++)
        {
            deck.Add(type);
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
        RefreshTurnUI();
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

            display.UpdateName(players[i].playerName.ToString(), IsCurrentTurnIndex(i));

            RectTransform displayRect = display.GetComponent<RectTransform>();
            if (displayRect != null)
            {
                displayRect.anchorMin = new Vector2(1f, 1f);
                displayRect.anchorMax = new Vector2(1f, 1f);
                displayRect.pivot = new Vector2(1f, 1f);
                displayRect.anchoredPosition = new Vector2(-24f, -164f - (i * 60f));
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
        bool isLocalTurn = IsLocalPlayerTurn();
        int visibleCardCount = 0;
        for (int i = 0; i < dealtCards.Count; i++)
        {
            if (dealtCards[i].ownerClientId != localClientId)
            {
                continue;
            }

            CardView card = Instantiate(cardPrefab, handRoot);
            card.gameObject.SetActive(true);

            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.localScale = Vector3.one;
                cardRect.sizeDelta = new Vector2(100f, 140f);
            }

            LayoutElement layoutElement = card.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = card.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredWidth = 100f;
            layoutElement.preferredHeight = 140f;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;

            card.SetCard(dealtCards[i].cardType);
            SetCardInteractivity(card, isLocalTurn);
            spawnedHandCards.Add(card);
            visibleCardCount++;
        }

        Debug.Log($"Local hand refreshed: client {localClientId}, cards {visibleCardCount}");
    }

    public GameObject handRootPrefab;
    private void EnsureHandRoot()
    {
        if (handRoot != null) return;

        GameObject rootObject = Instantiate(handRootPrefab);
        rootObject.name = "LocalHand";

        // Parent the generated hand to the main Canvas.
        rootObject.transform.SetParent(mainCanvas.transform, false);

        handRoot = rootObject.GetComponent<RectTransform>();
        Debug.Log("Local hand UI instantiated and parented to Canvas");
    }

    // Card placement.
    public void TryPlaceCardFromUI(int x, int y)
    {
        RequestPlaceCardServerRpc(x, y, CardType.PathStraight, false);
    }

    public void TryPlaceCardFromUI(int x, int y, CardType cardType, bool rotated)
    {
        RequestPlaceCardServerRpc(x, y, cardType, rotated);
    }

    public bool CanPlaceCardFromUI(int x, int y, CardType cardType, bool rotated)
    {
        return IsLocalPlayerTurn()
            && IsTerrainCard(cardType)
            && CanPlaceCard(new Vector2Int(x, y), cardType, rotated);
    }

    public void ShowPlacementHighlights(CardType cardType, bool rotated)
    {
        foreach (CellComponent cell in FindObjectsOfType<CellComponent>())
        {
            bool canPlace = CanPlaceCardFromUI(cell.x, cell.y, cardType, rotated);
            cell.SetPlacementHighlight(canPlace);
        }
    }

    public void ClearPlacementHighlights()
    {
        foreach (CellComponent cell in FindObjectsOfType<CellComponent>())
        {
            cell.SetPlacementHighlight(false);
        }
    }

    public void TryPlayActionCardFromUI(CardType cardType)
    {
        RequestPlayActionCardServerRpc(cardType);
    }

    public void TryDiscardAndDrawFromUI(CardType cardType)
    {
        RequestDiscardAndDrawServerRpc(cardType);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceCardServerRpc(int x, int y, CardType cardType, bool rotated, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!CanAct(senderClientId) || !IsTerrainCard(cardType) || !HasCardInHand(senderClientId, cardType))
        {
            return;
        }

        Vector2Int position = new Vector2Int(x, y);

        if (!CanPlaceCard(position, cardType, rotated))
        {
            return;
        }

        RemoveCardFromHand(senderClientId, cardType);
        placedCards.Add(new CardState(
            x,
            y,
            cardType,
            rotated,
            senderClientId));
        DrawCard(senderClientId);
        AdvanceTurn();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayActionCardServerRpc(CardType cardType, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!CanAct(senderClientId) || !IsActionCard(cardType) || !HasCardInHand(senderClientId, cardType))
        {
            return;
        }

        RemoveCardFromHand(senderClientId, cardType);
        DrawCard(senderClientId);
        AdvanceTurn();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDiscardAndDrawServerRpc(CardType cardType, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!CanAct(senderClientId) || !HasCardInHand(senderClientId, cardType))
        {
            return;
        }

        RemoveCardFromHand(senderClientId, cardType);
        DrawCard(senderClientId);
        AdvanceTurn();
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

        return CardRules.CanPlaceCard(position, cardType, rotated, placedCards)
            && ConnectsToStart(position, cardType, rotated);
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

    private bool ConnectsToStart(Vector2Int position, CardType cardType, bool rotated)
    {
        foreach (Vector2Int direction in GetCardDirections())
        {
            Vector2Int neighborPosition = position + direction;
            if (!TryGetPlacedCard(neighborPosition, out CardState neighbor))
            {
                continue;
            }

            if (!HasRoadConnection(cardType, rotated, direction, neighbor))
            {
                continue;
            }

            if (neighbor.cardType == CardType.Start || ExistingCardConnectsToStart(neighborPosition))
            {
                return true;
            }
        }

        return false;
    }

    private bool ExistingCardConnectsToStart(Vector2Int startPosition)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(startPosition);
        visited.Add(startPosition);

        while (queue.Count > 0)
        {
            Vector2Int currentPosition = queue.Dequeue();
            if (!TryGetPlacedCard(currentPosition, out CardState currentCard))
            {
                continue;
            }

            if (currentCard.cardType == CardType.Start)
            {
                return true;
            }

            foreach (Vector2Int direction in GetCardDirections())
            {
                Vector2Int nextPosition = currentPosition + direction;
                if (visited.Contains(nextPosition) || !TryGetPlacedCard(nextPosition, out CardState nextCard))
                {
                    continue;
                }

                if (!HasRoadConnection(currentCard.cardType, currentCard.rotated, direction, nextCard))
                {
                    continue;
                }

                visited.Add(nextPosition);
                queue.Enqueue(nextPosition);
            }
        }

        return false;
    }

    private bool TryGetPlacedCard(Vector2Int position, out CardState cardState)
    {
        for (int i = 0; i < placedCards.Count; i++)
        {
            if (placedCards[i].x == position.x && placedCards[i].y == position.y)
            {
                cardState = placedCards[i];
                return true;
            }
        }

        cardState = default;
        return false;
    }

    private static bool HasRoadConnection(CardType cardType, bool rotated, Vector2Int direction, CardState neighbor)
    {
        PathDirection cardPaths = CardRules.GetRotatedPaths(cardType, rotated);
        PathDirection neighborPaths = CardRules.GetRotatedPaths(neighbor.cardType, neighbor.rotated);

        PathDirection cardDirection = GetPathDirection(direction);
        PathDirection neighborDirection = GetOppositePathDirection(direction);

        return (cardPaths & cardDirection) != 0 && (neighborPaths & neighborDirection) != 0;
    }

    private static PathDirection GetPathDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return PathDirection.Up;
        if (direction == Vector2Int.down) return PathDirection.Down;
        if (direction == Vector2Int.left) return PathDirection.Left;
        return PathDirection.Right;
    }

    private static PathDirection GetOppositePathDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return PathDirection.Down;
        if (direction == Vector2Int.down) return PathDirection.Up;
        if (direction == Vector2Int.left) return PathDirection.Right;
        return PathDirection.Left;
    }

    private static Vector2Int[] GetCardDirections()
    {
        return new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    }

    private bool CanAct(ulong clientId)
    {
        return IsServer && players.Count > 0 && players[currentPlayerIndex.Value].clientId == clientId;
    }

    private bool HasCardInHand(ulong clientId, CardType cardType)
    {
        for (int i = 0; i < dealtCards.Count; i++)
        {
            if (dealtCards[i].ownerClientId == clientId && dealtCards[i].cardType == cardType)
            {
                return true;
            }
        }

        return false;
    }

    private bool RemoveCardFromHand(ulong clientId, CardType cardType)
    {
        for (int i = 0; i < dealtCards.Count; i++)
        {
            if (dealtCards[i].ownerClientId == clientId && dealtCards[i].cardType == cardType)
            {
                dealtCards.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private void DrawCard(ulong clientId)
    {
        if (deck.Count == 0)
        {
            return;
        }

        int randomIndex = Random.Range(0, deck.Count);
        CardType cardType = deck[randomIndex];
        deck.RemoveAt(randomIndex);
        dealtCards.Add(new DealtCard(clientId, cardType));
    }

    private void AdvanceTurn()
    {
        if (!IsServer || players.Count == 0)
        {
            return;
        }

        int nextPlayerIndex = currentPlayerIndex.Value + 1;
        if (nextPlayerIndex >= players.Count)
        {
            nextPlayerIndex = 0;
            roundNumber.Value++;
        }

        currentPlayerIndex.Value = nextPlayerIndex;
    }

    private void OnTurnChanged(int previousValue, int newValue)
    {
        RefreshPlayerList();
        RefreshLocalHand();
        RefreshTurnUI();
    }


    private void RefreshTurnUI()
    {
        EnsureTurnText();

        if (turnText == null)
        {
            return;
        }

        if (players.Count == 0)
        {
            turnText.text = "\u30e9\u30a6\u30f3\u30c9 1\n\u5f85\u6a5f\u4e2d";
            return;
        }

        int safeIndex = Mathf.Clamp(currentPlayerIndex.Value, 0, players.Count - 1);
        string currentName = players[safeIndex].playerName.ToString();
        string localTurnLine = NetworkManager.Singleton != null &&
                               players[safeIndex].clientId == NetworkManager.Singleton.LocalClientId
            ? "\u3042\u306a\u305f\u306e\u756a\u3067\u3059"
            : "\u5f85\u6a5f\u4e2d";

        turnText.text = $"\u30e9\u30a6\u30f3\u30c9 {roundNumber.Value}\n{currentName} \u306e\u30bf\u30fc\u30f3\n{localTurnLine}";
    }

    private void EnsureTurnText()
    {
        if (turnText != null || mainCanvas == null)
        {
            return;
        }

        GameObject turnObject = new GameObject("RoundTurnText", typeof(RectTransform), typeof(TextMeshProUGUI));
        turnObject.transform.SetParent(mainCanvas.transform, false);

        RectTransform rectTransform = turnObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = new Vector2(-24f, -24f);
        rectTransform.sizeDelta = new Vector2(360f, 120f);

        turnText = turnObject.GetComponent<TextMeshProUGUI>();
        turnText.alignment = TextAlignmentOptions.TopRight;
        turnText.fontSize = 28f;
        turnText.fontStyle = FontStyles.Bold;
        turnText.color = Color.white;
        turnText.raycastTarget = false;
        turnText.enableWordWrapping = false;
    }

    private bool IsCurrentTurnIndex(int playerIndex)
    {
        return players.Count > 0 && playerIndex == Mathf.Clamp(currentPlayerIndex.Value, 0, players.Count - 1);
    }

    public bool IsLocalPlayerTurn()
    {
        if (NetworkManager.Singleton == null || players.Count == 0)
        {
            return false;
        }

        int safeIndex = Mathf.Clamp(currentPlayerIndex.Value, 0, players.Count - 1);
        return players[safeIndex].clientId == NetworkManager.Singleton.LocalClientId;
    }

    private void SetCardInteractivity(CardView card, bool isInteractable)
    {
        CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = card.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = isInteractable ? 1f : 0.55f;
        canvasGroup.interactable = isInteractable;
        canvasGroup.blocksRaycasts = isInteractable;
    }

    public static bool IsActionCard(CardType cardType)
    {
        switch (cardType)
        {
            case CardType.ActionRepair:
            case CardType.ActionSabotage:
            case CardType.ActionMap:
            case CardType.ActionFallingRocks:
            case CardType.Lanternrepaire:
            case CardType.Lanternban:
            case CardType.Pickaxerepaire:
            case CardType.Pickaxeban:
            case CardType.Railcarrepaire:
            case CardType.Railcarban:
            case CardType.Treasuremap:
            case CardType.Fallingrocks:
                return true;
            default:
                return false;
        }
    }

    private static bool IsTerrainCard(CardType cardType)
    {
        return cardType != CardType.Start && !IsActionCard(cardType);
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
        currentPlayerIndex.OnValueChanged -= OnTurnChanged;
        roundNumber.OnValueChanged -= OnTurnChanged;
    }

    private void OnDestroy()
    {
        connectedPlayers?.Dispose();
        placedCards?.Dispose();
        players?.Dispose();
        dealtCards?.Dispose();
    }
}
