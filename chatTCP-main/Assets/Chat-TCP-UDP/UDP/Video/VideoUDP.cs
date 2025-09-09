using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

public class VideoUDPPeer : MonoBehaviour
{
    [Header("UI")]
    public RawImage localRawImage;
    public RawImage remoteRawImage;

    [Header("Network")]
    public string remoteIP = "127.0.0.1";
    public int remotePort = 6000;
    public int localPort = 6001; // puerto para recibir
    public int maxChunkSize = 60000; // bytes por UDP (seguro < 65507)
    public int jpgQuality = 30; // 0-100

    private WebCamTexture cam;
    private UdpClient udp;
    private IPEndPoint remoteEndPoint;

    // para enviar throttling
    public float sendIntervalSeconds = 0.2f; // enviar cada 0.2s (5 FPS) - ajústalo

    private uint nextFrameId = 1;

    // recepción: map frameId -> lista de chunks
    private class FrameBuffer
    {
        public int totalChunks;
        public Dictionary<int, byte[]> chunks = new Dictionary<int, byte[]>();
        public DateTime firstReceived = DateTime.UtcNow;
    }
    private Dictionary<uint, FrameBuffer> receiveBuffers = new Dictionary<uint, FrameBuffer>();
    private object recvLock = new object();

    private void Start()
    {
        // Inicializar cámara local
        cam = new WebCamTexture();
        localRawImage.texture = cam;
        cam.Play();

        // Inicializar UDP
        udp = new UdpClient(localPort);
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteIP), remotePort);
        udp.BeginReceive(ReceiveCallback, null);

        // Ensure dispatcher exists
        var _ = MainThreadDispatcher.Instance;

        // Start send loop
        InvokeRepeating(nameof(CaptureAndSend), 0.1f, sendIntervalSeconds);
    }

    private void CaptureAndSend()
    {
        if (cam == null || !cam.didUpdateThisFrame) return;

        // Crear Texture2D desde WebCamTexture
        Texture2D tex = new Texture2D(cam.width, cam.height, TextureFormat.RGB24, false);
        tex.SetPixels(cam.GetPixels());
        tex.Apply();

        // Encode to JPG with quality
        var jpgBytes = tex.EncodeToJPG(jpgQuality);
        Destroy(tex);

        // Fragmentación
        int bytesLeft = jpgBytes.Length;
        int offset = 0;
        int chunkSize = maxChunkSize;
        int totalChunks = (jpgBytes.Length + chunkSize - 1) / chunkSize;
        uint frameId = nextFrameId++;

        for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            int thisChunkSize = Math.Min(chunkSize, bytesLeft);
            byte[] packet = new byte[4 + 2 + 2 + thisChunkSize];
            // header: frameId (4 bytes)
            Array.Copy(BitConverter.GetBytes(frameId), 0, packet, 0, 4);
            // totalChunks (ushort)
            Array.Copy(BitConverter.GetBytes((ushort)totalChunks), 0, packet, 4, 2);
            // chunkIndex (ushort)
            Array.Copy(BitConverter.GetBytes((ushort)chunkIndex), 0, packet, 6, 2);
            // payload
            Array.Copy(jpgBytes, offset, packet, 8, thisChunkSize);

            try
            {
                udp.Send(packet, packet.Length, remoteEndPoint);
            }
            catch (Exception ex)
            {
                Debug.LogError("Send error: " + ex);
            }

            offset += thisChunkSize;
            bytesLeft -= thisChunkSize;
        }

        Debug.Log($"Sent frame {frameId} ({jpgBytes.Length} bytes) in {totalChunks} chunks");
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
        byte[] data;
        try
        {
            data = udp.EndReceive(ar, ref any);
        }
        catch (ObjectDisposedException) { return; }
        catch (Exception ex) { Debug.LogError("Udp receive error: " + ex); return; }
        finally
        {
            try { udp.BeginReceive(ReceiveCallback, null); } catch { }
        }

        if (data.Length < 8) return; // header mínimo

        uint frameId = BitConverter.ToUInt32(data, 0);
        ushort totalChunks = BitConverter.ToUInt16(data, 4);
        ushort chunkIndex = BitConverter.ToUInt16(data, 6);
        int payloadLen = data.Length - 8;

        lock (recvLock)
        {
            if (!receiveBuffers.TryGetValue(frameId, out FrameBuffer fb))
            {
                fb = new FrameBuffer() { totalChunks = totalChunks };
                receiveBuffers[frameId] = fb;
            }

            fb.chunks[chunkIndex] = new byte[payloadLen];
            Array.Copy(data, 8, fb.chunks[chunkIndex], 0, payloadLen);

            // comprobar si completo
            if (fb.chunks.Count == fb.totalChunks)
            {
                // ensamblar
                MemoryStream ms = new MemoryStream();
                for (int i = 0; i < fb.totalChunks; i++)
                {
                    if (!fb.chunks.TryGetValue(i, out byte[] part))
                    {
                        Debug.LogWarning($"Missing chunk {i} for frame {frameId}");
                        ms.Dispose();
                        receiveBuffers.Remove(frameId);
                        return;
                    }
                    ms.Write(part, 0, part.Length);
                }
                byte[] jpg = ms.ToArray();
                ms.Dispose();
                receiveBuffers.Remove(frameId);

                // Crear textura e actualizar en hilo principal
                MainThreadDispatcher.Instance.Enqueue(() =>
                {
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(jpg))
                    {
                        remoteRawImage.texture = tex;
                        remoteRawImage.SetNativeSize();
                    }
                    else
                    {
                        Debug.LogWarning("Failed to LoadImage remote frame");
                        Destroy(tex);
                    }
                });

                Debug.Log($"Received complete frame {frameId} ({jpg.Length} bytes)");
            }
        }
    }

    private void OnApplicationQuit()
    {
        try
        {
            CancelInvoke(nameof(CaptureAndSend));
            udp?.Close();
            cam?.Stop();
        }
        catch { }
    }
}
