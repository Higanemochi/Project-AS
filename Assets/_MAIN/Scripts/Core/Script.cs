using System.Collections.Generic;
using UnityEngine;

public class Command
{
    public string Type { get; set; }
    public Dictionary<string, object> Params { get; set; } = new();
    public List<Dictionary<string, string>> Choices { get; set; }

    public string GetParam(string key, string defaultValue = "")
    {
        return Params.ContainsKey(key) ? Params[key].ToString() : defaultValue;
    }
}
public class Script
{
    private List<Command> _commands;
    private int _currentIndex = -1;
    private Dictionary<string, int> _labelMap = new();

    public Script(List<Command> commands, Dictionary<string, int> labelMap)
    {
        _commands = commands;
        _labelMap = labelMap;
        _currentIndex = -1;
    }

    public bool HasNextCommand()
    {
        return _currentIndex < _commands.Count - 1;
    }

    public Command Continue()
    {
        if (!HasNextCommand())
            return null;

        _currentIndex++;
        Command currentCommand = _commands[_currentIndex];

        return currentCommand;
    }

    public Command GetCurrent()
    {
        if (_currentIndex >= 0 && _currentIndex < _commands.Count)
            return _commands[_currentIndex];
        return null;
    }

    public Command PeekNext()
    {
        if (_currentIndex < _commands.Count - 1)
            return _commands[_currentIndex + 1];
        return null;
    }

    public void JumpTo(string labelName)
    {
        _currentIndex = _labelMap[labelName] - 1;
        Debug.Log($"Script :: Jump to label: {labelName} (Index: {_currentIndex + 1})");
    }
}
