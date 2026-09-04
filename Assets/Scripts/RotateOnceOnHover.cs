using UnityEngine;
using UnityEngine.EventSystems;

public class RotateOnceOnHover : MonoBehaviour, IPointerEnterHandler
{
    [Header("‰ñ“]‚³‚¹‚éUI")]
    public RectTransform targetObject;

    [Header("‰ñ“]ŽžŠÔ")]
    public float rotateDuration = 0.5f;

    private bool isRotating;
    private float timer;
    private float startAngle;
    private float endAngle;

    private void Update()
    {
        if (!isRotating || targetObject == null)
            return;

        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / rotateDuration);

        float angle = Mathf.Lerp(startAngle, endAngle, t);

        targetObject.localEulerAngles = new Vector3(0, 0, angle);

        if (t >= 1f)
        {
            isRotating = false;
            targetObject.localEulerAngles = new Vector3(0, 0, endAngle);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isRotating || targetObject == null)
            return;

        startAngle = targetObject.localEulerAngles.z;
        endAngle = startAngle + 360f;

        timer = 0f;
        isRotating = true;
    }
}