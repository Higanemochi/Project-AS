using System.Collections.Generic;
using UnityEngine;

public class Command
{
    private List<CommandSet> _actions;
    private int _currentIndex = -1;
    private Dictionary<string, int> _labelMap = new();

    public Command(List<CommandSet> actions, Dictionary<string, int> labelMap)
    {
        _actions = actions;
        _labelMap = labelMap;
        _currentIndex = -1;
    }

    public bool HasNextAction()
    {
        return _currentIndex < _actions.Count - 1;
    }

    public CommandSet Continue()
    {
        if (!HasNextAction())
            return null;

        _currentIndex++;
        CommandSet currentAction = _actions[_currentIndex];

        return currentAction;
    }

    public CommandSet GetCurrent()
    {
        if (_currentIndex >= 0 && _currentIndex < _actions.Count)
            return _actions[_currentIndex];
        return null;
    }

    public CommandSet PeekNext()
    {
        if (_currentIndex < _actions.Count - 1)
            return _actions[_currentIndex + 1];
        return null;
    }

    public void JumpTo(string labelName)
    {
        _currentIndex = _labelMap[labelName] - 1; // Continue() 호출 시 해당 인덱스가 되도록 -1
        Debug.Log($"Script :: Jump to label: {labelName} (Index: {_currentIndex + 1})");
    }

    public void Save()
    {
        // TODO: _currentIndex 값을 받아와서 파일에 기록 or DB에 기록
        // 20251126_191933_SAVE -> { _currentIndex, expData, ... }
    }
}
