using System.Collections.Generic;
using UnityEngine;

public class Script
{
    private List<ScriptAction> _actions;
    private int _currentIndex = -1;
    private Dictionary<string, int> _labelMap = new();

    public Script(List<ScriptAction> actions, Dictionary<string, int> labelMap)
    {
        _actions = actions;
        _labelMap = labelMap;
        _currentIndex = -1;
    }

    public bool HasNextAction()
    {
        return _currentIndex < _actions.Count - 1;
    }

    public ScriptAction Continue()
    {
        if (!HasNextAction())
            return null;

        _currentIndex++;
        ScriptAction currentAction = _actions[_currentIndex];

        return currentAction;
    }

    public ScriptAction GetCurrent()
    {
        if (_currentIndex >= 0 && _currentIndex < _actions.Count)
            return _actions[_currentIndex];
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
