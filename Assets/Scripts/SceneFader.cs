using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneFader : MonoBehaviour
{
    [SerializeField] private Image fadeImage;

    [Header("フェードインフェードアウトの速度調整")]
    [SerializeField] private float fadeInDuration = 2.0f;   //フェードインにかかる時間
    [SerializeField] private float fadeOutDuration = 0.5f;  //フェードアウトにかかる時間

    private void Start()
    {
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = false;
        }

        //シーン開始時自動でフェードイン
        _ = FadeInAsync();
    }

    //ボタンを押されたらシーンを呼び出す
    public void ChangeScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        Debug.Log($"[SceneFader] Button clicked: {sceneName}");
        _ = TransitionAndLoadScene(NormalizeSceneName(sceneName));
    }

    public async Task TransitionAndLoadScene(string sceneName)
    {
        //1.フェードアウト(透明から黒へ)
        await FadeOutAsync();

        //2.シーン切り替え
        Debug.Log($"[SceneFader] LoadScene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    private string NormalizeSceneName(string sceneName)
    {
        string normalized = sceneName.Trim();
        string lowerName = normalized.ToLowerInvariant();

        switch (lowerName)
        {
            case "title":
            case "titlescene":
            case "title scene":
                return "TitleScene";
            case "game":
            case "gamescene":
            case "game scene":
            case "lobby":
                return "LobbyScene";
            case "maingame":
            case "maingamescene":
            case "main game":
            case "main game scene":
                return "GameScene";
            case "rule":
            case "rules":
            case "rulescene":
            case "rule scene":
                return "RuleScene";
            default:
                return normalized;
        }
    }

    private async Task FadeInAsync()
    {
        if (fadeImage == null) return;

        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(true);
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsedTime / fadeInDuration);
            fadeImage.color = color;
            await Task.Yield(); //1フレーム待つ
        }

        color.a = 0f;
        fadeImage.color = color;
        fadeImage.gameObject.SetActive(false); //クリックを妨げないように非アクティブ化
    }

    private async Task FadeOutAsync()
    {
        if (fadeImage == null) return;

        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(true);
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, elapsedTime / fadeOutDuration);
            fadeImage.color = color;
            await Task.Yield();
        }

        color.a = 1f;
        fadeImage.color = color;
    }
}
