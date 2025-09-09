using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections.Concurrent;
using SFB; // StandaloneFileBrowser

public enum ChatSender { Client, Server }

public class ChatUIManager : MonoBehaviour
{
    public static ChatUIManager Instance;

    [Header("UI Elements")]
    [SerializeField] private Transform contentPanel;
    [SerializeField] private GameObject clientMessagePrefab;
    [SerializeField] private GameObject serverMessagePrefab;
    [SerializeField] private GameObject fileMessagePrefab; // 🔹 nuevo

    [Header("Settings")]
    [SerializeField] private int maxMessages = 7;

    private readonly Queue<GameObject> messageQueue = new Queue<GameObject>();
    private readonly ConcurrentQueue<System.Action> mainThreadActions = new ConcurrentQueue<System.Action>();

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    public void AddMessage(string message, ChatSender sender)
    {
        mainThreadActions.Enqueue(() =>
        {
            GameObject prefab = sender == ChatSender.Client ? clientMessagePrefab : serverMessagePrefab;
            if (prefab == null) return;

            GameObject go = Instantiate(prefab, contentPanel);
            TMP_Text txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = message;

            RegisterMessage(go);
        });
    }

    public void AddFileMessage(string fileName, byte[] fileData, ChatSender sender)
{
    mainThreadActions.Enqueue(() =>
    {
        if (fileMessagePrefab == null || contentPanel == null) return;

        GameObject go = Instantiate(fileMessagePrefab, contentPanel);

        // Buscamos el TMP en el prefab
        TMP_Text txt = go.transform.Find("FileNameText")?.GetComponent<TMP_Text>();
        if (txt == null) txt = go.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = fileName;
        else Debug.LogWarning("No se encontró FileNameText en el prefab de archivo.");

        // Configurar botón
        Transform btnT = go.transform.Find("DownloadButton");
        if (btnT == null) btnT = go.GetComponentInChildren<Button>()?.transform;
        if (btnT != null)
        {
            Button btn = btnT.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                var path = StandaloneFileBrowser.SaveFilePanel("Guardar archivo", "", fileName, "");
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllBytes(path, fileData);
                    Debug.Log("Archivo guardado en: " + path);
                }
            });
        }
        else
        {
            Debug.LogWarning("No se encontró DownloadButton en el prefab de archivo.");
        }

        RegisterMessage(go);
    });
}


    private void RegisterMessage(GameObject go)
    {
        messageQueue.Enqueue(go);
        if (messageQueue.Count > maxMessages)
        {
            GameObject old = messageQueue.Dequeue();
            if (old != null) Destroy(old);
        }

        var scroll = contentPanel.GetComponentInParent<ScrollRect>();
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }
    }
}
