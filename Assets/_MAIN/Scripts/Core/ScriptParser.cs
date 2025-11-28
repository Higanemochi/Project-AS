using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class ScriptParser
{
    private static readonly Regex TagRegex = new(@"^\[(\w+)(?:\s+(.*))?\]$");
    private static readonly Regex AttrRegex = new(@"(\w+)=(""[^""]*""|'[^']*'|[^ \t\]]+)");
    private static readonly Regex ChoiceOptionRegex = new(@"^\*\s*(.+?)\s*>\s*(.+)$");

    public static Script Parse(string text)
    {
        List<ScriptAction> actions = new();
        Dictionary<string, int> labelMap = new();

        ScriptAction lastChoice = null;

        text = Regex.Replace(text, "<shake>", "<link=shake>");
        text = Regex.Replace(text, "</shake>", "</link>");
        text = Regex.Replace(text, "\r\n", "\n");

        string[] lines = text.Split("\n");

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            Match tagMatch = TagRegex.Match(line);
            if (tagMatch.Success)
            {
                string tagName = tagMatch.Groups[1].Value;
                string attrString = tagMatch.Groups[2].Value;
                Debug.Log($"ScriptParser :: Tag: {tagName} {attrString}");

                var scriptAction = new ScriptAction { Type = tagName };

                if (!attrString.Contains("=")) scriptAction.Params["content"] = attrString;
                else ParseAttributes(attrString, scriptAction.Params);

                if (tagName == "label")
                {
                    string label = scriptAction.GetParam("content");
                    if (!string.IsNullOrEmpty(label) && !labelMap.ContainsKey(label))
                    {
                        labelMap[label] = actions.Count;
                    }
                }

                else if (tagName == "choices")
                {
                    scriptAction.Choices = new List<Dictionary<string, string>>();
                    lastChoice = scriptAction;
                }

                actions.Add(scriptAction);
                continue;
            }

            Match choiceMatch = ChoiceOptionRegex.Match(line);
            if (choiceMatch.Success && lastChoice != null)
            {
                lastChoice.Choices.Add(
                    new Dictionary<string, string>
                    {
                        { "type", "msg" },
                        { "content", choiceMatch.Groups[1].Value.Trim() },
                        { "goto", choiceMatch.Groups[2].Value.Trim() },
                    }
                );
                continue;
            }

            actions.Add(new ScriptAction { Type = "msg", Params = { { "content", line } } });
        }

        return new Script(actions, labelMap);
    }

    private static void ParseAttributes(string attrString, Dictionary<string, object> paramDict)
    {
        if (string.IsNullOrWhiteSpace(attrString))
            return;

        foreach (Match m in AttrRegex.Matches(attrString))
        {
            string key = m.Groups[1].Value;
            string rawValue = m.Groups[2].Value;

            if (rawValue.Length >= 2 && (rawValue.StartsWith("\"") || rawValue.StartsWith("'")))
                rawValue = rawValue[1..^1];

            paramDict[key] = rawValue;
        }
    }
}
