using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class DraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private Transform parentAfterDrag;
    private CardView cardView;
    private RectTransform rectTransform;
    private bool isDragging;
    private int siblingIndexBeforeDrag;
    private Vector2 anchoredPositionBeforeDrag;

    private void Awake()
    {
        cardView = GetComponent<CardView>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        CanvasGroup cg = GetOrAddCanvasGroup();
        cg.blocksRaycasts = false;

        if (!CanUseCard())
        {
            return;
        }

        // Move the dragged card to the top-level canvas while dragging.
        parentAfterDrag = transform.parent;
        siblingIndexBeforeDrag = transform.GetSiblingIndex();
        if (rectTransform != null)
        {
            anchoredPositionBeforeDrag = rectTransform.anchoredPosition;
        }

        
        GetOrAddCanvasGroup().blocksRaycasts = false;
        isDragging = true;

        if (BoardManager.Instance != null && !BoardManager.IsActionCard(GetCardType()))
        {
            Debug.Log("ハイライトを呼び出します");
            BoardManager.Instance.ShowPlacementHighlights(GetCardType(), false);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestReparentServerRpc(NetworkObjectReference parentRef)
    {
        if (parentRef.TryGet(out NetworkObject parentObj))
        {
            //親子関係を設定(networkobject)
            GetComponent<NetworkObject>().TrySetParent(parentObj.transform);

            ResetPositionClientRpc(); // クライアント側で位置をリセット
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        transform.position = eventData.position;
    }

    //カードのドロップ処理
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
       

        //セル上にドロップされたか確認
        if (eventData.pointerEnter != null &&
            eventData.pointerEnter.CompareTag("BoardCell") &&
            eventData.pointerEnter.TryGetComponent(out CellComponent cell))
        {
            //サーバーへ「配置したい」という依頼を投げる
            BoardManager.Instance.TryPlaceCardFromUI(cell.x, cell.y, GetCardType(), false);

            Debug.Log($"[Client] ドロップしました: {cell.x}, {cell.y}");
            // 成功した前提で、クライアント側では手札から消す処理
            return;
        }
        GetOrAddCanvasGroup().blocksRaycasts = true;
        BoardManager.Instance?.ClearPlacementHighlights();
        // セル以外にドロップされたら手札に戻す
        ReturnToHand();
    }
    [ServerRpc(RequireOwnership = false)]
    public void PlaceCardServerRpc(int x, int y, CardType cardType, bool rotated)
    {
        // ここで BoardManager に配置処理を依頼する
        // BoardManager に「サーバー側での配置」を依頼するメソッドを別途作る必要があります
        BoardManager.Instance.ExecutePlacementOnServer(x, y, cardType, rotated);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanUseCard())
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left && BoardManager.IsActionCard(GetCardType()))
        {
            BoardManager.Instance.TryPlayActionCardFromUI(GetCardType());
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            BoardManager.Instance.TryDiscardAndDrawFromUI(GetCardType());
        }
    }

    private CardType GetCardType()
    {
        if (cardView == null)
        {
            cardView = GetComponent<CardView>();
        }

        return cardView != null ? cardView.CardType : CardType.PathStraight;
    }

    private bool CanUseCard()
    {
        return BoardManager.Instance != null && BoardManager.Instance.IsLocalPlayerTurn();
    }

    private void ReturnToHand()
    {
        if (parentAfterDrag == null)
        {
            return;
        }

        transform.SetParent(parentAfterDrag);
        transform.SetSiblingIndex(siblingIndexBeforeDrag);

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = anchoredPositionBeforeDrag;
        }
    }

    private CanvasGroup GetOrAddCanvasGroup()
    {
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    // 配置確定時にマスの中心にぴったり合わせる
    [ClientRpc]
    private void ResetPositionClientRpc()
    {
        // 配置確定時にマスの中心にぴったり合わせる
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one; // マスの大きさに合わせる
    }
}
