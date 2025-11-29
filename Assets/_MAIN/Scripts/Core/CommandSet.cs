using System.Collections.Generic;

public class CommandSet
{
    public string Type { get; set; }
    public Dictionary<string, object> Params { get; set; } = new();
    public List<Dictionary<string, string>> Choices { get; set; }

    public string GetParam(string key, string defaultValue = "")
    {
        return Params.ContainsKey(key) ? Params[key].ToString() : defaultValue;
    }
}
