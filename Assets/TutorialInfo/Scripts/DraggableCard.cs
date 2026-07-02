using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Transform parentAfterDrag;

    public void OnBeginDrag(PointerEventData eventData)
    {
        // ドラッグ開始時に親をCanvasの最前面へ移動
        parentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        GetComponent<CanvasGroup>().blocksRaycasts = false; // 下にあるオブジェクトを透過させる
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position; // マウスに追従
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(parentAfterDrag);
        GetComponent<CanvasGroup>().blocksRaycasts = true;

        // ドロップ先にボードがあるか判定
        if (eventData.pointerEnter != null && eventData.pointerEnter.CompareTag("BoardCell"))
        {
            // ボードのセル（マス）に置く処理へ
            BoardManager.Instance.TryPlaceCardFromUI(
                eventData.pointerEnter.GetComponent<CellComponent>().x,
                eventData.pointerEnter.GetComponent<CellComponent>().y
            );
        }
    }
}