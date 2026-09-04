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

    [Header("Private Role UI")]
    [SerializeField] private Sprite minerRoleSprite;
    [SerializeField] private Sprite saboteurRoleSprite;

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

    [Header("UI References (BoardManager内で完結)")]
    [SerializeField] private GameObject choicePanel;       // ダイアログパネル本体
    [SerializeField] private Transform buttonContainer;    // ボタンを並べる親Transform
    [SerializeField] private GameObject buttonPrefab;       // ボタンのプレハブ

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
    private NetworkList<PlayerToolState> playerToolStates;
    private NetworkList<DealtCard> dealtCards;           // 手札の同期用リスト（所有者情報を含む）

    // --- ローカル状態管理 ---
    private readonly Dictionary<Vector2Int, CardView> spawnedCards = new Dictionary<Vector2Int, CardView>();
    private readonly List<PlayerDisplay> spawnedPlayerDisplays = new List<PlayerDisplay>();
    private readonly List<CardView> spawnedHandCards = new List<CardView>();
    private readonly List<CellComponent> cachedBoardCells = new List<CellComponent>();
    private readonly List<CardType> deck = new List<CardType>(); // サーバーのみが保持する山札リスト
    private readonly Dictionary<ulong, PlayerRole> serverPlayerRoles = new Dictionary<ulong, PlayerRole>();

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
    private bool actionTargetSelectionActive;
    private CardType pendingTargetActionCard;
    private GameObject actionTargetPanel;
    private readonly List<GameObject> actionTargetPanelEntries = new List<GameObject>();
    private GameObject localRoleImageObject;
    private bool rolesAssigned;
    private GameObject currentSelectedCardObject;
    private CardType currentSelectedActionCard;
    private ulong currentSelectedTargetClientId;
    public static BoardManager Instance;
    public bool selectingFallingRocks = false;


    private void Start()
    {
        // 最初はUIを非表示にしておく
        if (choicePanel != null) choicePanel.SetActive(false);
    }

    private void Awake()
    {
        Instance = this;
        // NetworkListの初期化
        connectedPlayers = new NetworkList<ulong>();
        placedCards = new NetworkList<CardState>();
        players = new NetworkList<PlayerInfo>();
        playerToolStates = new NetworkList<PlayerToolState>();
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
        playerToolStates.OnListChanged += OnPlayerToolStatesChanged;
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

            StartCoroutine(AssignRolesWhenPlayersReady());

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

        EnsurePlayerToolState(clientId);

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

    private void EnsurePlayerToolState(ulong clientId)
    {
        for (int i = 0; i < playerToolStates.Count; i++)
        {
            if (playerToolStates[i].clientId == clientId)
            {
                return;
            }
        }

        playerToolStates.Add(new PlayerToolState(clientId));
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

    private IEnumerator AssignRolesWhenPlayersReady()
    {
        while (!rolesAssigned)
        {
            int connectedCount = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ConnectedClientsIds.Count
                : 0;

            if (connectedCount >= 2 && players.Count == connectedCount)
            {
                // 全クライアント側でシーンオブジェクトがSpawnされる時間を1フレーム確保する。
                yield return null;
                AssignRolesOnServer();
                yield break;
            }

            yield return null;
        }
    }

    private void AssignRolesOnServer()
    {
        if (!IsServer || rolesAssigned || players.Count < 2)
        {
            return;
        }

        List<ulong> shuffledClientIds = new List<ulong>();
        for (int i = 0; i < players.Count; i++)
        {
            shuffledClientIds.Add(players[i].clientId);
        }

        for (int i = 0; i < shuffledClientIds.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, shuffledClientIds.Count);
            ulong temporary = shuffledClientIds[i];
            shuffledClientIds[i] = shuffledClientIds[randomIndex];
            shuffledClientIds[randomIndex] = temporary;
        }

        int saboteurCount = shuffledClientIds.Count == 4
            ? UnityEngine.Random.Range(1, 3)
            : Mathf.Clamp(Mathf.RoundToInt(shuffledClientIds.Count / 3f), 1, shuffledClientIds.Count - 1);

        serverPlayerRoles.Clear();
        for (int i = 0; i < shuffledClientIds.Count; i++)
        {
            ulong clientId = shuffledClientIds[i];
            PlayerRole role = i < saboteurCount ? PlayerRole.Saboteur : PlayerRole.Miner;
            serverPlayerRoles[clientId] = role;

            ReceivePrivateRoleClientRpc(
                role,
                CreateTargetClientRpcParams(clientId));
        }

        rolesAssigned = true;
        Debug.Log($"[Role] Assigned {saboteurCount} saboteur(s) and {shuffledClientIds.Count - saboteurCount} miner(s).");
    }

    [ClientRpc]
    private void ReceivePrivateRoleClientRpc(PlayerRole role, ClientRpcParams clientRpcParams = default)
    {
        ShowLocalRoleImage(role);
    }

    private void ShowLocalRoleImage(PlayerRole role)
    {
        if (mainCanvas == null)
        {
            return;
        }

        if (localRoleImageObject == null)
        {
            localRoleImageObject = new GameObject(
                "PrivateRoleImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            localRoleImageObject.transform.SetParent(mainCanvas.transform, false);

            RectTransform roleRect = localRoleImageObject.GetComponent<RectTransform>();
            roleRect.anchorMin = new Vector2(0f, 1f);
            roleRect.anchorMax = new Vector2(0f, 1f);
            roleRect.pivot = new Vector2(0f, 1f);
            roleRect.anchoredPosition = new Vector2(24f, -24f);
            roleRect.sizeDelta = new Vector2(180f, 180f);
        }

        Image roleImage = localRoleImageObject.GetComponent<Image>();
        roleImage.sprite = role == PlayerRole.Saboteur ? saboteurRoleSprite : minerRoleSprite;
        roleImage.preserveAspect = true;
        roleImage.raycastTarget = false;
        localRoleImageObject.SetActive(roleImage.sprite != null);
        localRoleImageObject.transform.SetAsLastSibling();
    }

    private void OnPlayerToolStatesChanged(NetworkListEvent<PlayerToolState> changeEvent)
    {
        RefreshPlayerList();
        RefreshLocalHand();
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

        RefreshActionTargetSelectionHighlights();
        RefreshActionTargetSelectionPanel();
    }

    /// 既存のプレイヤー選択UIで「プレイヤー名」がクリックされた時の処理
    public void OnSelectPlayerTarget(ulong targetClientId, CardType selectedCardType)
    {
        currentSelectedTargetClientId = targetClientId;
        currentSelectedActionCard = selectedCardType;

        EnsurePlayerToolState(targetClientId);
        TryGetPlayerToolBrokenState(targetClientId, out bool isLanternBroken, out bool isPickaxeBroken, out bool isRailcarBroken);

        // ----------------------------------------------------
        // 2種対応修復カードの判定
        // ----------------------------------------------------
        switch (selectedCardType)
        {
            case CardType.PickaxeOrLanternrepaire:
                if (isPickaxeBroken && isLanternBroken)
                {
                    // 両方壊れている ➔ 道具選択ボタンを表示する！
                    ShowToolSelectionButtons(targetClientId, CardType.Pickaxerepaire, CardType.Lanternrepaire);
                    return; // ここで一旦止めてプレイヤーの道具選択を待つ
                }
                else if (isPickaxeBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Pickaxerepaire, false);
                }
                else if (isLanternBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Lanternrepaire, false);
                }
                break;

            case CardType.PickaxeOrRailcarrepaire:
                if (isPickaxeBroken && isRailcarBroken)
                {
                    ShowToolSelectionButtons(targetClientId, CardType.Pickaxerepaire, CardType.Railcarrepaire);
                    return;
                }
                else if (isPickaxeBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Pickaxerepaire, false);
                }
                else if (isRailcarBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Railcarrepaire, false);
                }
                break;

            case CardType.LanternOrRailcarrepaire:
                if (isLanternBroken && isRailcarBroken)
                {
                    ShowToolSelectionButtons(targetClientId, CardType.Lanternrepaire, CardType.Railcarrepaire);
                    return;
                }
                else if (isLanternBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Lanternrepaire, false);
                }
                else if (isRailcarBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Railcarrepaire, false);
                }
                break;

            // 1種カードや妨害カードなどは既存の通り処理
            default:
                SetPlayerToolBrokenState(targetClientId, selectedCardType, selectedCardType.ToString().EndsWith("ban"));
                break;
        }

        // 選択完了したらターゲットパネルを閉じる
        CloseActionTargetSelectionPanel();
    }

    /// <summary>
    /// 両方壊れている時に「道具選択ボタン（選択肢）」を表示・切替する処理
    /// </summary>
    private void ShowToolSelectionButtons(ulong targetClientId, CardType optionA, CardType optionB)
    {

    }

    /// <summary>
    /// ターゲット選択パネルを閉じる関数
    /// </summary>
    private void CloseActionTargetSelectionPanel()
    {
        // パネルを非表示にし、RefreshPlayerList() などを呼んで表示を最新化する
        RefreshPlayerList();
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

    public bool CanUseCardFromUI(CardType cardType)
    {
        if (!IsLocalPlayerTurn() || NetworkManager.Singleton == null)
        {
            return false;
        }

        if (!IsRoadCard(cardType))
        {
            return true;
        }

        TryGetPlayerToolBrokenState(
            NetworkManager.Singleton.LocalClientId,
            out bool isLanternBroken,
            out bool isPickaxeBroken,
            out bool isRailcarBroken);
        return !isLanternBroken && !isPickaxeBroken && !isRailcarBroken;
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

        if (IsFallingRocksCard(cardType) || IsTreasureMapCard(cardType))
        {
            return false;
        }

        if (actionTargetPolicy.RequiresOtherPlayer(cardType))
        {
            return BeginActionTargetSelection(cardType);
        }

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        return TryPlayActionCardFromUI(cardType, localClientId, 0, 0);
    }

    public bool TryPlayActionCardFromUI(CardType cardType, ulong targetClientId, int targetX, int targetY)
    {
        if (!IsLocalPlayerTurn() ||
            !IsActionCard(cardType) ||
            IsFallingRocksCard(cardType) ||
            IsTreasureMapCard(cardType) ||
            !HasPlayer(targetClientId) ||
            !IsValidLocalActionTarget(cardType, targetClientId))
        {
            return false;
        }

        ClearActionTargetSelection();
        RequestPlayActionCardServerRpc(cardType, targetClientId, targetX, targetY);
        return true;
    }

    public bool TryPlayTreasureMapFromUI(CardType cardType, int targetX, int targetY)
    {
        if (NetworkManager.Singleton == null ||
            !IsLocalPlayerTurn() ||
            !IsTreasureMapCard(cardType) ||
            !HasCardInHand(NetworkManager.Singleton.LocalClientId, cardType) ||
            !TryGetHiddenGoalIndex(new Vector2Int(targetX, targetY), out _))
        {
            return false;
        }

        ClearActionTargetSelection();
        RequestPlayTreasureMapServerRpc(cardType, targetX, targetY);
        return true;
    }

    public bool TryGetHiddenGoalAtScreenPoint(
        Vector2 screenPoint,
        Camera eventCamera,
        out int targetX,
        out int targetY)
    {
        foreach (KeyValuePair<Vector2Int, CardView> entry in spawnedCards)
        {
            if (entry.Value == null ||
                !TryGetHiddenGoalIndex(entry.Key, out _))
            {
                continue;
            }

            RectTransform cardRect = entry.Value.GetComponent<RectTransform>();
            if (cardRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    cardRect,
                    screenPoint,
                    eventCamera))
            {
                targetX = entry.Key.x;
                targetY = entry.Key.y;
                return true;
            }
        }

        targetX = 0;
        targetY = 0;
        return false;
    }

    public bool TryPlayFallingRocksFromUI(CardType cardType, int targetX, int targetY)
    {
        if (NetworkManager.Singleton == null ||
            !IsLocalPlayerTurn() ||
            !IsFallingRocksCard(cardType) ||
            !HasCardInHand(NetworkManager.Singleton.LocalClientId, cardType) ||
            !TryGetRemovableRoadIndex(new Vector2Int(targetX, targetY), out _))
        {
            return false;
        }

        ClearActionTargetSelection();
        RequestPlayFallingRocksServerRpc(cardType, targetX, targetY);
        return true;
    }

    public bool TryGetRemovableRoadAtScreenPoint(
        Vector2 screenPoint,
        Camera eventCamera,
        out int targetX,
        out int targetY)
    {
        foreach (KeyValuePair<Vector2Int, CardView> entry in spawnedCards)
        {
            if (entry.Value == null ||
                !TryGetRemovableRoadIndex(entry.Key, out _))
            {
                continue;
            }

            RectTransform cardRect = entry.Value.GetComponent<RectTransform>();
            if (cardRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    cardRect,
                    screenPoint,
                    eventCamera))
            {
                targetX = entry.Key.x;
                targetY = entry.Key.y;
                return true;
            }
        }

        targetX = 0;
        targetY = 0;
        return false;
    }

    private bool isSelectingFallingRocks = false;

    public void StartFallingRocksSelection()
    {
        isSelectingFallingRocks = true;
        Debug.Log("落石カード選択モード");
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

    private bool BeginActionTargetSelection(CardType cardType)
    {
        if (NetworkManager.Singleton == null ||
            !IsLocalPlayerTurn() ||
            !IsActionCard(cardType) ||
            !HasCardInHand(NetworkManager.Singleton.LocalClientId, cardType))
        {
            return false;
        }

        bool hasCandidate = false;
        for (int i = 0; i < players.Count; i++)
        {
            if (IsValidLocalActionTarget(cardType, players[i].clientId))
            {
                hasCandidate = true;
                break;
            }
        }

        if (!hasCandidate)
        {
            return false;
        }

        pendingTargetActionCard = cardType;
        actionTargetSelectionActive = true;
        RefreshActionTargetSelectionHighlights();
        RefreshActionTargetSelectionPanel();
        RefreshTurnUI();
        return true;
    }

    public bool TrySelectPendingActionTarget(ulong targetClientId)
    {
        if (!actionTargetSelectionActive ||
            !IsValidLocalActionTarget(pendingTargetActionCard, targetClientId))
        {
            return false;
        }

        return TryPlayActionCardFromUI(pendingTargetActionCard, targetClientId, 0, 0);
    }

    private void ClearActionTargetSelection(bool refreshTurnText = true)
    {
        if (!actionTargetSelectionActive)
        {
            return;
        }

        actionTargetSelectionActive = false;
        RefreshActionTargetSelectionHighlights();
        RefreshActionTargetSelectionPanel();
        if (refreshTurnText)
        {
            RefreshTurnUI();
        }
    }

    private void RefreshActionTargetSelectionHighlights()
    {
        PlayerDisplay[] displays = FindObjectsByType<PlayerDisplay>(FindObjectsSortMode.None);
        foreach (PlayerDisplay display in displays)
        {
            bool highlighted = actionTargetSelectionActive &&
                               IsValidLocalActionTarget(pendingTargetActionCard, display.ClientId);
            display.SetDragTargetHighlighted(highlighted);
        }
    }

    /// <summary>
    /// 妨害カードを左クリックした時だけ、自分以外の対象候補を専用パネルに表示する。
    /// シーンへの手作業でのUI追加を不要にするため、パネルは実行時に生成する。
    /// </summary>
    private void RefreshActionTargetSelectionPanel()
    {
        if (!actionTargetSelectionActive)
        {
            if (actionTargetPanel != null)
            {
                actionTargetPanel.SetActive(false);
            }
            return;
        }

        EnsureActionTargetPanel();
        if (actionTargetPanel == null)
        {
            return;
        }

        foreach (GameObject entry in actionTargetPanelEntries)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }
        actionTargetPanelEntries.Clear();

        actionTargetPanel.SetActive(true);
        actionTargetPanel.transform.SetAsLastSibling();

        GameObject title = CreateActionTargetText(
            actionTargetPanel.transform,
            "対象プレイヤーを選択",
            28f,
            54f);
        actionTargetPanelEntries.Add(title);

        for (int i = 0; i < players.Count; i++)
        {
            PlayerInfo player = players[i];
            if (!IsValidLocalActionTarget(pendingTargetActionCard, player.clientId))
            {
                continue;
            }

            ulong targetClientId = player.clientId;
            GameObject buttonObject = new GameObject(
                $"ActionTarget_{targetClientId}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(actionTargetPanel.transform, false);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.18f, 0.22f, 0.28f, 0.98f);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 380f;
            layout.preferredHeight = 62f;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.88f, 0.45f, 1f);
            colors.pressedColor = new Color(1f, 0.72f, 0.25f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => TrySelectPendingActionTarget(targetClientId));

            CreateActionTargetText(
                buttonObject.transform,
                player.playerName.ToString(),
                25f,
                62f,
                true);
            actionTargetPanelEntries.Add(buttonObject);
        }

        GameObject cancelButton = new GameObject(
            "ActionTargetCancel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        cancelButton.transform.SetParent(actionTargetPanel.transform, false);
        cancelButton.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.35f, 0.95f);
        LayoutElement cancelLayout = cancelButton.GetComponent<LayoutElement>();
        cancelLayout.preferredWidth = 380f;
        cancelLayout.preferredHeight = 48f;
        cancelButton.GetComponent<Button>().onClick.AddListener(() => ClearActionTargetSelection());
        CreateActionTargetText(cancelButton.transform, "キャンセル", 21f, 48f, true);
        actionTargetPanelEntries.Add(cancelButton);
    }

    private void EnsureActionTargetPanel()
    {
        if (actionTargetPanel != null)
        {
            return;
        }

        Transform panelParent = mainCanvas != null ? mainCanvas.transform : transform;
        actionTargetPanel = new GameObject(
            "ActionTargetSelectionPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        actionTargetPanel.transform.SetParent(panelParent, false);

        RectTransform panelRect = actionTargetPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(420f, 0f);

        Image panelImage = actionTargetPanel.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.055f, 0.075f, 0.96f);

        VerticalLayoutGroup layout = actionTargetPanel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 18, 18);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = actionTargetPanel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private GameObject CreateActionTargetText(
        Transform parent,
        string content,
        float fontSize,
        float preferredHeight,
        bool stretchToParent = false)
    {
        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        if (stretchToParent)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        if (turnText != null && turnText.font != null)
        {
            text.font = turnText.font;
        }

        LayoutElement textLayout = textObject.GetComponent<LayoutElement>();
        textLayout.preferredHeight = preferredHeight;
        textLayout.ignoreLayout = stretchToParent;
        return textObject;
    }

    public bool TryDiscardAndDrawFromUI(CardType cardType)
    {
        if (NetworkManager.Singleton == null ||
            !IsLocalPlayerTurn() ||
            !IsDiscardableHandCard(cardType) ||
            !HasCardInHand(NetworkManager.Singleton.LocalClientId, cardType))
        {
            return false;
        }

        ClearActionTargetSelection();
        RequestDiscardAndDrawServerRpc(cardType);
        return true;
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
    private void RequestPlayActionCardServerRpc(CardType cardType, ulong targetClientId, int targetX, int targetY, ServerRpcParams rpcParams = default)
    {

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!CanAct(senderClientId) ||
            !IsActionCard(cardType) ||
            IsFallingRocksCard(cardType) ||
            IsTreasureMapCard(cardType) ||
            !HasCardInHand(senderClientId, cardType) ||
            !HasPlayer(targetClientId) ||
            !actionTargetPolicy.IsValidTarget(cardType, senderClientId, targetClientId))
        {
            return;
        }
        Vector2Int targetPosition = new Vector2Int(targetX, targetY);

        RemoveCardFromHand(senderClientId, cardType);//手札からカードを削除
        ApplyActionEffect(senderClientId, cardType, targetClientId, targetPosition); //カードの効果を適応
        DrawCard(senderClientId);                    //カードを引く
        AdvanceTurn();                               //ターンを進める
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayTreasureMapServerRpc(
        CardType cardType,
        int targetX,
        int targetY,
        ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Vector2Int targetPosition = new Vector2Int(targetX, targetY);
        if (!CanAct(senderClientId) ||
            !IsTreasureMapCard(cardType) ||
            !HasCardInHand(senderClientId, cardType) ||
            !TryGetHiddenGoalIndex(targetPosition, out _))
        {
            RejectPlaceCardClientRpc(CreateTargetClientRpcParams(senderClientId));
            return;
        }

        CardType revealedType = goalObjectiveService.GetRevealedCardType(targetPosition);
        RemoveCardFromHand(senderClientId, cardType);
        RevealGoalPrivatelyClientRpc(
            targetX,
            targetY,
            revealedType,
            CreateTargetClientRpcParams(senderClientId));
        DrawCard(senderClientId);
        AdvanceTurn();
    }

    [ClientRpc]
    private void RevealGoalPrivatelyClientRpc(
        int targetX,
        int targetY,
        CardType revealedType,
        ClientRpcParams clientRpcParams = default)
    {
        StartCoroutine(ShowPrivateGoalPreview(
            new Vector2Int(targetX, targetY),
            revealedType));
    }

    private IEnumerator ShowPrivateGoalPreview(
        Vector2Int goalPosition,
        CardType revealedType)
    {
        if (!spawnedCards.TryGetValue(goalPosition, out CardView goalView) ||
            goalView == null)
        {
            yield break;
        }

        goalView.SetCard(revealedType, true);
        yield return new WaitForSeconds(3f);

        if (!spawnedCards.TryGetValue(goalPosition, out goalView) ||
            goalView == null)
        {
            yield break;
        }

        for (int i = 0; i < placedCards.Count; i++)
        {
            CardState current = placedCards[i];
            if (current.x == goalPosition.x && current.y == goalPosition.y)
            {
                goalView.SetCard(current.cardType, current.isFlipped);
                yield break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayFallingRocksServerRpc(
        CardType cardType,
        int targetX,
        int targetY,
        ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Vector2Int targetPosition = new Vector2Int(targetX, targetY);
        if (!CanAct(senderClientId) ||
            !IsFallingRocksCard(cardType) ||
            !HasCardInHand(senderClientId, cardType) ||
            !TryGetRemovableRoadIndex(targetPosition, out int roadIndex))
        {
            RejectPlaceCardClientRpc(CreateTargetClientRpcParams(senderClientId));
            return;
        }

        RemoveCardFromHand(senderClientId, cardType);
        placedCards.RemoveAt(roadIndex);
        DrawCard(senderClientId);
        AdvanceTurn();
    }

    //アクションカードの効果を適応する処理
    private void ApplyActionEffect(ulong senderId,
    CardType cardType,
    ulong targetClientId,
        Vector2Int targetPosition
   )
    {
        string targetName = GetPlayerName(targetClientId);
        switch (cardType)
        {
            case CardType.ActionFallingRocks:
            case CardType.Fallingrocks:
                RemovePlacedCard(targetPosition);
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

            // 2種対応 修理カード
            case CardType.PickaxeOrLanternrepaire:
                EnsurePlayerToolState(targetClientId);
                PlayerToolState statePL = GetPlayerToolState(targetClientId);

                if (statePL.isPickaxeBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Pickaxerepaire, false);
                }
                else if (statePL.isLanternBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Lanternrepaire, false);
                }
                break;

            case CardType.PickaxeOrRailcarrepaire:
                EnsurePlayerToolState(targetClientId);
                PlayerToolState statePR = GetPlayerToolState(targetClientId);

                if (statePR.isPickaxeBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Pickaxerepaire, false);
                }
                else if (statePR.isRailcarBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Railcarrepaire, false);
                }
                break;

            case CardType.LanternOrRailcarrepaire:
                EnsurePlayerToolState(targetClientId);
                PlayerToolState stateLR = GetPlayerToolState(targetClientId);

                if (stateLR.isLanternBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Lanternrepaire, false);
                }
                else if (stateLR.isRailcarBroken)
                {
                    SetPlayerToolBrokenState(targetClientId, CardType.Railcarrepaire, false);
                }
                break;

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
        SetSynchronizedPlayerToolBrokenState(targetClientId, cardType, isBroken);

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

    // NetworkList<PlayerToolState> から安全にデータを取り出す関数
    private PlayerToolState GetPlayerToolState(ulong targetClientId)
    {
        for (int i = 0; i < playerToolStates.Count; i++)
        {
            if (playerToolStates[i].clientId == targetClientId)
            {
                return playerToolStates[i];
            }
        }
        return default;
    }

    private void SetSynchronizedPlayerToolBrokenState(ulong targetClientId, CardType cardType, bool isBroken)
    {
        EnsurePlayerToolState(targetClientId);

        for (int i = 0; i < playerToolStates.Count; i++)
        {
            PlayerToolState state = playerToolStates[i];
            if (state.clientId != targetClientId)
            {
                continue;
            }

            switch (cardType)
            {
                case CardType.Lanternban:
                case CardType.Lanternrepaire:
                    state.isLanternBroken = isBroken;
                    break;
                case CardType.Pickaxeban:
                case CardType.Pickaxerepaire:
                    state.isPickaxeBroken = isBroken;
                    break;
                case CardType.Railcarban:
                case CardType.Railcarrepaire:
                case CardType.PickaxeOrLanternrepaire:
                case CardType.PickaxeOrRailcarrepaire:
                case CardType.LanternOrRailcarrepaire:
                    state.isRailcarBroken = isBroken;
                    break;
                default:
                    return;
            }

            playerToolStates[i] = state;
            return;
        }
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
        if (!CanAct(senderClientId) ||
            !IsDiscardableHandCard(cardType) ||
            !HasCardInHand(senderClientId, cardType))
        {
            return;
        }

        RemoveCardFromHand(senderClientId, cardType);
        DrawCard(senderClientId);
        AdvanceTurn();
    }

    private bool IsDiscardableHandCard(CardType cardType)
    {
        return IsRoadCard(cardType) || IsActionCard(cardType);
    }

    private bool IsRoadCard(CardType cardType)
    {
        return cardType == CardType.PathStraight ||
               cardType == CardType.PathCorner ||
               cardType == CardType.PathTJunction ||
               cardType == CardType.PathCross ||
               cardType == CardType.DeadEnd ||
               cardType == CardType.LRdeadend ||
               cardType == CardType.LDdeadend ||
               cardType == CardType.UDLRdeadend ||
               cardType == CardType.UDLdeadend ||
               cardType == CardType.RDdeadend ||
               cardType == CardType.Ldeadend ||
               cardType == CardType.Udeadend ||
               cardType == CardType.ULRdeadend ||
               cardType == CardType.UDLload ||
               cardType == CardType.UDRload ||
               cardType == CardType.DRload ||
               cardType == CardType.URload ||
               cardType == CardType.DLload ||
               cardType == CardType.ULload ||
               cardType == CardType.UDload ||
               cardType == CardType.DLRload ||
               cardType == CardType.ULRload ||
               cardType == CardType.LRload ||
               cardType == CardType.UDLRload ||
               cardType == CardType.UDdeadend ||
               cardType == CardType.DLloadHandkerchief ||
               cardType == CardType.DRloadPocketwatch ||
               cardType == CardType.ULRloadBucket ||
               cardType == CardType.ULRloadMouse ||
               cardType == CardType.UDLloadPot ||
               cardType == CardType.UDLloadShoe ||
               cardType == CardType.UDLRloadBone ||
               cardType == CardType.UDLRloadCup ||
               cardType == CardType.UDLRloadHat ||
               cardType == CardType.LRloadSpoon ||
               cardType == CardType.LRloadWheel ||
               cardType == CardType.UDloadBucket ||
               cardType == CardType.UDLdeadendHedgehog ||
               cardType == CardType.UDdeadendFriedegg;
    }

    private static bool IsFallingRocksCard(CardType cardType)
    {
        return cardType == CardType.Fallingrocks ||
               cardType == CardType.ActionFallingRocks;
    }

    private static bool IsTreasureMapCard(CardType cardType)
    {
        return cardType == CardType.Treasuremap ||
               cardType == CardType.ActionMap;
    }

    //指定された位置にカードを置けるかを確認する
    private bool CanPlaceCard(ulong clientId, Vector2Int position, CardType cardType, bool rotated)
    {
        bool hasNormalRoadConnection = false;
        bool hasDeadEndConnection = false;
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
            Vector2Int neighborPos = new Vector2Int(card.x, card.y);

            // 上下左右のみ確認
            if (Vector2Int.Distance(position, neighborPos) != 1)
                continue;

            Vector2Int direction = neighborPos - position;//

            PathDirection newCardPaths =
       CardRules.GetRotatedPaths(cardType, rotated);

            PathDirection neighborPaths =
                CardRules.GetRotatedPaths(
                    card.cardType,
                    card.rotated
                );

            if (CardRules.HasRoadConnection(
               newCardPaths,
        CardRules.GetPathDirection(direction),
        neighborPaths,
        CardRules.GetOppositePathDirection(direction)))
            {
                if (CardRules.IsDeadEnd(card.cardType))
                {
                    hasDeadEndConnection = true;
                }
                else
                {
                    hasNormalRoadConnection = true;
                }
            }
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

        if (!hasNormalRoadConnection && hasDeadEndConnection)
        {
            Debug.Log("失敗: DeadEndにしか接続していません");
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

        for (int i = 0; i < playerToolStates.Count; i++)
        {
            PlayerToolState state = playerToolStates[i];
            if (state.clientId != clientId)
            {
                continue;
            }

            isLanternBroken = state.isLanternBroken;
            isPickaxeBroken = state.isPickaxeBroken;
            isRailcarBroken = state.isRailcarBroken;
            return true;
        }

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
        for (int i = 0; i < dealtCards.Count; i++)//手札を順番に調べる
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

    //盤面からカードを削除する処理
    private void RemovePlacedCard(Vector2Int position)
    {
        if (TryGetRemovableRoadIndex(position, out int roadIndex))
        {
            placedCards.RemoveAt(roadIndex);
        }
    }

    private bool TryGetRemovableRoadIndex(Vector2Int position, out int roadIndex)
    {
        for (int i = 0; i < placedCards.Count; i++)
        {
            CardState card = placedCards[i];
            if (card.x == position.x && card.y == position.y)
            {
                roadIndex = IsRoadCard(card.cardType) ? i : -1;
                return roadIndex >= 0;
            }
        }

        roadIndex = -1;
        return false;
    }

    private bool TryGetHiddenGoalIndex(Vector2Int position, out int goalIndex)
    {
        for (int i = 0; i < placedCards.Count; i++)
        {
            CardState card = placedCards[i];
            if (card.x == position.x && card.y == position.y)
            {
                goalIndex = card.cardType == CardType.Goal ? i : -1;
                return goalIndex >= 0;
            }
        }

        goalIndex = -1;
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

            // 金塊が見つかった場合は Miner チームの勝利扱いにする
            winningClientId.Value = discoveringClientId;
            EndGame(PlayerRole.Miner);
            Debug.Log($"[Goal] {GetPlayerName(discoveringClientId)} reached the gold and triggered Miner victory.");
            return;
        }
    }

    //ゲーム終了処理
    // ゲーム終了処理（勝者の役割に応じてシーン遷移）
    private void EndGame(PlayerRole winnerRole)
    {
        if (!IsServer)
        {
            // シーン遷移はサーバーから行う想定
            return;
        }

        gameEnded.Value = true;

        // Miner が勝ったら MResultScene、Saboteur が勝ったら SResultScene に遷移
        string sceneName = winnerRole == PlayerRole.Miner ? "MResultScene" : "SResultScene";

        Debug.Log($"[GameEnd] Ending game. WinnerRole={winnerRole}, LoadingScene={sceneName}");
        NetworkManager.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    // サーバー上で、全プレイヤーの手札（dealtCards）が0かどうかを確認してゲーム終了を行う
    private void CheckForEmptyHandsAndEndGame()
    {
        if (!IsServer || gameEnded.Value)
        {
            return;
        }

        // players にいる全クライアントが手札を持っていないかをチェック
        for (int i = 0; i < players.Count; i++)
        {
            ulong clientId = players[i].clientId;
            bool hasAnyCard = false;
            for (int j = 0; j < dealtCards.Count; j++)
            {
                if (dealtCards[j].ownerClientId == clientId)
                {
                    hasAnyCard = true;
                    break;
                }
            }

            if (hasAnyCard)
            {
                // 1人でもカードを持っているなら終了条件は満たさない
                return;
            }
        }

        // ここまで来たら全員の手札が0 → Saboteur の勝利
        Debug.Log("[EndCondition] All players have no cards. Saboteur victory triggered.");
        winningClientId.Value = ulong.MaxValue;
        EndGame(PlayerRole.Saboteur);
    }

    //次のプレイヤーにターンを進める処理
    // AdvanceTurn の末尾で「全員の手札が0か」をチェックして Saboteur 勝利を判定するように変更
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

        // ターン進行後に全員の手札が無くなっていないかチェック（全員0なら Saboteur の勝利）
        CheckForEmptyHandsAndEndGame();
    }

    private void OnGameEndedChanged(bool previousValue, bool newValue)
    {
        ClearActionTargetSelection(false);
        RefreshTurnUI();
        RefreshLocalHand();
        RefreshPlacementHighlights();
    }

    private void OnTurnChanged(int previousValue, int newValue)
    {
        ClearActionTargetSelection(false);
        RefreshPlayerList();
        RefreshLocalHand();
        RefreshTurnUI();
        RefreshPlacementHighlights();
    }

    //マスにカードがあるかどうかを確認する
    private bool IsCellEmpty(int x, int y)
    {

        return !HasCardAt(new Vector2Int(x, y));
    }

    private void RefreshTurnUI()
    {
        if (turnText == null) return;

        if (actionTargetSelectionActive)
        {
            turnText.text = "banカードの対象プレイヤーを選択してください";
            return;
        }

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

        if (players.Count == 0)
        {
            turnText.text = "ラウンド 1\n待機中";
            return;
        }

        int safeIndex = Mathf.Clamp(currentPlayerIndex.Value, 0, players.Count - 1);  //playerの人数の制限(範囲:最小0,最大player-1)
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
            case CardType.PickaxeOrLanternrepaire:
            case CardType.PickaxeOrRailcarrepaire:
            case CardType.LanternOrRailcarrepaire:
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

        cardView.SetCard(state.cardType, state.isFlipped);
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
        playerToolStates.OnListChanged -= OnPlayerToolStatesChanged;
        dealtCards.OnListChanged -= OnDealtCardsChanged;
        currentPlayerIndex.OnValueChanged -= OnTurnChanged;
        roundNumber.OnValueChanged -= OnTurnChanged;
        gameEnded.OnValueChanged -= OnGameEndedChanged;
    }

    private void OnDestroy()
    {
        if (actionTargetPanel != null)
        {
            Destroy(actionTargetPanel);
        }

        if (localRoleImageObject != null)
        {
            Destroy(localRoleImageObject);
        }

        connectedPlayers?.Dispose();
        placedCards?.Dispose();
        players?.Dispose();
        playerToolStates?.Dispose();
        dealtCards?.Dispose();
    }
}
