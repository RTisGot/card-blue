using UnityEngine;
using UnityEngine.SceneManagement;

public class resultSceneManager : MonoBehaviour
{
public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
