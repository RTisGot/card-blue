using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
        Debug.Log("[RuleUI] NextPage clicked");

        if (currentPage < pages.Length - 1)
        {
            currentPage++;
            ShowPage(currentPage);
        }
    }

    public void BackPage()
    {
        Debug.Log("[RuleUI] BackPage clicked");

        if (currentPage > 0)
        {
            currentPage--;
            ShowPage(currentPage);
            return;
        }
    }

    void ShowPage(int page)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
            {
                pages[i].SetActive(i == page);
            }
        }

        if (backButton != null)
        {
            backButton.gameObject.SetActive(page > 0);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(page < pages.Length - 1);
        }
    }
}
