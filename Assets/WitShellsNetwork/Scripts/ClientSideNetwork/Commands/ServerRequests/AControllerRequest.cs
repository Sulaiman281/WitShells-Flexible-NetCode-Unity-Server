using UnityEngine;

public abstract class AControllerRequest<T> : IResponseCommand
{
    private T _responseData;

    protected T Data => _responseData;

    public AControllerRequest(string jsonData)
    {
        _responseData = JsonUtility.FromJson<T>(jsonData);
    }
    public virtual void Execute()
    {
        Debug.Log($"Response Command with data: {_responseData}");
    }
}