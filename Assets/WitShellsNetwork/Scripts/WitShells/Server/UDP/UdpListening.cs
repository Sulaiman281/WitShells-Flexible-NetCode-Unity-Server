using System.Net;
using UnityEngine;
using UnityEngine.Events;

public class UdpListening : AbstractUdpListener
{
    public override int Port { get; }

    [Header("Events")]
    public UnityEvent<string> onMessageReceived;

    protected override void OnConnectionClosed()
    {
        Debug.Log("Connection closed");
    }

    protected override void OnConnectionOpen()
    {
        Debug.Log("Connection opened on port: " + Port);
    }

    protected override void OnMessageReceived(string message)
    {
        Debug.Log("Received: " + message);
    }
}


public struct Request
{
    public string securityKey;
    public string deviceUniqueIdentifier;
    public string ipAddress;
    public static string ToJson(Request request)
    {
        return JsonUtility.ToJson(request);
    }
    public static Request FromJson(string json)
    {
        return JsonUtility.FromJson<Request>(json);
    }
}

public struct Response
{
    public string message;
    public int code;

    public static Response FromJson(string json)
    {
        return JsonUtility.FromJson<Response>(json);
    }

    public static string ToJson(Response request)
    {
        return JsonUtility.ToJson(request);
    }
}