using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PlayerDisplay : NetworkBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_SpriteAsset toolIconSpriteAsset;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color currentTurnColor = new Color(1f, 0.84f, 0.29f, 0.25f);
    [SerializeField] private Color dragTargetColor = new Color(1f, 0.93f, 0.25f, 0.52f);

    public ulong ClientId { get; private set; }
    private bool isCurrentTurn;
    private bool isDragTarget;

    private void Awake()
    {
        EnsureReferences();
    }

    public void UpdateName(string playerName)
    {
        SetPlayer(ClientId, playerName, false, false, false, false);
    }

    public void UpdateName(string playerName, bool isCurrentTurn)
    {
        SetPlayer(ClientId, playerName, isCurrentTurn, false, false, false);
    }

    public void SetPlayer(ulong clientId, string playerName, bool isCurrentTurn)
    {
        SetPlayer(clientId, playerName, isCurrentTurn, false, false, false);
    }

    public void SetPlayer(
        ulong clientId,
        string playerName,
        bool isCurrentTurn,
        bool isLanternBroken,
        bool isPickaxeBroken,
        bool isRailcarBroken)
    {
        ClientId = clientId;
        this.isCurrentTurn = isCurrentTurn;
        EnsureReferences();

        if (nameText != null)
        {
            nameText.richText = true;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.enableWordWrapping = true;
            nameText.fontSizeMin = 18f;
            nameText.fontSizeMax = Mathf.Max(nameText.fontSize, 26f);
            nameText.enableAutoSizing = true;
            nameText.lineSpacing = 6f;
            nameText.margin = new Vector4(14f, 6f, 14f, 6f);
            nameText.outlineColor = new Color32(0, 0, 0, 230);
            nameText.outlineWidth = 0.18f;

            string playerLine = isCurrentTurn
                ? $"<color=#FFD54A><b>\u25B6 {playerName} \u306E\u30BF\u30FC\u30F3</b></color>"
                : playerName;
            string actionIconLine = GetActionIconLine(isLanternBroken, isPickaxeBroken, isRailcarBroken);
            nameText.text = $"{playerLine}\n{actionIconLine}";
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = GetBackgroundColor();
            backgroundImage.raycastTarget = true;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            BoardManager.Instance?.TrySelectPendingActionTarget(ClientId);
        }
    }

    public void SetDragTargetHighlighted(bool highlighted)
    {
        if (isDragTarget == highlighted)
        {
            return;
        }

        isDragTarget = highlighted;
        EnsureReferences();

        if (backgroundImage != null)
        {
            backgroundImage.color = GetBackgroundColor();
        }

        if (nameText != null)
        {
            nameText.fontStyle = highlighted ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private Color GetBackgroundColor()
    {
        if (isDragTarget)
        {
            return dragTargetColor;
        }

        return isCurrentTurn ? currentTurnColor : normalColor;
    }

    private static string GetActionIconLine(bool isLanternBroken, bool isPickaxeBroken, bool isRailcarBroken)
    {
        string lanternIcon = isLanternBroken ? "LanternBroken" : "LanternNormal";
        string pickaxeIcon = isPickaxeBroken ? "PickaxeBroken" : "PickaxeNormal";
        string railcarIcon = isRailcarBroken ? "RailcarBroken" : "RailcarNormal";

        return $"<size=70%><sprite name=\"{lanternIcon}\"> " +
               $"<sprite name=\"{pickaxeIcon}\"> " +
               $"<sprite name=\"{railcarIcon}\"></size>";
    }

    private void EnsureReferences()
    {
        if (nameText == null)
        {
            nameText = GetComponentInChildren<TMP_Text>();
        }

        if (nameText != null && toolIconSpriteAsset != null)
        {
            nameText.spriteAsset = toolIconSpriteAsset;
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }
    }
}
