using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TMP_InputFieldを使うために必要

public class NetBootstrapper : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private GameObject nameInputPanel;

    //パネル
    [SerializeField] private GameObject lobbyPanel; // 接続ボタンがあるパネル
    [SerializeField] private GameObject gamePanel;  // 名前一覧やゲーム画面のパネル
    [SerializeField] private TMP_Text statusText;

    private bool isHosting; // ホストかクライアントかを一時保存するフラグ

    void Start()
    {
        if (nameInputPanel != null) nameInputPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(false);
        // 最初は入力欄を隠しておく
        if (nameInputField != null) nameInputField.gameObject.SetActive(false);

        // ボタンの処理を「直接接続」ではなく「名前入力画面の表示」に変更
        if (hostButton != null)
            hostButton.onClick.AddListener(() => PrepareConnection(true));
        if (clientButton != null)
            clientButton.onClick.AddListener(() => PrepareConnection(false));

        // 入力完了時（Enterキーを押した時）のイベント設定
        if (nameInputField != null)
            nameInputField.onSubmit.AddListener(OnNameInputSubmitted);
    }

    private void PrepareConnection(bool isHost)
    {
        isHosting = isHost;

        // 接続ボタンを隠して入力欄を出す
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);
        if (nameInputField != null) nameInputField.gameObject.SetActive(true);
    }

    private void OnNameInputSubmitted(string playerName)
    {
        // 名前を保存
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.SavedPlayerName = playerName;

        // パネルを切り替え！
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);
        if (statusText != null) statusText.text = "マッチング中...";

        // 接続処理
        if (isHosting) NetworkManager.Singleton.StartHost();
        else NetworkManager.Singleton.StartClient();
    }
}