using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    [SerializeField] private Image cardArtImage;

    [Header("Path Sprites")]
    [SerializeField] private Sprite LRdeadend;
    [SerializeField] private Sprite LDdeadend;
    [SerializeField] private Sprite UDLRdeadend;
    [SerializeField] private Sprite UDLdeadend;
    [SerializeField] private Sprite RDdeadend;
    [SerializeField] private Sprite Ldeadend;
    [SerializeField] private Sprite Udeadend;
    [SerializeField] private Sprite ULRdeadend;

    [Header("Load Sprites")]
    [SerializeField] private Sprite UDLload;
    [SerializeField] private Sprite DRload;
    [SerializeField] private Sprite URload;
    [SerializeField] private Sprite DLload;
    [SerializeField] private Sprite ULload;
    [SerializeField] private Sprite UDload;
    [SerializeField] private Sprite DLRload;
    [SerializeField] private Sprite ULRload;
    [SerializeField] private Sprite LRload;
    [SerializeField] private Sprite UDLRload;
    [SerializeField] private Sprite RDload;

    [Header("Action Sprites")]
    [SerializeField] private Sprite Lanternrepaire;
    [SerializeField] private Sprite Lanternban;
    [SerializeField] private Sprite Pickaxerepaire;
    [SerializeField] private Sprite Pickaxeban;
    [SerializeField] private Sprite railcarrepaire;
    [SerializeField] private Sprite railcarban;
    [SerializeField] private Sprite treasuremap;
    [SerializeField] private Sprite Fallingrocks;

    public void SetCard(CardType type)
    {
        if (cardArtImage == null)
        {
            cardArtImage = GetComponent<Image>();
        }

        if (cardArtImage == null)
        {
            Debug.LogWarning("CardViewにImageが設定されていません: " + name);
            return;
        }

        switch (type)
        {
            // --- Deadend系 ---
            case CardType.LRdeadend: cardArtImage.sprite = LRdeadend; break;
            case CardType.LDdeadend: cardArtImage.sprite = LDdeadend; break;
            case CardType.UDLRdeadend: cardArtImage.sprite = UDLRdeadend; break;
            case CardType.UDLdeadend: cardArtImage.sprite = UDLdeadend; break;
            case CardType.RDdeadend: cardArtImage.sprite = RDdeadend; break;
            case CardType.Ldeadend: cardArtImage.sprite = Ldeadend; break;
            case CardType.Udeadend: cardArtImage.sprite = Udeadend; break;
            case CardType.ULRdeadend: cardArtImage.sprite = ULRdeadend; break;

            // --- Load(道)系 ---
            case CardType.UDLload: cardArtImage.sprite = UDLload; break;
            case CardType.DRload: cardArtImage.sprite = DRload; break;
            case CardType.URload: cardArtImage.sprite = URload; break;
            case CardType.DLload: cardArtImage.sprite = DLload; break;
            case CardType.ULload: cardArtImage.sprite = ULload; break;
            case CardType.UDload: cardArtImage.sprite = UDload; break;
            case CardType.DLRload: cardArtImage.sprite = DLRload; break;
            case CardType.ULRload: cardArtImage.sprite = ULRload; break;
            case CardType.LRload: cardArtImage.sprite = LRload; break;
            case CardType.UDLRload: cardArtImage.sprite = UDLRload; break;
            case CardType.RDload: cardArtImage.sprite = RDload; break;

            // --- Action系 ---
            case CardType.Lanternrepaire: cardArtImage.sprite = Lanternrepaire; break;
            case CardType.Lanternban: cardArtImage.sprite = Lanternban; break;
            case CardType.Pickaxerepaire: cardArtImage.sprite = Pickaxerepaire; break;
            case CardType.Pickaxeban: cardArtImage.sprite = Pickaxeban; break;
            case CardType.Railcarrepaire: cardArtImage.sprite = railcarrepaire; break;
            case CardType.Railcarban: cardArtImage.sprite = railcarban; break;
            case CardType.ActionMap: cardArtImage.sprite = treasuremap; break;
            case CardType.ActionFallingRocks: cardArtImage.sprite = Fallingrocks; break;

            default:
                Debug.LogWarning("未定義のカードタイプです: " + type);
                break;
        }
    }
}
