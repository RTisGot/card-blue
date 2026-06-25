using UnityEngine;
using UnityEngine.UI;

public class RuleUI : MonoBehaviour
{
    public GameObject[] pages;

    public Button nextButton;
    public Button backButton;

    int currentPage = 0;

    void Start()
    {
        ShowPage(0);
    }

    public void NextPage()
    {
        if (currentPage < pages.Length - 1)
        {
            currentPage++;
            ShowPage(currentPage);
        }
    }

    public void BackPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            ShowPage(currentPage);
        }
    }

    void ShowPage(int page)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == page);
        }

        backButton.gameObject.SetActive(page > 0);
        nextButton.gameObject.SetActive(page < pages.Length - 1);
    }
}