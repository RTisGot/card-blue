using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonScaleEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("サイズ設定")]
    [SerializeField] private float targetScale = 1.1f;  //最大の大きさ
    [SerializeField] private float duration = 0.1f;     //変化時間

    private Vector3 defaultScale;
    private bool isHovering = false;

    private void Awake()
    {
        defaultScale=transform.localScale;
    }

    //カーソルがボタンに触れた時
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        _ = ScaleAnimation(defaultScale * targetScale);
    }

    //カーソルがボタンから離れた時
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        _ = ScaleAnimation(defaultScale);
    }

    private async Task ScaleAnimation(Vector3 target)
    {
        float elapsedTime = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsedTime < duration)
        {
            if (target != defaultScale && !isHovering) break;
            if (target == defaultScale && isHovering) break;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.Lerp(startScale, target, t);
            await Task.Yield();
        }

        transform.localScale = target;
    }

    private void OnDisable()
    {
        transform.localScale = defaultScale;
    }
}
