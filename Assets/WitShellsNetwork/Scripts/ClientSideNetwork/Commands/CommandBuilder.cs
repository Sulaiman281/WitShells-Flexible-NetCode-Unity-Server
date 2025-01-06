using UnityEngine;

public static class CommandBuilder
{
    struct Command
    {
        public string cmd;
        public string data;
    }
    public static IResponseCommand BuildResponseCommand(string jsonCommand)
    {
        Command command = JsonUtility.FromJson<Command>(jsonCommand);

        var data = command.data;

        return command.cmd switch
        {

            _ => throw new System.Exception($"{command} Response Command Not Registered!"),
        };
    }
}