using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class CardView : MonoBehaviour
{
    [SerializeField] private Image cardArtImage;
    public CardType CardType { get; private set; }

    [Header("Special Sprites")]
    [SerializeField] private Sprite startCard;

    [Header("Goal Sprites")]
    [FormerlySerializedAs("backSideSprite")]
    [SerializeField] private Sprite goalBackSprite;
    [FormerlySerializedAs("goalCard")]
    [SerializeField] private Sprite goalGoldSprite;
    [SerializeField] private Sprite goalEmptyTopSprite;
    [SerializeField] private Sprite goalEmptyMiddleSprite;
    [SerializeField] private Sprite goalEmptyBottomSprite;

    [Header("Path Sprites")]
    [SerializeField] private Sprite LRdeadend;      // ¶‰Es‚«~‚Ü‚è
    [SerializeField] private Sprite UDdeadend;      // ã‰ºs‚«~‚Ü‚è
    [SerializeField] private Sprite LDdeadend;      // ‰º¶s‚«~‚Ü‚è
    [SerializeField] private Sprite UDLRdeadend;    // ‘S•ûŒüs‚«~‚Ü‚è
    [SerializeField] private Sprite UDLdeadend;     // ‰EˆÈŠOs‚«~‚Ü‚è
    [SerializeField] private Sprite RDdeadend;      // ‰º‰Es‚«~‚Ü‚è
    [SerializeField] private Sprite Ldeadend;       // ¶s‚«~‚Ü‚è
    [SerializeField] private Sprite Udeadend;       // ãs‚«~‚Ü‚è
    [SerializeField] private Sprite ULRdeadend;     // ‰ºˆÈŠOs‚«~‚Ü‚è

    [Header("Load Sprites")]
    [SerializeField] private Sprite URload;         // Lš-1
    [SerializeField] private Sprite DLload;         // Lš-2
    [SerializeField] private Sprite DRload;         // Lš-3
    [SerializeField] private Sprite ULload;         // Lš-4
    [SerializeField] private Sprite UDRload;        // Tš˜H(c)-1
    [SerializeField] private Sprite UDLload;        // Tš˜H(c)-2
    [SerializeField] private Sprite DLRload;        // Tš˜H(‰¡)-1
    [SerializeField] private Sprite ULRload;        // Tš˜H(‰¡)-2
    [SerializeField] private Sprite UDLRload;       // \š˜H
    [SerializeField] private Sprite UDload;         // ’¼ü(c)
    [SerializeField] private Sprite LRload;         // ’¼ü(‰¡)

    [Header("On-Props Sprites")]
    [SerializeField] private Sprite DLloadHandkerchief;     // Lš-2 ƒnƒ“ƒJƒ`
    [SerializeField] private Sprite DRloadPocketwatch;      // Lš-3 ‰ù’†Œv
    [SerializeField] private Sprite ULRloadBucket;          // Tš˜H(‰¡)-2 ƒoƒPƒc
    [SerializeField] private Sprite ULRloadMouse;           // Tš˜H(‰¡)-2 ƒlƒYƒ~
    [SerializeField] private Sprite UDLloadPot;             // Tš˜H(c)-2 “ç
    [SerializeField] private Sprite UDLloadShoe;            // Tš˜H(c)-2 ŒC
    [SerializeField] private Sprite UDLRloadBone;           // \š˜H œ
    [SerializeField] private Sprite UDLRloadCup;            // \š˜H ƒJƒbƒv
    [SerializeField] private Sprite UDLRloadHat;            // \š˜H –Xq
    [SerializeField] private Sprite LRloadSpoon;            // ’¼ü(‰¡) ƒXƒv[ƒ“
    [SerializeField] private Sprite LRloadWheel;            // ’¼ü(‰¡) Ô—Ö
    [SerializeField] private Sprite UDloadBucket;           // ’¼ü(c) ƒoƒPƒc
    [SerializeField] private Sprite UDLdeadendHedgehog;     // ‰EˆÈŠOs‚«~‚Ü‚è ƒnƒŠƒlƒYƒ~
    [SerializeField] private Sprite UDdeadendFriedegg;      // ã‰ºs‚«~‚Ü‚è –Ú‹ÊÄ‚«

    [Header("Action Sprites")]
    [SerializeField] private Sprite Lanternrepaire;
    [SerializeField] private Sprite Lanternban;
    [SerializeField] private Sprite Pickaxerepaire;
    [SerializeField] private Sprite Pickaxeban;
    [SerializeField] private Sprite railcarrepaire;
    [SerializeField] private Sprite railcarban;
    [SerializeField] private Sprite treasuremap;
    [SerializeField] private Sprite Fallingrocks;
    [SerializeField] private Sprite PickaxeOrRailcarrepaire;
    [SerializeField] private Sprite PickaxeOrLanternrepaire;
    [SerializeField] private Sprite LanternOrRailcarrepaire;

    public void SetCard(CardType type, bool isFlipped)
    {
        CardType = type;

        if (cardArtImage == null)
        {
            cardArtImage = GetComponent<Image>();
        }

        if (cardArtImage == null)
        {
            Debug.LogWarning("CardView has no Image assigned: " + name);
            return;
        }

        if (!isFlipped)
        {
            // — –Ê‚Ì‰æ‘œ‚ğ•\¦‚·‚é
            cardArtImage.sprite = goalBackSprite;
            return;
        }


        switch (type)
        {
            case CardType.Start: cardArtImage.sprite = startCard; break;
            case CardType.GoalGold:
                cardArtImage.sprite = goalGoldSprite;
                break;
            case CardType.GoalEmpty:
            case CardType.GoalEmptyMiddle:
                cardArtImage.sprite = goalEmptyMiddleSprite != null ? goalEmptyMiddleSprite : Fallingrocks;
                break;
            case CardType.GoalEmptyTop:
                cardArtImage.sprite = goalEmptyTopSprite != null ? goalEmptyTopSprite : Fallingrocks;
                break;
            case CardType.GoalEmptyBottom:
                cardArtImage.sprite = goalEmptyBottomSprite != null ? goalEmptyBottomSprite : Fallingrocks;
                break;
            // --- Deadend cards ---
            case CardType.LRdeadend: cardArtImage.sprite = LRdeadend; break;
            case CardType.LDdeadend: cardArtImage.sprite = LDdeadend; break;
            case CardType.UDLRdeadend: cardArtImage.sprite = UDLRdeadend; break;
            case CardType.UDLdeadend: cardArtImage.sprite = UDLdeadend; break;
            case CardType.RDdeadend: cardArtImage.sprite = RDdeadend; break;
            case CardType.Ldeadend: cardArtImage.sprite = Ldeadend; break;
            case CardType.Udeadend: cardArtImage.sprite = Udeadend; break;
            case CardType.ULRdeadend: cardArtImage.sprite = ULRdeadend; break;
            case CardType.UDdeadend: cardArtImage.sprite = UDdeadend; break;

            // --- Load cards ---
            case CardType.UDLload: cardArtImage.sprite = UDLload; break;
            case CardType.DRload: cardArtImage.sprite = DRload; break;
            case CardType.URload: cardArtImage.sprite = URload; break;
            case CardType.DLload: cardArtImage.sprite = DLload; break;
            case CardType.ULload: cardArtImage.sprite = ULload; break;
            case CardType.UDload: cardArtImage.sprite = UDload; break;
            case CardType.DLRload: cardArtImage.sprite = DLRload; break;
            case CardType.ULRload: cardArtImage.sprite = ULRload; break;
            case CardType.UDRload: cardArtImage.sprite = UDRload; break;
            case CardType.LRload: cardArtImage.sprite = LRload; break;
            case CardType.UDLRload: cardArtImage.sprite = UDLRload; break;

            // --- On-Props cards ---
            case CardType.DLloadHandkerchief: cardArtImage.sprite = DLloadHandkerchief; break;
            case CardType.DRloadPocketwatch: cardArtImage.sprite = DRloadPocketwatch; break;
            case CardType.ULRloadBucket: cardArtImage.sprite = ULRloadBucket; break;
            case CardType.ULRloadMouse: cardArtImage.sprite = ULRloadMouse; break;
            case CardType.UDLloadPot: cardArtImage.sprite = UDLloadPot; break;
            case CardType.UDLloadShoe: cardArtImage.sprite = UDLloadShoe; break;
            case CardType.UDLRloadBone: cardArtImage.sprite = UDLRloadBone; break;
            case CardType.UDLRloadCup: cardArtImage.sprite = UDLRloadCup; break;
            case CardType.UDLRloadHat: cardArtImage.sprite = UDLRloadHat; break;
            case CardType.LRloadSpoon: cardArtImage.sprite = LRloadSpoon; break;
            case CardType.LRloadWheel: cardArtImage.sprite = LRloadWheel; break;
            case CardType.UDloadBucket: cardArtImage.sprite = UDloadBucket; break;
            case CardType.UDLdeadendHedgehog: cardArtImage.sprite = UDLdeadendHedgehog; break;
            case CardType.UDdeadendFriedegg: cardArtImage.sprite = UDdeadendFriedegg; break;

            // --- Action cards ---
            case CardType.Lanternrepaire: cardArtImage.sprite = Lanternrepaire; break;
            case CardType.Lanternban: cardArtImage.sprite = Lanternban; break;
            case CardType.Pickaxerepaire: cardArtImage.sprite = Pickaxerepaire; break;
            case CardType.Pickaxeban: cardArtImage.sprite = Pickaxeban; break;
            case CardType.Railcarrepaire: cardArtImage.sprite = railcarrepaire; break;
            case CardType.Railcarban: cardArtImage.sprite = railcarban; break;
            case CardType.PickaxeOrRailcarrepaire: cardArtImage.sprite = PickaxeOrRailcarrepaire; break;
            case CardType.PickaxeOrLanternrepaire: cardArtImage.sprite = PickaxeOrLanternrepaire; break;
            case CardType.LanternOrRailcarrepaire: cardArtImage.sprite = LanternOrRailcarrepaire; break;
            case CardType.ActionMap: cardArtImage.sprite = treasuremap; break;
            case CardType.Treasuremap: cardArtImage.sprite = treasuremap; break;
            case CardType.ActionFallingRocks: cardArtImage.sprite = Fallingrocks; break;
            case CardType.Fallingrocks: cardArtImage.sprite = Fallingrocks; break;

            default:
                Debug.LogWarning("Unknown card type: " + type);
                break;
        }
    }
}
