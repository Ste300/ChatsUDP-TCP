using UnityEngine;
using TMPro;
using SFB;

public class TCPServerUIA : MonoBehaviour
{
    public int serverPort = 5555;
    [SerializeField] private TCPServer _server;
    [SerializeField] private TMP_InputField messageInput;

    public void SendServerMessage()
    {
        if(!_server.isServerRunning){
            Debug.Log("The server is not running");
            return;
        }

        if(string.IsNullOrEmpty(messageInput.text)){
            Debug.Log("The chat entry is empty");
            return;
        }

        string message = messageInput.text;
        _server.SendData(message);
        messageInput.text = "";
    }

    public void StartServer()
    {
        _server.StartServer(serverPort);
    }

    // 🔹 Método del botón 📎
    public void PickFileAndSend()
    {
        var paths = StandaloneFileBrowser.OpenFilePanel("Seleccionar archivo", "", "", false);
        if (paths.Length > 0)
        {
            string filePath = paths[0];
            _server.SendFile(filePath);
        }
    }
}
