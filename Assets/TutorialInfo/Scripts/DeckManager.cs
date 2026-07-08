//@breif
//山札の管理,cardの枚数上限設定
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class DeckManager : MonoBehaviour
{
    [System.Serializable]
    public struct CardLimit
    {
        public CardType type;
        public int maxCount; //上限
    }

    [SerializeField] private List<CardLimit> cardLimits = new List<CardLimit>();
    private Dictionary<CardType, int> currentCounts = new Dictionary<CardType, int>();
    private Dictionary<CardType, int> maxCounts = new Dictionary<CardType, int>();

    private void Awake()
    {
        // 上限設定を辞書にコピー
        foreach (var limit in cardLimits)
        {
            maxCounts[limit.type] = limit.maxCount;
            currentCounts[limit.type] = 0;
        }
    }

    
    public bool CanAddCard(CardType type)
    {
        if (!maxCounts.ContainsKey(type)) return true; // 上限設定がないものは無制限

        return currentCounts[type] < maxCounts[type];
    }


}



