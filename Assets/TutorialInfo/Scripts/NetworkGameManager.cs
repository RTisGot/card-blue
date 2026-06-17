using UnityEngine;

public class NetworkGameManager : MonoBehaviour
{
   public static NetworkGameManager Instance { get; private set; } //他のすくりぷとからアクセス

    public string SavedPlayerName { get; set; } = "Player"; //プレイヤー名を保存

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            DontDestroyOnLoad(gameObject); //シーンをまたいでも破棄されないようにする
        }
        else
        {
                       Destroy(gameObject); //すでにインスタンスが存在する場合は破棄する
        }
    }
}
