using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public abstract class AbstractUdpListener : MonoBehaviour
{
    // open connection at a specific port
    // close connection
    // concurrent queue for received messages and send messages
    // separate threads for receiving and sending messages

    public abstract int Port { get; }
    private ConcurrentQueue<Action> receivedMessages = new ConcurrentQueue<Action>();
    private ConcurrentQueue<Action> sendMessages = new ConcurrentQueue<Action>();
    private ConcurrentDictionary<int, UdpClient> udpClients = new ConcurrentDictionary<int, UdpClient>();
    private ConcurrentStack<IPEndPoint> endPoints = new ConcurrentStack<IPEndPoint>();
    private IPEndPoint endPoint;
    private Thread receiveThread;
    private Thread sendThread;
    private bool _isRunning;

    public UdpClient Client
    {
        get
        {
            if (udpClients.TryGetValue(0, out UdpClient client))
            {
                return client;
            }
            else
            {
                client = new UdpClient();
                udpClients.TryAdd(0, client);
            }

            return client;

        }
    }

    public IPEndPoint LastPoint
    {
        get
        {
            if (endPoints.TryPop(out IPEndPoint endPoint))
            {
                return endPoint;
            }
            return null;
        }
        set
        {
            endPoints.Push(value);
        }
    }

    public virtual void StartConnection()
    {
        Debug.Log("Starting UDP listener on port: " + Port);
        _isRunning = true;
        endPoint = new IPEndPoint(IPAddress.Any, Port);
        Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        Client.Client.Bind(endPoint);
        receiveThread = new Thread(HandleIncomingMessages);
        sendThread = new Thread(HandleOutgoingMessages);
        receiveThread.Start();
        sendThread.Start();

        OnConnectionOpen();

    }

    protected virtual void Update()
    {
        while (receivedMessages.TryDequeue(out Action action))
        {
            action();
        }
    }

    private void HandleIncomingMessages()
    {
        try
        {
            while (_isRunning)
            {
                if (Client.Available > 0)
                {
                    byte[] data = Client.Receive(ref endPoint);
                    string message = System.Text.Encoding.UTF8.GetString(data);
                    LastPoint = endPoint;
                    receivedMessages.Enqueue(() => OnMessageReceived(message));
                }
            }
            receivedMessages.Enqueue(() => OnConnectionClosed());
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            Disconnect();
        }
    }

    private void HandleOutgoingMessages()
    {
        try
        {
            while (_isRunning)
            {
                if (sendMessages.TryDequeue(out Action action))
                {
                    action();
                }
            }

            receivedMessages.Enqueue(() => OnConnectionClosed());
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            Disconnect();
        }
    }


    public void ResponseMessage(string message)
    {
        sendMessages.Enqueue(() => QueueSendMessage(message));
    }

    private void QueueSendMessage(string message)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
        var endpoint = LastPoint;
        if (endpoint == null)
        {
            Debug.Log("No endpoint to send message to");
            return;
        }
        Client.Send(data, data.Length, endpoint);
    }

    public void Disconnect()
    {
        receivedMessages.Enqueue(() => OnConnectionClosed());

        Client.Close();
        receiveThread.Abort();
        sendThread.Abort();
        _isRunning = false;
    }

    protected abstract void OnConnectionOpen();
    protected abstract void OnMessageReceived(string message);
    protected abstract void OnConnectionClosed();


}