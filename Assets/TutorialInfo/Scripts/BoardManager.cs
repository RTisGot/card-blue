using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゲームの盤面、プレイヤーの手札、ターン管理、ネットワーク同期を司るメインコントローラー。
/// サーバー・クライアント間の状態の一貫性を維持する役割を持ちます。
/// </summary>
public class BoardManager : NetworkBehaviour
{
    [Header("Start Settings")]
    [SerializeField] private Transform startCardRoot;  // Startカード専用の表示場所
    [SerializeField] private int startCardX = 1;
    [SerializeField] private int startCardY = 3;

    [Header("Goal Settings")]
    [SerializeField] private Transform[] goalCardRoots = new Transform[3];//Goalカードを配列に
    [SerializeField] private Transform goalCardRoot;   // Goalカード専用の表示場所
    [SerializeField] private int goalCardX = 8;
    [SerializeField] private int goalCardY = 3;
    [SerializeField, Min(1)] private int goalVerticalSpacing = 2;

    [Header("Board View")]
    [SerializeField] private Transform boardRoot;        // 盤面上のカードを配置する親コンテナ
    [SerializeField] private CardView cardPrefab;        // 生成するカードのプレハブ
    [SerializeField] private float cellSize = 120f;      // グリッドの間隔

    [Header("UI Settings")]
    [SerializeField] private PlayerDisplay playerEntryPrefab;
    [SerializeField] private Transform playerListParent;
    [SerializeField] private Canvas mainCanvas;

    [Header("Deck Settings")]
    [SerializeField, Min(1)] private int initialHandSize = 6;
    [SerializeField, Min(1)] private int copiesPerCardType = 10;

    [Header("Turn UI")]
    [SerializeField] private TMP_Text turnText;          // 現在のラウンド・ターンを表示するUI

    [Header("Turn UI Messages")]
    [SerializeField] private string waitingText = "待機中";
    [SerializeField] private string myTurnText = "あなたの番です";
    [SerializeField] private string roundFormat = "ラウンド {0}";
    [SerializeField] private string turnFormat = "{0} のターン";

    [Header("Layout Settings")]
    [SerializeField] private RectTransform turnTextRect; // turnTextのRectTransformをアサイン
    [SerializeField] private Transform myTurnPosition;    // 自分の番の時の位置
    [SerializeField] private Transform waitingPosition;   // 待機中の位置

    [System.Serializable]
    public struct CardDistribution
    {
        public CardType cardType;
        public int count;
    }

    // --- ネットワーク同期データ ---
    private NetworkList<ulong> connectedPlayers;
    private NetworkList<CardState> placedCards;          // 全プレイヤーで共有する盤面のカード状態
    private NetworkList<PlayerInfo> players;             // 参加プレイヤーのリスト
    private NetworkList<DealtCard> dealtCards;           // 手札の同期用リスト（所有者情報を含む）

    // --- ローカル状態管理 ---
    private readonly Dictionary<Vector2Int, CardView> spawnedCards = new Dictionary<Vector2Int, CardView>();
    private readonly List<PlayerDisplay> spawnedPlayerDisplays = new List<PlayerDisplay>();
    private readonly List<CardView> spawnedHandCards = new List<CardView>();
    private readonly List<CellComponent> cachedBoardCells = new List<CellComponent>();
    private readonly List<CardType> deck = new List<CardType>(); // サーバーのみが保持する山札リスト

    // --- 同期変数 ---
    private readonly NetworkVariable<int> currentPlayerIndex = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> roundNumber = new NetworkVariable<int>(1);
    private readonly NetworkVariable<bool> gameEnded = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<ulong> winningClientId = new NetworkVariable<ulong>(ulong.MaxValue);
    private IBoardConnectivityService connectivityService;
    private GoalObjectiveService goalObjectiveService;
    private ICardDeckBuilder deckBuilder;
    private ActionTargetPolicy actionTargetPolicy;

    public List<CardDistribution> deckComposition;
    private Transform handRoot;
    private bool playerListPrepared;
    private bool placementHighlightsVisible;
    private CardType highlightedCardType;
    private bool highlightedCardRotated;
    public static BoardManager Instance;





    private void Awake()
    {
        Instance = this;
        // NetworkListの初期化
        connectedPlayers = new NetworkList<ulong>();
        placedCards = new NetworkList<CardState>();
        players = new NetworkList<PlayerInfo>();
        dealtCards = new NetworkList<DealtCard>();
        connectivityService = new BoardConnectivityService();
        goalObjectiveService = new GoalObjectiveService();
        deckBuilder = new DefaultCardDeckBuilder();
        actionTargetPolicy = new ActionTargetPolicy();
    }

