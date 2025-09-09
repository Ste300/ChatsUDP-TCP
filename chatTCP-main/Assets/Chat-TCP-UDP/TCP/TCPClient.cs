using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class TCPClient : MonoBehaviour
{
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private byte[] receiveBuffer;

    [SerializeField] private ChatUIManager chatUI; // <- asigna el del panel IZQUIERDO
    public bool isServerConnected;

    public void ConnectToServer(string ipAddress, int port)
    {
        tcpClient = new TcpClient();
        tcpClient.Connect(IPAddress.Parse(ipAddress), port);
        networkStream = tcpClient.GetStream();
        receiveBuffer = new byte[tcpClient.ReceiveBufferSize];
        networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
        isServerConnected = true;
    }

    public void SendData(string message)
    {
        if (!isServerConnected || networkStream == null) return;

        try
        {
            byte[] sendBytes = Encoding.UTF8.GetBytes(message);
            networkStream.Write(sendBytes, 0, sendBytes.Length);
            networkStream.Flush();
            Debug.Log("Sent to server: " + message);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Client send failed: " + e.Message);
        }
    }

    private void ReceiveData(IAsyncResult result)
{
    try
    {
        int bytesRead = networkStream.EndRead(result);
        if (bytesRead <= 0)
        {
            // conexión cerrada
            isServerConnected = false; // en cliente; en servidor usa connectedClient?.Close();
            tcpClient?.Close(); // cliente: ajusta según tu script
            return;
        }

        byte[] chunk = new byte[bytesRead];
        Array.Copy(receiveBuffer, chunk, bytesRead);

        // Buscamos si el chunk empieza por la cabecera "FILE|"
        // NOTA: si tu cabecera es pequeña, casi siempre estará completa en este primer chunk
        string chunkText = System.Text.Encoding.UTF8.GetString(chunk);

        if (chunkText.StartsWith("FILE|"))
        {
            // encontrar posición del '\n' que termina la cabecera
            int newlineIndex = Array.IndexOf(chunk, (byte)'\n');
            if (newlineIndex < 0)
            {
                Debug.LogError("Cabecera incompleta recibida (no contiene '\\n').");
                // no manejamos caso fragmentado de cabecera en esta versión simple
                networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
                return;
            }

            // leer cabecera completa (bytes 0..newlineIndex-1)
            string header = System.Text.Encoding.UTF8.GetString(chunk, 0, newlineIndex); // sin '\n'
            string[] parts = header.Split('|');
            if (parts.Length < 3)
            {
                Debug.LogError("Cabecera FILE mal formada: " + header);
                networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
                return;
            }

            string fileName = parts[1];
            if (!int.TryParse(parts[2], out int fileSize))
            {
                Debug.LogError("Tamaño de archivo inválido en cabecera: " + parts[2]);
                networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
                return;
            }

            // Calculamos si en este mismo chunk ya vienen bytes de contenido después de la cabecera
            int contentStartInChunk = newlineIndex + 1;
            int contentInChunk = bytesRead - contentStartInChunk;

            byte[] fileData = new byte[fileSize];
            int written = 0;

            if (contentInChunk > 0)
            {
                int toCopy = Math.Min(contentInChunk, fileSize);
                Array.Copy(chunk, contentStartInChunk, fileData, 0, toCopy);
                written += toCopy;
            }

            // Si no hemos recibido todo el archivo, leer el resto (bloqueante)
            while (written < fileSize)
            {
                int read = networkStream.Read(fileData, written, fileSize - written);
                if (read <= 0)
                {
                    Debug.LogWarning("Lectura devolvió EOF antes de completar el archivo.");
                    break;
                }
                written += read;
            }

            // Llamar a la UI en hilo principal
            ChatUIManager.Instance?.AddFileMessage(fileName, fileData, /*sender:*/ ChatSender.Server /* o Client según contexto */);

        }
        else
        {
            // Es mensaje de texto normal: evitar mostrar cabeceras por accidente
            string message = System.Text.Encoding.UTF8.GetString(chunk, 0, bytesRead);
            ChatUIManager.Instance?.AddMessage(message, ChatSender.Server /* o Client según contexto */);
        }

        // seguir leyendo
        networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
    }
    catch (Exception ex)
    {
        Debug.LogError("Error recibiendo datos: " + ex.Message);
        // Manejo de cierre de conexión según tu script
    }
}


    public void SendFile(string filePath)
    {
        if (networkStream == null) return;

        byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
        string fileName = System.IO.Path.GetFileName(filePath);
        int fileSize = fileBytes.Length;

        // Cabecera
        string header = $"FILE|{fileName}|{fileSize}\n";
        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        networkStream.Write(headerBytes, 0, headerBytes.Length);

        // Datos
        networkStream.Write(fileBytes, 0, fileBytes.Length);
        networkStream.Flush();

        Debug.Log($"Archivo enviado: {fileName} ({fileSize} bytes)");
        ChatUIManager.Instance?.AddMessage($"Archivo enviado: {fileName}", ChatSender.Server);
    }

    private void OnDestroy()
    {
        try { networkStream?.Close(); tcpClient?.Close(); } catch { }
    }
}
