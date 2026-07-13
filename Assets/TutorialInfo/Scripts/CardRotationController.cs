using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles card rotation input and presentation. Dragging remains the
/// responsibility of DraggableCard.
/// </summary>
public sealed class CardRotationController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Func<bool> canRotate;
    private Action<bool> rotationChanged;
    private bool pointerOver;
    private bool dragging;

    public bool IsRotated { get; private set; }

    public void Configure(
        bool initialRotation,
        Func<bool> canRotatePredicate,
        Action<bool> onRotationChanged)
    {
        canRotate = canRotatePredicate;
        rotationChanged = onRotationChanged;
        SetRotation(initialRotation, false);
    }

    public void SetDragging(bool value)
    {
        dragging = value;
    }

    private void Update()
    {
        if ((!dragging && !pointerOver) ||
            canRotate == null ||
            !canRotate() ||
            !Input.GetKeyDown(KeyCode.R))
        {
            return;
        }

        SetRotation(!IsRotated, true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
    }

    private void SetRotation(bool rotated, bool notify)
    {
        IsRotated = rotated;
        ApplyRotation(transform, rotated);
        if (notify)
        {
            rotationChanged?.Invoke(rotated);
        }
    }

    public static void ApplyRotation(Transform target, bool rotated)
    {
        if (target == null)
        {
            return;
        }

        target.localRotation = rotated
            ? Quaternion.Euler(0f, 0f, 180f)
            : Quaternion.identity;
    }
}