    /// <summary>
    /// ネットワーク接続が確立された後に実行される初期化処理。
    /// 各クライアントでの状態同期やイベント購読。
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
            {
                connectedPlayers.Add(client);
            }
        }

        // 状態変更時のコールバック登録
        placedCards.OnListChanged += OnPlacedCardsChanged;
        players.OnListChanged += OnPlayersChanged;
        dealtCards.OnListChanged += OnDealtCardsChanged;
        currentPlayerIndex.OnValueChanged += OnTurnChanged;
        roundNumber.OnValueChanged += OnTurnChanged;
        gameEnded.OnValueChanged += OnGameEndedChanged;

        // サーバーのみ：盤面の初期カードと山札の生成
        if (IsServer && placedCards.Count == 0)
        {
            placedCards.Add(new CardState(startCardX, startCardY, CardType.Start, false, NetworkManager.ServerClientId, false, false, false));

            int goldGoalIndex = UnityEngine.Random.Range(0, 3);
            Vector2Int[] goalPositions = goalObjectiveService.Initialize(
                goalCardX,
                goalCardY,
                goalVerticalSpacing,
                goldGoalIndex);

            for (int i = 0; i < goalPositions.Length; i++)
            {
                Vector2Int goalPosition = goalPositions[i];
                placedCards.Add(new CardState(goalPosition.x, goalPosition.y, CardType.Goal, false, NetworkManager.ServerClientId, false, false, false));
            }

            Debug.Log($"[Goal] Gold goal selected on server. Index={goldGoalIndex}");
            BuildAndShuffleDeck();
        }

        // 接続済みプレイヤーの登録
        if (IsServer)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                RegisterOrUpdatePlayer(clientId, $"Player {clientId}");
            }
           
        }

        // ビューの初期化
        StartCoroutine(RegisterLocalPlayerWhenReady());
        RebuildBoardView();
        StartCoroutine(RebuildBoardViewAfterCellsReady());
        RefreshPlayerList();
        RefreshLocalHand();
        RefreshTurnUI();
        RefreshPlacementHighlights();
    }

    // --- プレイヤー管理ロジック ---

    /// <summary>
    /// プレイヤー名が解決されるまで待機し、サーバーに登録をリクエストします。
    /// </summary>
    private IEnumerator Register;

    // 自分の名前をサーバーへの登録が完了するまで待機する
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

    //ローカルプレイヤーの名前を取得する処理
    private string GetLocalPlayerName(ulong localClientId)
    {
        if (NetworkManager.Singleton.LocalClient != null &&                  // ローカルクライアント情報が存在する場合
            NetworkManager.Singleton.LocalClient.PlayerObject != null &&     // プレイヤーオブジェクトが存在する場合
            NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent(
                out PlayerNetworkData playerData))                          // PlayerNetworkDataコンポーネントが存在する場合
        {
            string networkName = playerData.PlayerInfoVariable.Value.playerName.ToString().Trim();//ネットワーク経由でプレイヤーの名前を取得変形
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
        deckBuilder.Build(deck);
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
            int randomIndex = UnityEngine.Random.Range(0, deck.Count);
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
        RefreshPlacementHighlights();
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

            TryGetPlayerToolBrokenState(players[i].clientId, out bool isLanternBroken, out bool isPickaxeBroken, out bool isRailcarBroken);
            display.SetPlayer(
                players[i].clientId,
                players[i].playerName.ToString(),
                IsCurrentTurnIndex(i),
                isLanternBroken,
                isPickaxeBroken,
                isRailcarBroken);

            RectTransform displayRect = display.GetComponent<RectTransform>();
            if (displayRect != null)
            {
                displayRect.anchorMin = new Vector2(1f, 1f);
                displayRect.anchorMax = new Vector2(1f, 1f);
                displayRect.pivot = new Vector2(1f, 1f);
                displayRect.sizeDelta = new Vector2(Mathf.Max(displayRect.sizeDelta.x, 300f), 86f);
                displayRect.anchoredPosition = new Vector2(-32f, -164f - (i * 96f));
            }

            if (i > 0)
            {
                spawnedPlayerDisplays.Add(display);
            }
        }
    }

    //手札の状態を更新する
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

        // 全ての描画先をリセット
        ClearContainer(handRoot); // EnsureHandRootで初期化される想定

        // 手札UIリストをクリア
        foreach (CardView card in spawnedHandCards)
        {
            if (card != null) Destroy(card.gameObject);
        }
        spawnedHandCards.Clear();

        //必要な親ルートの確保
        EnsureHandRoot();
        if (handRoot == null) return;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        bool isLocalTurn = IsLocalPlayerTurn();
        int visibleCardCount = 0;

        Debug.Log($"[Client] Refreshing hand. Current dealtCards count: {dealtCards.Count}");

        for (int i = 0; i < dealtCards.Count; i++)
        {
            // 自分以外のカードと、盤面用のStartカードはスキップ
            if (dealtCards[i].ownerClientId != localClientId || dealtCards[i].cardType == CardType.Start)
            {
                continue;
            }

            Transform parentContainer = handRoot;

            // 指定したコンテナを親にして生成
            CardView card = Instantiate(cardPrefab, parentContainer);
            card.gameObject.SetActive(true);

            // UI設定
            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.localScale = Vector3.one;
                cardRect.sizeDelta = new Vector2(100f, 140f);
            }

            LayoutElement layoutElement = card.GetComponent<LayoutElement>()
                                          ?? card.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 100f;
            layoutElement.preferredHeight = 140f;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;

            // カード設定
            card.SetCard(dealtCards[i].cardType, true);

            // 通常の手札エリアにあるカードだけをインタラクティブにする
            bool isInteractable = (parentContainer == handRoot) && isLocalTurn;
            SetCardInteractivity(card, isInteractable);

            spawnedHandCards.Add(card);
            visibleCardCount++;
        }

        Debug.Log($"Local hand refreshed: client {localClientId}, cards {visibleCardCount}");
    }

    // コンテナの中身をすべて削除
    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
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


    public bool TryPlaceCardFromUI(int x, int y, CardType cardtype, bool rotated)
    {
        if (!CanPlaceCardFromUI(x, y, cardtype, rotated))
        {
            return false;
        }

        // クライアントが勝手に動かすのではなく、サーバーに処理を依頼する
        RequestPlaceCardServerRpc(x, y, cardtype, rotated);
        return true;
    }

    public bool CanPlaceCardFromUI(int x, int y, CardType cardType, bool rotated)
    {
        return IsLocalPlayerTurn()
            && IsTerrainCard(cardType)
            && CanPlaceCard(NetworkManager.Singleton.LocalClientId, new Vector2Int(x, y), cardType, rotated);
    }

    //おける場所のハイライト表示
    public void ShowPlacementHighlights(CardType cardType, bool rotated)
    {
        Debug.Log($"[Debug] ハイライト処理開始: {cardType}");
        placementHighlightsVisible = true;
        highlightedCardType = cardType;
        highlightedCardRotated = rotated;
        RefreshPlacementHighlights();
    }

    public void UpdatePlacementHighlights(CardType cardType, bool rotated)
    {
        if (placementHighlightsVisible && highlightedCardType == cardType && highlightedCardRotated == rotated)
        {
            return;
        }

        placementHighlightsVisible = true;
        highlightedCardType = cardType;
        highlightedCardRotated = rotated;
        RefreshPlacementHighlights();
    }

    private void RefreshPlacementHighlights()
    {
        bool shouldShow = placementHighlightsVisible
            && IsLocalPlayerTurn()
            && IsTerrainCard(highlightedCardType);

        int highlightedCount = 0;
        List<CellComponent> cells = GetBoardCells();

        foreach (CellComponent cell in cells)
        {
            bool canPlace = shouldShow
                && CanPlaceCardFromUI(cell.x, cell.y, highlightedCardType, highlightedCardRotated);

            if (canPlace)
            {
                highlightedCount++;
            }

            cell.SetPlacementHighlight(canPlace);
        }

        Debug.Log($"[Debug] Highlight cells: {highlightedCount}/{cells.Count}, card: {highlightedCardType}");
    }


    public void ClearPlacementHighlights()
    {
        placementHighlightsVisible = false;
        foreach (CellComponent cell in GetBoardCells())
        {
            cell.SetPlacementHighlight(false);
        }
    }

    private List<CellComponent> GetBoardCells()
    {
        cachedBoardCells.RemoveAll(cell => cell == null);
        if (cachedBoardCells.Count == 0)
        {
            cachedBoardCells.AddRange(FindObjectsOfType<CellComponent>());
        }

        return cachedBoardCells;
    }
    public bool TryPlayActionCardFromUI(CardType cardType)
    {
        if (NetworkManager.Singleton == null)
        {
            return false;
        }

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        return TryPlayActionCardFromUI(cardType, localClientId);
    }

    public bool TryPlayActionCardFromUI(CardType cardType, ulong targetClientId)
    {
        if (!IsLocalPlayerTurn() ||
            !IsActionCard(cardType) ||
            !HasPlayer(targetClientId) ||
            !IsValidLocalActionTarget(cardType, targetClientId))
        {
            return false;
        }

        RequestPlayActionCardServerRpc(cardType, targetClientId);
        return true;
    }

    public bool IsValidLocalActionTarget(CardType cardType, ulong targetClientId)
    {
        if (NetworkManager.Singleton == null || !HasPlayer(targetClientId))
        {
            return false;
        }

        return actionTargetPolicy.IsValidTarget(
            cardType,
            NetworkManager.Singleton.LocalClientId,
            targetClientId);
    }

    public void TryDiscardAndDrawFromUI(CardType cardType)
    {
        RequestDiscardAndDrawServerRpc(cardType);
    }

    //カードを盤面に置く処理
    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceCardServerRpc(int x, int y, CardType cardType, bool rotated, ServerRpcParams rpcParams = default)
    {

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!CanAct(senderClientId) || !IsTerrainCard(cardType) || !HasCardInHand(senderClientId, cardType))
        {
            // どの条件で失敗したか特定するログに書き換える
            Debug.Log($"配置失敗: CanAct={CanAct(senderClientId)}, IsTerrain={IsTerrainCard(cardType)}, HasCard={HasCardInHand(senderClientId, cardType)}");
            RejectPlaceCardClientRpc(CreateTargetClientRpcParams(senderClientId));
            return;
        }
        
        

       
        Vector2Int position = new Vector2Int(x, y);

        if (!CanPlaceCard(senderClientId, position, cardType, rotated))
        {
            Debug.Log("配置失敗: 配置ルールを満たしていません");
            RejectPlaceCardClientRpc(CreateTargetClientRpcParams(senderClientId));
            return;
        }
       
        RemoveCardFromHand(senderClientId, cardType); //手札からカードを削除
        //盤面にカードを追加
        placedCards.Add(new CardState(
            x,
            y,
            cardType,
            rotated,
            senderClientId,
            false,
            false,
            false));

        RevealConnectedGoals(senderClientId);
        if (!gameEnded.Value)
        {
            DrawCard(senderClientId);
            AdvanceTurn();
        }
    }

    [ClientRpc]
    private void RejectPlaceCardClientRpc(ClientRpcParams clientRpcParams = default)
    {
        DraggableCard.ReturnPendingPlacementToHand();
    }

    private ClientRpcParams CreateTargetClientRpcParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };
    }
    public void ExecutePlacementOnServer(int x, int y, CardType cardType, bool rotated)
    {
        // 配置場所のセルを取得
        CellComponent cell = GetCellAt(x, y); // 座標からCellを取得するメソッド(既存のもの)

        if (cell == null || !IsCellEmpty(x, y)) return;

        // カードのプレハブからインスタンスを生成してスポーンする
        GameObject cardInstance = Instantiate(cardPrefab).gameObject;
        NetworkObject netObj = cardInstance.GetComponent<NetworkObject>();

        // サーバー上でスポーンさせる
        netObj.Spawn();

        //親子関係を設定
        netObj.TrySetParent(cell.transform);

        // クライアント側の見た目を整える
        // 配置完了を全員に通知するClientRpc
        UpdateCardVisualClientRpc(netObj.NetworkObjectId, x, y);
    }

    [ClientRpc]
    private void UpdateCardVisualClientRpc(ulong networkObjectId, int x, int y)
    {
        // 必要に応じて、スポーンしたカードのローカル位置をリセット
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            netObj.transform.localPosition = Vector3.zero;
            netObj.transform.localScale = Vector3.one;

            RectTransform cardRect = netObj.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                MatchCardSizeToCell(cardRect, x, y);
            }
        }
    }
    //アクションカードをプレイする処理
    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayActionCardServerRpc(CardType cardType, ulong targetClientId, ServerRpcParams rpcParams = default)
    {

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!CanAct(senderClientId) ||
            !IsActionCard(cardType) ||
            !HasCardInHand(senderClientId, cardType) ||
            !HasPlayer(targetClientId) ||
            !actionTargetPolicy.IsValidTarget(cardType, senderClientId, targetClientId))
        {
            return;
        }

        RemoveCardFromHand(senderClientId, cardType);//手札からカードを削除
        ApplyActionEffect(senderClientId, cardType, targetClientId); //カードの効果を適応
        DrawCard(senderClientId);                    //カードを引く
        AdvanceTurn();                               //ターンを進める
    }

    //アクションカードの効果を適応する処理
    private void ApplyActionEffect(ulong senderId, CardType cardType, ulong targetClientId)
    {
        string targetName = GetPlayerName(targetClientId);
        switch (cardType)
        {
            case CardType.ActionFallingRocks:
            case CardType.Fallingrocks:
                //落石の処理
                Debug.Log($"落盤対象: {targetName}");
                break;
            case CardType.ActionMap:
            case CardType.Treasuremap:
                // 地図の処理
                Debug.Log($"宝の地図対象: {targetName}");
                break;
            case CardType.Lanternban:
            case CardType.Pickaxeban:
            case CardType.Railcarban:
                SetPlayerToolBrokenState(targetClientId, cardType, true);
                Debug.Log($"妨害カード{targetName} を対象にしました");
                break;
            case CardType.Lanternrepaire:
            case CardType.Pickaxerepaire:
            case CardType.Railcarrepaire:
                SetPlayerToolBrokenState(targetClientId, cardType, false);
                Debug.Log($"修理カード{targetName} を対象にしました");
                break;
            default:
                Debug.Log($"アクションカード: {cardType}, 対象: {targetName}");
                break;
        }
    }
    //プレイヤーの持っている道具の状態を管理して更新する
    private void SetPlayerToolBrokenState(ulong targetClientId, CardType cardType, bool isBroken)
    {
        for (int i = 0; i < placedCards.Count; i++)
        {
            CardState card = placedCards[i];
            if (card.ownerClientId != targetClientId)
            {
                continue;
            }

            bool updated = false;
            //isBrokenの値に書き換える
            switch (cardType)
            {
                case CardType.Lanternban:
                case CardType.Lanternrepaire:
                    card.isLanternBroken = isBroken;
                    updated = true;
                    break;
                case CardType.Pickaxeban:
                case CardType.Pickaxerepaire:
                    card.isPickaxeBroken = isBroken;
                    updated = true;
                    break;
                case CardType.Railcarban:
                case CardType.Railcarrepaire:
                    card.isRailcarBroken = isBroken;
                    updated = true;
                    break;
            }

            if (updated)
            {
                placedCards[i] = card;
            }
        }

        SetPlayerNetworkToolBrokenState(targetClientId, cardType, isBroken);//ネットワークの同期
        RefreshPlayerList();
    }

    private CellComponent GetCellAt(int x, int y)
    {
        // 盤面上のすべてのCellComponentを探して、座標が一致するものを返す
        foreach (var cell in FindObjectsOfType<CellComponent>())
        {
            if (cell.x == x && cell.y == y) // もし CellComponent に x, y というプロパティがあれば
            {
                return cell;
            }
        }
        return null;
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

    private bool IsRoadCard(CardType cardType)
    {
        return cardType == CardType.PathStraight ||
               cardType == CardType.PathCorner ||
               cardType == CardType.PathTJunction ||
               cardType == CardType.PathCross     ||
               cardType == CardType.DeadEnd       ||
               cardType == CardType.LRdeadend     ||
               cardType == CardType.LDdeadend     ||
               cardType == CardType.UDLRdeadend   ||
               cardType == CardType.UDLdeadend    ||
               cardType == CardType.RDdeadend     ||
               cardType == CardType.Ldeadend      ||
               cardType == CardType.Udeadend      ||
               cardType == CardType.ULRdeadend    ||
               cardType == CardType.UDLload       ||
               cardType == CardType.DRload        ||
               cardType == CardType.URload        ||
               cardType == CardType.DLload        ||
               cardType == CardType.ULload        ||
               cardType == CardType.UDload        ||
               cardType == CardType.DLRload       ||
               cardType == CardType.ULRload       ||
               cardType == CardType.LRload        ||
               cardType == CardType.UDLRload;
    }

    //指定された位置にカードを置けるかを確認する
    private bool CanPlaceCard(ulong clientId, Vector2Int position, CardType cardType, bool rotated)
    {
        // 道カードを置くには、自分の道具が壊れていない必要があります。
        if (IsRoadCard(cardType))
        {
            TryGetPlayerToolBrokenState(
                clientId,
                out bool isLanternBroken,
                out bool isPickaxeBroken,
                out bool isRailcarBroken);

            if (isLanternBroken || isRailcarBroken || isPickaxeBroken)
            {
                Debug.Log("失敗: 道具が壊れているため、道カードを置けません");
                return false;
            }
        }

        // 現在の盤面リストの中身をすべて表示
        foreach (var card in placedCards)
        {
            Debug.Log($"[CheckList] 盤面にあるカード: Type={card.cardType}, Pos=({card.x}, {card.y})");
        }
        if (placedCards.Count == 0)
        {
            Debug.Log("[PlacementTest] 初手配置許可");
            return true;
        }

        if (HasCardAt(position)) //カードが置かれているか
        {
            Debug.Log("失敗: 既にカードがあります");
            return false;
        }

        //上下いずれかにカードが置かれているかを確認
        bool hasNeighbor = HasCardAt(position + Vector2Int.up)
                        || HasCardAt(position + Vector2Int.down)
                        || HasCardAt(position + Vector2Int.left)
                        || HasCardAt(position + Vector2Int.right);

        if (!hasNeighbor) { Debug.Log("失敗: 隣接するカードがありません"); return false; }

        // ここで詳細に分ける
        bool ruleOk = CardRules.CanPlaceCard(position, cardType, rotated, placedCards);
        bool connectOk = connectivityService.ConnectsToStart(
            position,
            cardType,
            rotated,
            CreatePlacedCardsSnapshot());

        if (!ruleOk) { Debug.Log("失敗: 道路の接続ルールに違反しています"); return false; }


        if (!connectOk) { Debug.Log("失敗: スタートカードにつながっていません"); return false; }


        return true;
       
    }

    private List<CardState> CreatePlacedCardsSnapshot()
    {
        List<CardState> snapshot = new List<CardState>(placedCards.Count);
        for (int i = 0; i < placedCards.Count; i++)
        {
            snapshot.Add(placedCards[i]);
        }

        return snapshot;
    }

    private bool HasCardAt(Vector2Int position)
    {
        foreach (var card in placedCards)
        {
            if (card.x == position.x && card.y == position.y)
            {
                return true;
            }
        }
        return false;
    }

    //行動圏内のプレイヤーかどうかを確認する
    private bool CanAct(ulong clientId)
    {
        return IsServer && !gameEnded.Value && players.Count > 0 && players[currentPlayerIndex.Value].clientId == clientId;
    }


    public int PlayerCount
    {
        get { return players != null ? players.Count : 0; }
    }

    public bool TryGetPlayerInfo(int index, out PlayerInfo playerInfo)
    {
        if (players != null && index >= 0 && index < players.Count)
        {
            playerInfo = players[index];
            return true;
        }

        playerInfo = default;
        return false;
    }
    //指定されたプレイヤーの道具の状態を取得する処理
    public bool TryGetPlayerToolBrokenState(ulong clientId, out bool isLanternBroken, out bool isPickaxeBroken, out bool isRailcarBroken)
    {
        isLanternBroken = false;
        isPickaxeBroken = false;
        isRailcarBroken = false;

        PlayerNetworkData playerData = FindPlayerNetworkData(clientId);//サーバー側のプレイヤーデータを取得
        if (playerData != null)
        {
            isLanternBroken = playerData.isLanternBroken.Value;
            isPickaxeBroken = playerData.isPickaxeBroken.Value;
            isRailcarBroken = playerData.isRailcarBroken.Value;
            return true;
        }

        foreach (var card in placedCards)
        {
            if (card.ownerClientId != clientId)
            {
                continue;
            }

            isLanternBroken |= card.isLanternBroken;
            isPickaxeBroken |= card.isPickaxeBroken;
            isRailcarBroken |= card.isRailcarBroken;
        }

        return isLanternBroken || isPickaxeBroken || isRailcarBroken;
    }

    private void SetPlayerNetworkToolBrokenState(ulong targetClientId, CardType cardType, bool isBroken)
    {
        PlayerNetworkData playerData = FindPlayerNetworkData(targetClientId);
        if (playerData != null)
        {
            playerData.SetToolBrokenState(cardType, isBroken);
        }
    }

    private PlayerNetworkData FindPlayerNetworkData(ulong clientId)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
        {
            return null;
        }

        foreach (NetworkObject networkObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (networkObject == null || !networkObject.TryGetComponent(out PlayerNetworkData playerData))
            {
                continue;
            }

            PlayerInfo playerInfo = playerData.PlayerInfoVariable.Value;
            if (networkObject.OwnerClientId == clientId || playerInfo.clientId == clientId)
            {
                return playerData;
            }
        }

        return null;
    }

    private bool HasPlayer(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId == clientId)
            {
                return true;
            }
        }

        return false;
    }

    private string GetPlayerName(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId == clientId)
            {
                return players[i].playerName.ToString();
            }
        }

        return $"Player {clientId}";
    }
    //指定されたプレイヤーが手札に特定のカードを持っているかを確認する
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

    //カードを手札から削除する処理
    private bool RemoveCardFromHand(ulong clientId, CardType cardType)
    {
        for (int i = 0; i < dealtCards.Count; i++)
        {
            if (dealtCards[i].ownerClientId == clientId && dealtCards[i].cardType == cardType)
            {
                Debug.Log($"[Server] Removing card {cardType} from {clientId}. Remaining count: {dealtCards.Count - 1}");
                dealtCards.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

 //カードを引く処理
    private void DrawCard(ulong clientId)
    {
        //山札の状態を確認
        if (deck.Count == 0)
        {
            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, deck.Count);      //山札からランダムにカードを引く
        CardType cardType = deck[randomIndex];              //引いたカードの種類を取得
        deck.RemoveAt(randomIndex);                         //山札から引いたカードを削除
        dealtCards.Add(new DealtCard(clientId, cardType));  //引いたカードをプレイヤーの手札に追加
    }
    // スタートから道がつながったゴールを公開し、金塊ならゲームを終了する。
    private void RevealConnectedGoals(ulong discoveringClientId)
    {
        if (!IsServer || gameEnded.Value)
        {
            return;
        }

        for (int i = 0; i < placedCards.Count; i++)
        {
            CardState goal = placedCards[i];
            if (goal.cardType != CardType.Goal)
            {
                continue;
            }

            Vector2Int goalPosition = new Vector2Int(goal.x, goal.y);
            if (!connectivityService.ExistingCardConnectsToStart(
                    goalPosition,
                    CreatePlacedCardsSnapshot()))
            {
                continue;
            }

            CardType revealedType = goalObjectiveService.GetRevealedCardType(goalPosition);
            bool foundGold = revealedType == CardType.GoalGold;
            goal.cardType = revealedType;
            goal.isFlipped = true;
            placedCards[i] = goal;

            if (!foundGold)
            {
                Debug.Log($"[Goal] Empty goal revealed at {goalPosition}.");
                continue;
            }

            winningClientId.Value = discoveringClientId;
            EndGame();
            Debug.Log($"[Goal] {GetPlayerName(discoveringClientId)} reached the gold and won the game.");
            return;
        }
    }

    //ゲーム終了処理
    private void EndGame()
    {
      
        gameEnded.Value = true;

        // 全クライアントに対してシーン遷移を命令
        NetworkManager.SceneManager.LoadScene("ResultScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    //次のプレイヤーにターンを進める処理
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

    private void OnGameEndedChanged(bool previousValue, bool newValue)
    {
        RefreshTurnUI();
        RefreshLocalHand();
        RefreshPlacementHighlights();
    }

    private void OnTurnChanged(int previousValue, int newValue)
    {
        RefreshPlayerList();
        RefreshLocalHand();
        RefreshTurnUI();
        RefreshPlacementHighlights();
    }

    //ますにかーどがあるかどうかを確認する
    private bool IsCellEmpty(int x, int y)
    {
       
        return !HasCardAt(new Vector2Int(x, y));
    }

    private void RefreshTurnUI()
    {
        if (turnText == null) return;

        if (gameEnded.Value)
        {
            turnText.text = $"{GetPlayerName(winningClientId.Value)} が金塊を発見！\n勝利！";
            return;
        }

        if (players.Count == 0)
        {
            // インスペクターで設定したデフォルトの待機状態を表示
            turnText.text = string.Format(roundFormat, 1) + "\n" + waitingText;
            return;
        }

        int safeIndex = Mathf.Clamp(currentPlayerIndex.Value, 0, players.Count - 1);
        var currentPlayer = players[safeIndex];

        bool isLocalTurn = NetworkManager.Singleton != null &&
                           currentPlayer.clientId == NetworkManager.Singleton.LocalClientId;

        // 設定したメッセージ変数を利用
        string statusText = isLocalTurn ? myTurnText : waitingText;
        string roundText = string.Format(roundFormat, roundNumber.Value);
        string nameText = string.Format(turnFormat, currentPlayer.playerName.ToString());

        if (myTurnPosition != null && waitingPosition != null)
        {
            turnTextRect.position = isLocalTurn ? myTurnPosition.position : waitingPosition.position;
        }

        turnText.text = $"{roundText}\n{nameText}\n{statusText}";
    }

    //プレイヤーのターン表示を更新するため
    private void EnsureTurnText()
    {
        if (turnText != null)
        {
            return;
        }

     if(players.Count == 0)
     {
            turnText.text = "ラウンド 1\n待機中";
            return;
     }

        int safeIndex = Mathf.Clamp(currentPlayerIndex.Value,0, players.Count - 1);  //playerの人数の制限(範囲:最小0,最大player-1)
        string currentName = players[safeIndex].playerName.ToString();                //現在のターンのplayerの名前
        string localTurnLine = NetworkManager.Singleton != null &&                    //ネットワークマネージャーが存在する場合
                           players[safeIndex].clientId == NetworkManager.Singleton.LocalClientId
        ? "あなたの番です"
        : "待機中";
    }

    //現在のターンのプレイヤーかどうかを判定する
    private bool IsCurrentTurnIndex(int playerIndex)
    {
        return players.Count > 0 && playerIndex == Mathf.Clamp(currentPlayerIndex.Value, 0, players.Count - 1);
    }

    //ローカルプレイヤーのターンかどうかを判定する
    public bool IsLocalPlayerTurn()
    {
        if (gameEnded.Value || NetworkManager.Singleton == null || players.Count == 0)
        {
            return false;
        }

        int safeIndex = Mathf.Clamp(currentPlayerIndex.Value, 0, players.Count - 1);
        return players[safeIndex].clientId == NetworkManager.Singleton.LocalClientId;
    }

    //カードの操作可能状態を設定する
    private void SetCardInteractivity(CardView card, bool isInteractable)
    {
        CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = card.gameObject.AddComponent<CanvasGroup>();
        }

       
        canvasGroup.interactable = isInteractable;   //入力システムの無効化
        canvasGroup.blocksRaycasts = isInteractable; //マウスの判定の無効化
    }
    
    //アクションカードかどうかを判定する
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

    private bool IsTerrainCard(CardType cardType)
    {
        // Startも地形カードに含める
        if (cardType == CardType.Start) return true;

        // Action で始まる名前以外をすべて許可する
        string name = cardType.ToString();
        bool isAction = name.StartsWith("Action");

        // デバッグログを追加して判定を可視化する
        Debug.Log($"[Check] Card: {cardType}, IsAction: {isAction}");

        return !isAction;
    }


    //カードが盤面に置かれたらビューを更新する
    private void OnPlacedCardsChanged(NetworkListEvent<CardState> changeEvent)
    {
        if (changeEvent.Type == NetworkListEvent<CardState>.EventType.Add)
        {
            SpawnCardView(changeEvent.Value);//新しいカードのビューを生成
            RefreshPlacementHighlights();
            return;
        }

        RebuildBoardView();
        RefreshPlayerList();
        RefreshPlacementHighlights();
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

    private IEnumerator RebuildBoardViewAfterCellsReady()
    {
        yield return null;
        RebuildBoardView();
        RefreshPlayerList();
        RefreshPlacementHighlights();
    }

    //カードの表示を生成する
    private void SpawnCardView(CardState state)
    {
        if (cardPrefab == null)
        {
            return;
        }

        Vector2Int position = new Vector2Int(state.x, state.y);
        if (spawnedCards.ContainsKey(position))
        {
            return;
        }

        CellComponent targetCell = GetCellAt(state.x, state.y);
        Transform parent = targetCell != null
            ? targetCell.transform
            : boardRoot;
        if (parent == null)
        {
            return;
        }

        bool parentIsCell = targetCell != null;

        CardView cardView = Instantiate(cardPrefab, parent);
        // 配置時の向きはプレイヤーがRキーで選んだ状態だけを反映する。
        CardRotationController.ApplyRotation(cardView.transform, state.rotated);
        RectTransform rectTransform = cardView.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = parentIsCell
                ? Vector2.zero
                : new Vector2(state.x * cellSize, state.y * cellSize);
            MatchCardSizeToCell(rectTransform, state.x, state.y);
        }
        else
        {
            cardView.transform.localPosition = parentIsCell
                ? Vector3.zero
                : new Vector3(state.x * cellSize, state.y * cellSize, 0f);
        }

        cardView.SetCard(state.cardType,state.isFlipped);
        spawnedCards.Add(position, cardView);
    }

    private void MatchCardSizeToCell(RectTransform cardRect, int x, int y)
    {
        RectTransform targetRect = null;
        CellComponent cell = GetCellAt(x, y);
        if (cell != null)
        {
            targetRect = cell.GetComponent<RectTransform>();
        }

        if (targetRect == null && x == startCardX && y == startCardY && startCardRoot != null)
        {
            targetRect = startCardRoot.GetComponent<RectTransform>();
        }

        if (targetRect == null && x == goalCardX && y == goalCardY && goalCardRoots != null)
        {
            for (int i = 0; i < goalCardRoots.Length; i++)
            {
                if (goalCardRoots[i] != null)
                {
                    targetRect = goalCardRoots[i].GetComponent<RectTransform>();
                    break;
                }
            }
        }

        Vector2 targetSize = targetRect != null
            ? targetRect.rect.size
            : new Vector2(cellSize, cellSize);

        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = targetSize;
        cardRect.localScale = Vector3.one;
    }
    //ネットワーク終了時のイベント解除
    public override void OnNetworkDespawn()
    {
        placedCards.OnListChanged -= OnPlacedCardsChanged;
        players.OnListChanged -= OnPlayersChanged;
        dealtCards.OnListChanged -= OnDealtCardsChanged;
        currentPlayerIndex.OnValueChanged -= OnTurnChanged;
        roundNumber.OnValueChanged -= OnTurnChanged;
        gameEnded.OnValueChanged -= OnGameEndedChanged;
    }

    private void OnDestroy()
    {
        connectedPlayers?.Dispose();
        placedCards?.Dispose();
        players?.Dispose();
        dealtCards?.Dispose();
    }
}
