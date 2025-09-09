using UnityEngine;
using TMPro;
using SFB; // StandaloneFileBrowser

public class TCPClientUIA : MonoBehaviour
{
    public int serverPort = 5555;
    public string serverAddress = "127.0.0.1";
    [SerializeField] private TCPClient _client;
    [SerializeField] private TMP_InputField messageInput;

    public void SendClientMessage()
    {
        if(!_client.isServerConnected)
        {
            Debug.Log("The client is not connected");
            return;
        }

        if(string.IsNullOrEmpty(messageInput.text)){
            Debug.Log("The chat entry is empty");
            return;
        }

        string message = messageInput.text;
        _client.SendData(message);
        messageInput.text = "";
    }

    public void ConnectClient()
    {
        _client.ConnectToServer(serverAddress, serverPort);
    }

    //  Método del botón 
    public void PickFileAndSend()
    {
        var paths = StandaloneFileBrowser.OpenFilePanel("Seleccionar archivo", "", "", false);
        if (paths.Length > 0)
        {
            string filePath = paths[0];
            _client.SendFile(filePath);
        }
    }
}
