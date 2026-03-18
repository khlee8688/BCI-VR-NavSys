using LSL;
using LSL4Unity.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Profiling;

public class StimulusSender : MonoBehaviour
{
    private int m_iPos = 8;
    private int m_iMax = 24;
    byte[] m_Buf = new byte[24];

    //TCP instance
    public TcpClient m_clientSocket = new TcpClient();
    public NetworkStream readstream = default(NetworkStream);
    public BinaryWriter writer;

    public virtual bool open(string host, int portNo)
    {
        Int32 port = portNo;
        m_clientSocket.Connect(host, port);
        //m_clientSocket = new TcpClient(host, port);
        readstream = m_clientSocket.GetStream();
        //writer = new BinaryWriter(readstream);
        Array.Clear(m_Buf, 0x0, 24);
        return true;
    }

    // Close connection
    //JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in C#:
    //ORIGINAL LINE: public boolean close() throws Exception
    public virtual bool close()
    {
        readstream.Close();
        m_clientSocket.Close();
        return true;
    }

    // Send stimulation with a timestamp. 
    //JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in C#:
    //ORIGINAL LINE: public boolean send(System.Nullable<long> stimulation, System.Nullable<long> timestamp) throws Exception
    public void SendStimulation(byte stimulation)
    {
        if (m_clientSocket == null || !m_clientSocket.Connected || readstream == null)
        {
            Debug.LogWarning("TCP not connected");
            return;
        }

        m_Buf[m_iPos] = stimulation;
        readstream.Write(m_Buf, 0, 24);
    }

    //JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in C#:
    //ORIGINAL LINE: public String receive() throws Exception
    public virtual string receive()
    {
        byte[] data = new byte[256];

        // String to store the response ASCII representation.
        string responseData = string.Empty;

        // Read the first batch of the TcpServer response bytes.
        int bytes = readstream.Read(data, 0, data.Length);
        responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);

        return responseData;
    }

    public void allocate(int iLimit)
    {
        m_iPos = 0;
        m_iMax = iLimit;
        m_Buf = new byte[iLimit];
    }

    public void putLong(long s)
    {
        if ((m_iPos + 8) > m_iMax)
            return;
        byte[] buf = new byte[8];

        buf = BitConverter.GetBytes(s);

        m_Buf[m_iPos++] = buf[0];
        m_Buf[m_iPos++] = buf[1];
        m_Buf[m_iPos++] = buf[2];
        m_Buf[m_iPos++] = buf[3];
        m_Buf[m_iPos++] = buf[4];
        m_Buf[m_iPos++] = buf[5];
        m_Buf[m_iPos++] = buf[6];
        m_Buf[m_iPos++] = buf[7];
    }

    public void putLongZero()
    {
        if ((m_iPos + 8) > m_iMax)
            return;
        m_Buf[m_iPos++] = 0;
        m_Buf[m_iPos++] = 0;
        m_Buf[m_iPos++] = 0;
        m_Buf[m_iPos++] = 0;
        m_Buf[m_iPos++] = 0;
        m_Buf[m_iPos++] = 0;
        m_Buf[m_iPos++] = 0;
        m_Buf[m_iPos++] = 0;
    }
}