using System;
using System.Diagnostics;
using System.Net.Sockets;
using UnityEngine;

public class SSVEPStimulusSender : MonoBehaviour
{
    public const ulong SessionStartMarker = 33027;
    public const ulong SessionEndMarker = 33028;
    public const ulong FlickerStartMarker = 33025;
    public const ulong FlickerEndMarker = 33026;

    [Header("OpenViBE TCP Tagging")]
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 12140;

    private TcpClient client;
    private NetworkStream stream;

    public bool IsConnected =>
        client != null && client.Connected && stream != null;

    public bool Open()
    {
        if (IsConnected)
            return true;

        Close();

        try
        {
            client = new TcpClient
            {
                NoDelay = true,
                SendTimeout = 1000
            };

            client.Connect(host, port);
            stream = client.GetStream();

            UnityEngine.Debug.Log(
                $"[SSVEP TCP] Connected to {host}:{port}"
            );

            return true;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError(
                $"[SSVEP TCP] Connection failed: {exception.Message}"
            );

            Close();
            return false;
        }
    }

    public bool SendStimulation(ulong stimulationId)
    {
        if (!IsConnected && !Open())
            return false;

        try
        {
            ulong stimulationTime = GetOpenVibeTimestamp();
            const ulong stimulationDuration = 0;

            byte[] packet = new byte[24];
            WriteUInt64LittleEndian(packet, 0, stimulationTime);
            WriteUInt64LittleEndian(packet, 8, stimulationId);
            WriteUInt64LittleEndian(packet, 16, stimulationDuration);

            stream.Write(packet, 0, packet.Length);

            UnityEngine.Debug.Log(
                $"[SSVEP TCP] Sent marker {stimulationId} " +
                $"(fixed timestamp={stimulationTime})"
            );

            return true;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError(
                $"[SSVEP TCP] Send failed: {exception.Message}"
            );

            Close();
            return false;
        }
    }

    public bool SendSessionStart()
    {
        return SendStimulation(SessionStartMarker);
    }

    public bool SendSessionEnd()
    {
        return SendStimulation(SessionEndMarker);
    }

    public bool SendFlickerStart()
    {
        return SendStimulation(FlickerStartMarker);
    }

    public bool SendFlickerEnd()
    {
        return SendStimulation(FlickerEndMarker);
    }

    public void Close()
    {
        if (stream != null)
        {
            stream.Close();
            stream = null;
        }

        if (client != null)
        {
            client.Close();
            client = null;
        }
    }

    private static ulong GetOpenVibeTimestamp()
    {
        double seconds = Stopwatch.GetTimestamp()
            / (double)Stopwatch.Frequency;

        return (ulong)(seconds * 4294967296.0);
    }

    private static void WriteUInt64LittleEndian(
        byte[] destination,
        int offset,
        ulong value
    )
    {
        for (int i = 0; i < 8; i++)
            destination[offset + i] = (byte)(value >> (i * 8));
    }

    private void OnDestroy()
    {
        Close();
    }
}
