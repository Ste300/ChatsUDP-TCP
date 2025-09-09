using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UDPChatServer : MonoBehaviour
{
    public int listenPort = 5000;
    private UdpClient udp;
    private IPEndPoint anyEP = new IPEndPoint(IPAddress.Any, 0);
    private HashSet<string> clientKeys = new HashSet<string>(); // "ip:port"
    private object clientsLock = new object();

    void Start()
    {
        udp = new UdpClient(listenPort);
        udp.BeginReceive(ReceiveCallback, null);
        Debug.Log($"UDP Chat Server listening on port {listenPort}");
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        byte[] data;
        try
        {
            data = udp.EndReceive(ar, ref sender);
        }
        catch (ObjectDisposedException) { return; }
        catch (Exception ex) { Debug.LogError("Server receive error: " + ex); return; }
        finally
        {
            try { udp.BeginReceive(ReceiveCallback, null); } catch { }
        }

        string senderKey = $"{sender.Address}:{sender.Port}";
        lock (clientsLock)
        {
            if (!clientKeys.Contains(senderKey)) clientKeys.Add(senderKey);
        }

        // Reenviar a todos los clientes conocidos (incluyendo emisor para que vea su mensaje)
        List<IPEndPoint> targets = new List<IPEndPoint>();
        lock (clientsLock)
        {
            foreach (var k in clientKeys)
            {
                var parts = k.Split(':');
                if (parts.Length != 2) continue;
                if (IPAddress.TryParse(parts[0], out IPAddress addr) && int.TryParse(parts[1], out int p))
                    targets.Add(new IPEndPoint(addr, p));
            }
        }

        foreach (var t in targets)
        {
            try { udp.Send(data, data.Length, t); }
            catch (Exception ex) { Debug.LogError("Server forward error: " + ex); }
        }
    }

    void OnApplicationQuit()
    {
        try { udp?.Close(); } catch { }
    }
}
