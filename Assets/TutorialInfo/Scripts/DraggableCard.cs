using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class DraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public CardType cardType;
    public bool isRotated;
    private Transform parentAfterDrag;
    private CardView cardView;
    private RectTransform rectTransform;
    private bool isDragging;
    private int siblingIndexBeforeDrag;
    private Vector2 anchoredPositionBeforeDrag;
    private static DraggableCard pendingPlacementCard;

    private void Awake()
    {
        cardView = GetComponent<CardView>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanUseCard())
        {
            return;
        }

        pendingPlacementCard = null;

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
            BoardManager.Instance.ShowPlacementHighlights(GetCardType(), isRotated);
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

        if (BoardManager.Instance != null && !BoardManager.IsActionCard(GetCardType()))
        {
            BoardManager.Instance.UpdatePlacementHighlights(GetCardType(), isRotated);
        }
    }

    //カードのドロップ処理
    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("[Log] OnEndDrag が呼ばれました");
        GameObject raycastObject = GetRaycastObjectAtPointer(eventData);
        if (raycastObject != null)
        {
            Debug.Log($"[Debug] 今当たっているオブジェクト: {raycastObject.name}");
            Debug.Log($"[Debug] 当たったオブジェクトのTag: {raycastObject.tag}");
        }
        else
        {
            Debug.Log("[Debug] 何にも当たっていません");
        }
        isDragging = false;
        GetOrAddCanvasGroup().blocksRaycasts = true;

        
       
        CellComponent cell = GetCellAtPointer(eventData);

        //  セル上にドロップできたか判定
        if (cell != null)
        {
            Debug.Log($"[Client] ドロップ成功: {cell.x}, {cell.y}");

            // サーバーへの依頼 (BoardManager.Instance を使用)
            bool placeRequested = BoardManager.Instance.TryPlaceCardFromUI(cell.x, cell.y, GetCardType(), this.isRotated);

            // 配置のハイライトを消す
            BoardManager.Instance.ClearPlacementHighlights();
            if (placeRequested)
            {
                pendingPlacementCard = this;
                return;
            }

            Debug.Log("[Log] 配置できないセルです。手札に戻します。");
            ReturnToHand();
            return;
        }

        // 3. セル以外（手札など）にドロップされた場合
        Debug.Log("[Log] セル以外にドロップされました。手札に戻します。");
        BoardManager.Instance?.ClearPlacementHighlights();
        ReturnToHand();
    }


    private GameObject GetRaycastObjectAtPointer(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            return eventData.pointerCurrentRaycast.gameObject;
        }

        RaycastResult result = GetFirstRaycastResultAtPointer(eventData);
        return result.gameObject;
    }

    private CellComponent GetCellAtPointer(PointerEventData eventData)
    {
        foreach (RaycastResult result in GetRaycastResultsAtPointer(eventData))
        {
            if (result.gameObject == gameObject || result.gameObject.transform.IsChildOf(transform))
            {
                continue;
            }

            CellComponent raycastCell = result.gameObject.GetComponentInParent<CellComponent>();
            if (raycastCell != null)
            {
                return raycastCell;
            }
        }

        foreach (CellComponent cell in FindObjectsOfType<CellComponent>())
        {
            RectTransform cellRect = cell.GetComponent<RectTransform>();
            if (cellRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    cellRect,
                    eventData.position,
                    eventData.pressEventCamera))
            {
                return cell;
            }
        }

        return null;
    }

    private RaycastResult GetFirstRaycastResultAtPointer(PointerEventData eventData)
    {
        if (EventSystem.current == null)
        {
            return default;
        }

        List<RaycastResult> results = GetRaycastResultsAtPointer(eventData);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject != gameObject &&
                !result.gameObject.transform.IsChildOf(transform))
            {
                return result;
            }
        }

        return default;
    }

    private List<RaycastResult> GetRaycastResultsAtPointer(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        if (EventSystem.current != null)
        {
            EventSystem.current.RaycastAll(eventData, results);
        }

        return results;
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
        if (pendingPlacementCard == this)
        {
            pendingPlacementCard = null;
        }

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


    public static void ReturnPendingPlacementToHand()
    {
        if (pendingPlacementCard == null)
        {
            return;
        }

        pendingPlacementCard.ReturnToHand();
        pendingPlacementCard = null;
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
