using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetBootstrapper : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button nameConfirmButton;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private GameObject nameInputPanel;
    [SerializeField] private GameObject hostSetupPanel;
    [SerializeField] private GameObject joinSetupPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject matchingPanel;
    [SerializeField] private TMP_Text statusText;

    private bool selectedHost;

    private void Start()
    {
        if (nameInputPanel != null)
        {
            nameInputPanel.SetActive(false);
        }

        if (hostSetupPanel != null)
        {
            hostSetupPanel.SetActive(false);
        }

        if (joinSetupPanel != null)
        {
            joinSetupPanel.SetActive(false);
        }

        if (matchingPanel != null)
        {
            matchingPanel.SetActive(false);
        }

        if (hostButton != null)
        {
            hostButton.onClick.AddListener(ShowHostSetup);
        }

        if (clientButton != null)
        {
            clientButton.onClick.AddListener(ShowJoinSetup);
        }

        if (nameConfirmButton != null)
        {
            nameConfirmButton.onClick.AddListener(OnClick_ConfirmName);
        }

        if (nameInputField != null)
        {
            nameInputField.onSubmit.AddListener(_ => OnClick_ConfirmName());
        }
    }

    private void ShowHostSetup()
    {
        selectedHost = true;
        ShowNameInput();
    }

    private void ShowJoinSetup()
    {
        selectedHost = false;
        ShowNameInput();
    }

    public void OnClick_ConfirmName()
    {
        string playerName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            SetStatus("Enter your name.");
            return;
        }

        NetworkGameManager.Instance.SavedPlayerName = playerName;

        if (nameInputPanel != null)
        {
            nameInputPanel.SetActive(false);
        }

        if (selectedHost)
        {
            if (hostSetupPanel != null)
            {
                hostSetupPanel.SetActive(true);
            }

            SetStatus("Set a room password.");
            return;
        }

        if (joinSetupPanel != null)
        {
            joinSetupPanel.SetActive(true);
        }

        SetStatus("Enter the room ID and password.");
    }

    private void ShowNameInput()
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false);
        }

        if (nameInputPanel != null)
        {
            nameInputPanel.SetActive(true);
        }

        if (hostSetupPanel != null)
        {
            hostSetupPanel.SetActive(false);
        }

        if (joinSetupPanel != null)
        {
            joinSetupPanel.SetActive(false);
        }

        SetStatus("Enter your name.");

        if (nameInputField != null)
        {
            nameInputField.Select();
            nameInputField.ActivateInputField();
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
