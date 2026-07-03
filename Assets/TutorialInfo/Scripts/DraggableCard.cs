using UnityEngine;
using UnityEngine.EventSystems;

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

        transform.SetParent(transform.root);
        GetOrAddCanvasGroup().blocksRaycasts = false;
        isDragging = true;

        if (BoardManager.Instance != null && !BoardManager.IsActionCard(GetCardType()))
        {
            BoardManager.Instance.ShowPlacementHighlights(GetCardType(), false);
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

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        ReturnToHand();
        GetOrAddCanvasGroup().blocksRaycasts = true;
        BoardManager.Instance?.ClearPlacementHighlights();

        if (eventData.pointerEnter != null &&
            eventData.pointerEnter.CompareTag("BoardCell") &&
            eventData.pointerEnter.TryGetComponent(out CellComponent cell) &&
            BoardManager.Instance != null &&
            BoardManager.Instance.CanPlaceCardFromUI(cell.x, cell.y, GetCardType(), false))
        {
            BoardManager.Instance.TryPlaceCardFromUI(
                cell.x,
                cell.y,
                GetCardType(),
                false
            );
        }
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
}
