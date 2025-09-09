using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class VideoUDPServer : MonoBehaviour
{
    public int listenPort = 6000;
    private UdpClient udp;
    private IPEndPoint anyEndpoint = new IPEndPoint(IPAddress.Any, 0);
    private HashSet<string> clientEndpoints = new HashSet<string>(); // "ip:port"
    private object clientsLock = new object();

    void Start()
    {
        udp = new UdpClient(listenPort);
        udp.BeginReceive(ReceiveCallback, null);
        Debug.Log("VideoUDPServer listening on " + listenPort);
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

        string key = $"{sender.Address}:{sender.Port}";
        lock (clientsLock)
        {
            if (!clientEndpoints.Contains(key)) clientEndpoints.Add(key);
        }

        // Reenviar a todos los demás clientes
        List<IPEndPoint> targets = new List<IPEndPoint>();
        lock (clientsLock)
        {
            foreach (var k in clientEndpoints)
            {
                if (k == key) continue; // no reenviar al emisor
                var parts = k.Split(':');
                if (parts.Length != 2) continue;
                if (IPAddress.TryParse(parts[0], out IPAddress addr) && int.TryParse(parts[1], out int p))
                {
                    targets.Add(new IPEndPoint(addr, p));
                }
            }
        }

        foreach (var t in targets)
        {
            try { udp.Send(data, data.Length, t); }
            catch (Exception ex) { Debug.LogError("Server forward error: " + ex); }
        }

        // también puedes procesar mensajes de control (HELLO/WELCOME) aquí si quieres
    }

    private void OnApplicationQuit()
    {
        try { udp?.Close(); } catch { }
    }
}
