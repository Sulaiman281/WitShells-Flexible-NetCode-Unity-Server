using UnityEngine;

public class MyTcpClient : AbstractTcpClient
{


#if UNITY_EDITOR

    private void OnValidate()
    {
    }

#endif


    protected override void OnConnectionClosed()
    {
        Debug.Log("Connection closed");
    }

    protected override void OnConnectionFailToOpen()
    {
        Debug.Log("Connection failed to open");
    }

    protected override void OnConnectionOpen()
    {
        Debug.Log("Connection open");
    }

    protected override void OnMessageReceived(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (string.Equals(message, "ping"))
        {
            SendMessageToServer("pong");
            return;
        }

        Debug.Log("Received: " + message);

        try
        {
            CommandBuilder.BuildResponseCommand(message)?.Execute();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(message + "\n" + e);
        }
    }

    #region  Response Methods

    public void SendResponse(IRequestCommand response)
    {
        SendMessageToServer(response.ToJson());
    }

    #endregion
}

