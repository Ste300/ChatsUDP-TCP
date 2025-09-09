using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatUI : MonoBehaviour
{
    [Header("Network")]
    public UDPChatClient client;

    [Header("UI")]
    public TMP_InputField inputField;
    public Button sendButton;
    public Transform contentContainer; // donde se instancian mensajes
    public GameObject messagePrefab; // prefab que contiene TextMeshProUGUI llamado "MessageText"

    [Header("Options")]
    public string localUsername = "You";

    void Start()
    {
        if (client == null) Debug.LogError("Asignar UDPChatClient en el inspector.");
        if (sendButton != null) sendButton.onClick.AddListener(OnSendClicked);

        client.OnMessageReceived += OnNetworkMessage;
        client.username = localUsername;
        client.StartClient();
    }

    void OnSendClicked()
    {
        string text = inputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;
        client.SendChatMessage(text);
        inputField.text = "";
        // opcional: mostrar eco local inmediatamente (si servidor no lo reenvía rápido)
        //CreateMessage(localUsername, text, true);
    }

    void OnNetworkMessage(string from, string message)
    {
        // from y message vienen en hilo principal ya (por dispatcher)
        bool isLocal = from == localUsername;
        CreateMessage(from, message, isLocal);
    }

    void CreateMessage(string from, string message, bool isLocal)
    {
        if (messagePrefab == null) return;
        GameObject go = Instantiate(messagePrefab, contentContainer);
        var textComp = go.GetComponentInChildren<TMP_Text>();
        if (textComp != null)
        {
            textComp.text = $"<b>{from}:</b> {message}";
            if (isLocal)
            {
                textComp.alignment = TextAlignmentOptions.Right;
            }
            else
            {
                textComp.alignment = TextAlignmentOptions.Left;
            }
        }
        
    }
}
