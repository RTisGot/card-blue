using UnityEngine;

public class NetworkGameManager : MonoBehaviour
{
    private static NetworkGameManager _instance;
    public static NetworkGameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("NetworkGameManager");
                _instance = go.AddComponent<NetworkGameManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public string SavedPlayerName { get; set; } = "Player";

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
}