using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum CardType
{
    Start,
    PathStraight,
    PathCorner,
    PathTJunction,
    PathCross,
    DeadEnd
}

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

    private NetworkList<ulong> connectedPlayers = new NetworkList<ulong>();
    private readonly Dictionary<Vector2Int, CardView> spawnedCards = new Dictionary<Vector2Int, CardView>();

    //初期化
    private void Awake() 
    {
        placedCards = new NetworkList<CardState>();
    }

    //player名の共有
    private NetworkList<ulong> connectedPlayers;

    //ネットワーク処理
    public override void OnNetworkSpawn()
    {
        connectedPlayers = new NetworkList<ulong>();
        if (IsServer)
        {
            // 接続されている全員をリストに追加
            foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
            {
                connectedPlayers.Add(client);
            }
        }
        placedCards.OnListChanged += OnPlacedCardsChanged;

        if (IsServer && placedCards.Count == 0)
        {
            placedCards.Add(new CardState(0, 0, CardType.Start, false, NetworkManager.ServerClientId));
        }

        RebuildBoardView();
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

        cardView.SetCard(state);
        spawnedCards.Add(position, cardView);
    }

    public override void OnNetworkDespawn()
    {
        placedCards.OnListChanged -= OnPlacedCardsChanged;
    }

    private void OnDestroy()
    {
        placedCards?.Dispose();
    }
}