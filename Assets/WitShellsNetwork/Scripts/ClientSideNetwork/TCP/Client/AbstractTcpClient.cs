using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public abstract class AbstractTcpClient : MonoBehaviour
{
    private const int MAX_RECONNECT_ATTEMPTS = 3;
    // open connection at given address and port
    // close connection
    // concurrent queue for received messages and send messages
    // separate threads for receiving and sending messages

    private ConcurrentQueue<Action> _receivedMessages = new ConcurrentQueue<Action>();
    private ConcurrentQueue<string> _sendMessages = new ConcurrentQueue<string>();
    private ConcurrentDictionary<int, TcpClient> _tcpClients = new ConcurrentDictionary<int, TcpClient>();
    private ConcurrentDictionary<int, bool> _isRunning = new ConcurrentDictionary<int, bool>();
    private Thread receiveThread;
    private Thread sendThread;

    private IPEndPoint _endPoint = null;
    private int _reconnectAttempts = 0;
    private bool _tryToConnect = false;

    public TcpClient Client
    {
        get
        {
            if (_tcpClients.TryGetValue(0, out TcpClient client))
            {
                return client;
            }
            else
            {
                client = new TcpClient();
                _tcpClients.TryAdd(0, client);
            }

            return client;

        }
    }

    public bool IsRunning
    {
        get
        {
            if (_isRunning.TryGetValue(0, out bool isRunning))
            {
                return isRunning;
            }
            else
            {
                _isRunning.TryAdd(0, false);
            }

            return false;
        }
        private set
        {
            _isRunning.TryUpdate(0, value, !value);
        }
    }

    public NetworkStream Stream => Client.GetStream();

    protected virtual void FixedUpdate()
    {
        if (_receivedMessages.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    void OnApplicationQuit()
    {
        CloseConnection();
    }

    public void ConnectToServer(string ip, int port)
    {
        try
        {
            if (Client.Connected)
            {
                return;
            }

            Client.Connect(ip, port);

            InitializeServer();
        }
        catch (Exception e)
        {
            Debug.Log(e);
            OnConnectionFailToOpen();
        }
    }

    private void ReconnectToServer()
    {
        if (_reconnectAttempts < MAX_RECONNECT_ATTEMPTS && _endPoint != null)
        {
            _reconnectAttempts++;
            ConnectToServer(_endPoint);
        }
        else
        {
            Debug.Log("Max reconnect attempts reached");
            QueueIncoming(() => OnConnectionFailToOpen());
        }
    }

    public void ConnectToServer(IPEndPoint endPoint)
    {
        if (_tryToConnect)
        {
            Debug.Log("Already trying to connect");
            return;
        }
        try
        {
            _tryToConnect = true;
            StartCoroutine(ConnectToServerAsync(endPoint));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Connection failed");
            Debug.Log(e);
            QueueIncoming(() => OnConnectionFailToOpen());
        }
    }

    private IEnumerator ConnectToServerAsync(IPEndPoint endPoint)
    {
        if (Client.Connected)
        {
            Debug.Log("Client already connected");
            yield break;
        }

        Debug.Log("Connecting to Tcp Server: " + endPoint);
        var time = Time.time;

        Task connectTask = Client.ConnectAsync(endPoint.Address, endPoint.Port);

        while (!connectTask.IsCompleted)
        {
            // Debug.Log("Connecting... " + (Time.time - time));
            yield return null;
        }

        if (!Client.Connected)
        {
            Debug.Log($"Failed to connect to server {endPoint} Time: {Time.time - time} retries: {_reconnectAttempts}");

            if (connectTask.IsFaulted)
            {
                Debug.Log("Faulted");
                Debug.Log(connectTask.Exception);
            }

            _tryToConnect = false;
            ReconnectToServer();
            yield break;
        }

        Debug.Log("Connected to Tcp Server: " + endPoint + " Time: " + (Time.time - time));
        _endPoint = endPoint;
        OnConnectionOpen();
        yield return new WaitForSeconds(2);
        InitializeServer();
    }

    private void InitializeServer()
    {
        IsRunning = true;
        _tryToConnect = false;

        receiveThread = new Thread(Incoming);
        receiveThread.Start();

        sendThread = new Thread(OutGoing);
        sendThread.Start();

        // OnConnectionOpen();
    }

    private void Incoming()
    {
        try
        {
            Debug.Log("Incoming thread started " + Client.Connected + " " + IsRunning);
            using StreamReader reader = new StreamReader(Client.GetStream());

            while (Client.Connected)
            {
                try
                {
                    string message = reader.ReadLine();

                    if (!string.IsNullOrEmpty(message))
                    {
                        QueueIncoming(() => OnMessageReceived(message));
                    }
                }
                catch (IOException)
                {
                    // ignored
                }
            }

            QueueIncoming(() => OnConnectionClosed());
        }
        catch (InvalidOperationException e)
        {
            // ignored
            Debug.LogException(e);
            ReconnectToServer();
        }
        catch (Exception e)
        {
            Debug.Log("Incoming thread exception\n" + e);
        }
        finally
        {
            CloseConnection();
        }
    }

    private void QueueIncoming(Action action)
    {
        _receivedMessages.Enqueue(action);
    }

    private void OutGoing()
    {
        try
        {
            Debug.Log("OutGoing thread started " + Client.Connected + " " + IsRunning);
            StreamWriter writer = new StreamWriter(Client.GetStream()) { AutoFlush = true };
            Debug.Log("Connection opened " + Client.Connected + " " + IsRunning);
            // while (Client.Connected && IsRunning)
            while (Client.Connected)
            {
                try
                {
                    if (_sendMessages.TryDequeue(out string message))
                    {
                        writer.WriteLine(message);
                    }
                }
                catch (IOException)
                {
                    // ignored
                }
            }

            Debug.Log("Connection closed ");
        }
        catch (InvalidOperationException e)
        {
            // ignored
            Debug.Log(Client.Connected + " " + IsRunning);
            Debug.Log("Invalid Operation " + e);
            ReconnectToServer();
        }
        catch (Exception e)
        {
            Debug.Log("OutGoing thread exception\n" + e);
        }
        finally
        {
            CloseConnection();
        }
    }

    public void SendMessageToServer(string message)
    {
        _sendMessages.Enqueue(message);
    }


    protected void CloseConnection()
    {
        if (Client != null)
        {
            Client.Close();
            Client.Dispose();
        }

        _tcpClients.Clear();

        IsRunning = false;

        QueueIncoming(() => OnConnectionClosed());
    }


    // abstract methods
    protected abstract void OnMessageReceived(string message);
    protected abstract void OnConnectionOpen();
    protected abstract void OnConnectionClosed();
    protected abstract void OnConnectionFailToOpen();
}