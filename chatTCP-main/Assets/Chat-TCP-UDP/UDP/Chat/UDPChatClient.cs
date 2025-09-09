using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UDPChatClient : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    public int serverPort = 5000;
    public int localPort = 0; // 0 para asignar aleatorio, o poner puerto fijo
    public string username = "User";

    private UdpClient udp;
    private IPEndPoint serverEndPoint;
    private IPEndPoint anyEP = new IPEndPoint(IPAddress.Any, 0);
    public event Action<string, string> OnMessageReceived; // username, message

    public void StartClient()
    {
        try
        {
            udp = new UdpClient(localPort);
            serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            // "Connect" facilita Send()
            udp.Connect(serverEndPoint);
            udp.BeginReceive(ReceiveCallback, null);
            Debug.Log($"UDP Chat Client started (local port {((IPEndPoint)udp.Client.LocalEndPoint).Port}) -> server {serverIP}:{serverPort}");
            // Opcional: notificar al servidor con un "HELLO" para que lo registre
            SendRaw($"{username}|{username} se ha conectado.");
        }
        catch (Exception ex)
        {
            Debug.LogError("StartClient error: " + ex);
        }
    }

    public void SendChatMessage(string message)
    {
        // payload: username|message
        SendRaw($"{username}|{message}");
    }

    private void SendRaw(string payload)
    {
        if (udp == null) return;
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            udp.Send(bytes, bytes.Length); // conectado a serverEndPoint
        }
        catch (Exception ex) { Debug.LogError("Send error: " + ex); }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        byte[] data;
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        try
        {
            data = udp.EndReceive(ar, ref remote);
        }
        catch (ObjectDisposedException) { return; }
        catch (Exception ex) { Debug.LogError("Receive error: " + ex); return; }
        finally
        {
            try { udp.BeginReceive(ReceiveCallback, null); } catch { }
        }

        string text = Encoding.UTF8.GetString(data);
        // Expect format "username|message"
        int sep = text.IndexOf('|');
        string from = sep > 0 ? text.Substring(0, sep) : "unknown";
        string msg = sep > 0 ? text.Substring(sep + 1) : text;

        // Encolar para el hilo principal
        MainThreadDispatcher.Instance.Enqueue(() =>
        {
            OnMessageReceived?.Invoke(from, msg);
        });
    }

    void OnApplicationQuit()
    {
        try
        {
            // notificar desconexión opcional
            SendRaw($"{username}|{username} se ha desconectado.");
            udp?.Close();
        }
        catch { }
    }
}
