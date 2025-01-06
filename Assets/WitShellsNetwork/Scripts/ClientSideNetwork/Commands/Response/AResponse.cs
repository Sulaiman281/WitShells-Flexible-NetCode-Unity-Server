using UnityEngine;

public abstract class AResponse<T> : IRequestCommand
{
    public const string APP_TYPE = "gameapp";
    public struct RequestData
    {
        public string apptype;
        public string cmd;
        public string data;
    }

    private T _requestData;
    public abstract EServerResponse RequestType { get; }

    public AResponse(T data)
    {
        _requestData = data;
    }

    public string ToJson()
    {
        var requestData = new RequestData
        {
            apptype = APP_TYPE,
            cmd = RequestType.CMD(),
            data = JsonUtility.ToJson(_requestData)
        };

        return JsonUtility.ToJson(requestData);
    }
}

public enum EServerResponse
{
    Salam,
    TossResults,
    PuckUpdateScore,
    GameStarted,
    GameEnded,
}

public static class ResponseExtension
{
    public static string CMD(this EServerResponse responseType)
    {
        return responseType switch
        {
            EServerResponse.TossResults => "tossResults",
            EServerResponse.PuckUpdateScore => "puckUpdateScore",
            EServerResponse.GameStarted => "gameStarted",
            EServerResponse.GameEnded => "gameEnded",
            EServerResponse.Salam => "salam",
            _ => throw new System.Exception($"{responseType} Response Command Not Registered!"),
        };
    }
}