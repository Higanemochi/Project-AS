using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

public class Store
{
    private static Store _instance;
    public static Store Instance => _instance ??= new Store();

    private Dictionary<string, object> _variables = new();

    public void SetVariable(string name, string value)
    {
        // Try to parse as int or float, otherwise string
        if (int.TryParse(value, out int intVal))
            _variables[name] = intVal;
        else if (float.TryParse(value, out float floatVal))
            _variables[name] = floatVal;
        else
            _variables[name] = value;


        Debug.Log($"VariableManager :: Set {name} = {_variables[name]} ({_variables[name].GetType()})");
    }

    public object GetVariable(string name)
    {
        return _variables.ContainsKey(name) ? _variables[name] : null;
    }

    public void AddVariable(string name, string valueStr)
    {
        if (!_variables.ContainsKey(name))
        {
            SetVariable(name, valueStr);
            return;
        }

        object current = _variables[name];


        if (current is int currentInt && int.TryParse(valueStr, out int addInt))
        {
            _variables[name] = currentInt + addInt;
        }
        else if (current is float currentFloat && float.TryParse(valueStr, out float addFloat))
        {
            _variables[name] = currentFloat + addFloat;
        }
        else if (current is string currentStr)
        {
            _variables[name] = currentStr + valueStr;
        }
        else
        {
            Debug.LogWarning($"VariableManager :: Cannot add {valueStr} to {name} (Type: {current.GetType()})");
        }
        Debug.Log($"VariableManager :: Add {name} += {valueStr} -> {_variables[name]}");
    }

    public string ReplaceVariables(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{")) return text;

        return Regex.Replace(text, @"\{([^}]+)\}", match =>
        {
            string key = match.Groups[1].Value;
            return _variables.ContainsKey(key) ? _variables[key].ToString() : match.Value;
        });
    }
}
