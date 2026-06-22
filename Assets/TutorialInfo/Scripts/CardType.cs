using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SaboteurBoardManager : NetworkBehaviour
{
    //UI
    [Header("Board View")]
    [SerializeField] private Transform boardRoot;
    [SerializeField] private SaboteurBoardCardView cardPrefab;
    [SerializeField] private float cellSize = 120f;

    private NetworkList<SaboteurBoardCardState> placedCards;
    private readonly Dictionary<Vector2Int, SaboteurBoardCardView> spawnedCards = new Dictionary<Vector2Int, SaboteurBoardCardView>();

    //èâä˙âª
    private void Awake()
    {
        placedCards = new NetworkList<SaboteurBoardCardState>();
    }

    //
    public override void OnNetworkSpawn()
    {
        placedCards.OnListChanged += OnPlacedCardsChanged;

        if (IsServer && placedCards.Count == 0)
        {
            placedCards.Add(new SaboteurBoardCardState(0, 0, SaboteurCardType.Start, false, NetworkManager.ServerClientId));
        }

        RebuildBoardView();
    }

    public void TryPlaceCardFromUI(int x, int y)
    {
        RequestPlaceCardServerRpc(x, y, SaboteurCardType.PathStraight, false);
    }

    public void TryPlaceCardFromUI(int x, int y, SaboteurCardType cardType, bool rotated)
    {
        RequestPlaceCardServerRpc(x, y, cardType, rotated);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceCardServerRpc(int x, int y, SaboteurCardType cardType, bool rotated, ServerRpcParams rpcParams = default)
    {
        Vector2Int position = new Vector2Int(x, y);

        if (!CanPlaceCard(position))
        {
            return;
        }

        placedCards.Add(new SaboteurBoardCardState(
            x,
            y,
            cardType,
            rotated,
            rpcParams.Receive.SenderClientId));
    }

    private bool CanPlaceCard(Vector2Int position)
    {
        if (HasCardAt(position))
        {
            return false;
        }

        return HasCardAt(position + Vector2Int.up)
            || HasCardAt(position + Vector2Int.down)
            || HasCardAt(position + Vector2Int.left)
            || HasCardAt(position + Vector2Int.right);
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

    private void OnPlacedCardsChanged(NetworkListEvent<SaboteurBoardCardState> changeEvent)
    {
        if (changeEvent.Type == NetworkListEvent<SaboteurBoardCardState>.EventType.Add)
        {
            SpawnCardView(changeEvent.Value);
            return;
        }

        RebuildBoardView();
    }

    private void RebuildBoardView()
    {
        foreach (SaboteurBoardCardView cardView in spawnedCards.Values)
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

    private void SpawnCardView(SaboteurBoardCardState state)
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

        SaboteurBoardCardView cardView = Instantiate(cardPrefab, boardRoot);
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