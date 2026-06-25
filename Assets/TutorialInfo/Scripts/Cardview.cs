using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
    [SerializeField] private Sprite Ddeadend;
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

    public void SetCard(CardState state)
    {
        switch (state.cardType)
        {
            // 道・行き止まりのケース
            case CardType.PathStraight: cardArtImage.sprite = LRload; break;
            case CardType.PathCorner: cardArtImage.sprite = RDload; break;
            case CardType.PathTJunction: cardArtImage.sprite = UDLload; break;
            case CardType.PathCross: cardArtImage.sprite = UDLRload; break;
            case CardType.DeadEnd: cardArtImage.sprite = Ddeadend; break;

            // アクションカードのケース
            case CardType.ActionRepair: cardArtImage.sprite = Lanternrepaire; break;
            // 必要に応じて他のActionのケースも追加してください

            default:
                Debug.LogWarning("未定義のカードタイプです: " + state.cardType);
                break;
        }
    }
}