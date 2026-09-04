using UnityEngine;

public class NetworkGameManager : MonoBehaviour
{
    private static NetworkGameManager instance;

    public static NetworkGameManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject(nameof(NetworkGameManager));
                instance = go.AddComponent<NetworkGameManager>();
                DontDestroyOnLoad(go);
            }

            return instance;
        }
    }

    public string SavedPlayerName { get; set; } = "Player";
    public string CurrentRoomId { get; set; } = string.Empty;
    public string CurrentRoomPassword { get; set; } = string.Empty;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (instance != this)
        {
            Destroy(gameObject);
        }
    }
}
