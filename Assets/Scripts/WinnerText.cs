using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class WinnerText : MonoBehaviour
{
    public TMP_Text firstPlaceText;
    public TMP_Text topPlaceText;
    public TMP_Text secondPlaceText;
    public TMP_Text thirdPlaceText;

    [System.Serializable]
    public class Player
    {
        public string playerName;
        public int score;
    }

    public List<Player> players = new List<Player>();


    void Start()
    {
        ShowRanking();
    }


    void ShowRanking()
    {
        List<Player> ranking = players.OrderByDescending(p => p.score).ToList();


        if(ranking.Count == 0)
            return;


        firstPlaceText.text = ranking[0].playerName + " WIN!";


        if (ranking.Count >= 1)
        {
            topPlaceText.text = ranking[0].playerName + " : " + ranking[0].score + " Gold";
        }


        if (ranking.Count >= 2)
        {
            secondPlaceText.text = ranking[1].playerName + " : " + ranking[1].score + " Gold";
        }


        if (ranking.Count >= 3)
        {
            thirdPlaceText.text = ranking[2].playerName + " : " + ranking[2].score + " Gold";
        }
    }
}