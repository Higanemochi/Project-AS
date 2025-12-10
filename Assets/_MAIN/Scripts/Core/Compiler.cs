using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Command 배열을 ScriptAction 배열로 컴파일</summary>
public class Compiler
{
    private readonly Dictionary<string, int> _labelMap;

    private Compiler(Dictionary<string, int> labelMap)
    {
        _labelMap = labelMap;
    }

    /// <summary>컴파일 시점에만 사용되는 선택지 데이터</summary>
    private class ChoiceData
    {
        public string RawText;
        public string TargetLabel;
        public int TargetIndex;
    }

    public static ScriptAction[] Compile(List<Command> commands, Dictionary<string, int> labelMap)
    {
        var compiler = new Compiler(labelMap);
        return commands.Select(compiler.CompileCommand).ToArray();
    }

    private ScriptAction CompileCommand(Command cmd)
    {
        return cmd.Type switch
        {
            "label" => CompileLabel(cmd),
            "msg" => CompileMsg(cmd),
            "spk" => CompileSpk(cmd),
            "char" => CompileChar(cmd),
            "remove" => CompileRemove(cmd),
            "action" => CompileAction(cmd),
            "expr" => CompileExpr(cmd),
            "goto" => CompileGoto(cmd),
            "choices" => CompileChoices(cmd),
            "var" => CompileVar(cmd),
            "add" => CompileAdd(cmd),
            "bg" => CompileBg(cmd),
            "script" => CompileScript(cmd),
            _ => CompileUnknown(cmd)
        };
    }

    // ===== 헬퍼 메서드 =====

    private static ScriptAction SyncAction(string type, string debugInfo, System.Action<ScriptContext> execute)
    {
        return new ScriptAction
        {
            DebugType = type,
            DebugInfo = debugInfo,
            Execute = ctx =>
            {
                execute(ctx);
                return UniTask.FromResult(ScriptResult.Continue);
            }
        };
    }

    // ===== 개별 컴파일러 =====

    private ScriptAction CompileLabel(Command cmd)
    {
        return new ScriptAction
        {
            DebugType = "label",
            DebugInfo = cmd.GetParam("content"),
            Execute = _ => UniTask.FromResult(ScriptResult.Continue)
        };
    }

    private ScriptAction CompileMsg(Command cmd)
    {
        var rawContent = cmd.GetParam("content");
        return new ScriptAction
        {
            DebugType = "msg",
            DebugInfo = rawContent.Length > 20 ? rawContent[..20] + "..." : rawContent,
            Execute = ctx => ExecuteMsg(ctx, rawContent)
        };
    }

    private static UniTask<ScriptResult> ExecuteMsg(ScriptContext ctx, string rawContent)
    {
        string content = ctx.VariableStore.ReplaceVariables(rawContent);
        ctx.DialogueStore.Dialogue.Value = content;

        return UniTask.FromResult(
            ctx.PeekNextType?.Invoke() == "choices"
                ? ScriptResult.Continue
                : ScriptResult.Wait);
    }

    private ScriptAction CompileSpk(Command cmd)
    {
        var rawName = cmd.GetParam("name");
        return SyncAction("spk", rawName, ctx =>
            ctx.DialogueStore.Speaker.Value = ctx.VariableStore.ReplaceVariables(rawName));
    }

    private ScriptAction CompileChar(Command cmd)
    {
        var img = cmd.GetParam("img");
        var direction = ParseDirectionType(cmd.GetParam("enter"));
        return SyncAction("char", img, ctx =>
        {
            var sprite = ctx.LoadCharacterSprite(img);
            if (sprite != null)
                ctx.CharacterStore.Add(img, sprite, direction);
        });
    }

    private ScriptAction CompileRemove(Command cmd)
    {
        var target = cmd.GetParam("target");
        var direction = ParseDirectionType(cmd.GetParam("exit"));
        return SyncAction("remove", target, ctx =>
            ctx.CharacterStore.Remove(target, direction));
    }

    private ScriptAction CompileAction(Command cmd)
    {
        var target = cmd.GetParam("target");
        var animationType = ParseAnimationType(cmd.GetParam("anim"));
        return SyncAction("action", $"{target}:{animationType}", ctx =>
            ctx.CharacterStore.PlayAction(target, animationType));
    }

    private ScriptAction CompileExpr(Command cmd)
    {
        var target = cmd.GetParam("target");
        var expr = cmd.GetParam("expr").ToLower();
        return SyncAction("expr", $"{target}:{expr}", ctx =>
        {
            var sprite = ctx.LoadCharacterSprite(expr);
            if (sprite != null)
                ctx.CharacterStore.ChangeExpression(target, sprite);
        });
    }

    private ScriptAction CompileGoto(Command cmd)
    {
        var targetLabel = cmd.GetParam("content");
        var targetIndex = _labelMap[targetLabel];
        return new ScriptAction
        {
            DebugType = "goto",
            DebugInfo = targetLabel,
            Execute = _ => UniTask.FromResult(ScriptResult.JumpTo(targetIndex))
        };
    }

    private ScriptAction CompileChoices(Command cmd)
    {
        var choices = cmd.Choices.Select(c => new ChoiceData
        {
            RawText = c["content"],
            TargetLabel = c["goto"],
            TargetIndex = _labelMap[c["goto"]]
        }).ToList();

        return new ScriptAction
        {
            DebugType = "choices",
            DebugInfo = $"{choices.Count} options",
            Execute = ctx => ExecuteChoices(ctx, choices)
        };
    }

    private static UniTask<ScriptResult> ExecuteChoices(ScriptContext ctx, List<ChoiceData> choices)
    {
        var options = choices.Select(c => new ChoiceOption
        {
            Text = ctx.VariableStore.ReplaceVariables(c.RawText),
            TargetLabel = c.TargetLabel,
            TargetIndex = c.TargetIndex
        }).ToList();

        ctx.ChoiceStore.Show(options);
        return UniTask.FromResult(ScriptResult.Wait);
    }

    private ScriptAction CompileVar(Command cmd)
    {
        var pairs = cmd.Params.ToDictionary(p => p.Key, p => p.Value.ToString());
        return SyncAction("var", string.Join(",", pairs.Keys), ctx =>
        {
            foreach (var kvp in pairs)
                ctx.VariableStore.SetVariable(kvp.Key, kvp.Value);
        });
    }

    private ScriptAction CompileAdd(Command cmd)
    {
        var pairs = cmd.Params.ToDictionary(p => p.Key, p => p.Value.ToString());
        return SyncAction("add", string.Join(",", pairs.Keys), ctx =>
        {
            foreach (var kvp in pairs)
                ctx.VariableStore.AddVariable(kvp.Key, kvp.Value);
        });
    }

    private ScriptAction CompileScript(Command cmd)
    {
        var scriptPath = cmd.GetParam("file");
        return new ScriptAction
        {
            DebugType = "script",
            DebugInfo = scriptPath,
            Execute = ctx =>
            {
                ctx.OnScriptChange?.Invoke(scriptPath);
                return UniTask.FromResult(ScriptResult.End);
            }
        };
    }

    private ScriptAction CompileBg(Command cmd)
    {
        var file = cmd.GetParam("file");
        return SyncAction("bg", file, ctx =>
            Debug.Log($"Background: {file}")); // TODO: 배경 변경 로직
    }

    private ScriptAction CompileUnknown(Command cmd)
    {
        return SyncAction(cmd.Type, "unknown", ctx =>
            Debug.LogWarning($"Unknown command: {cmd.Type}"));
    }

    // ===== Enum 파서 (컴파일 타임 변환) =====
    private static AnimationType ParseAnimationType(string str) => str.ToLower() switch
    {
        "jump" => AnimationType.Jump,
        "shake" => AnimationType.Shake,
        "run" => AnimationType.Run,
        "nod" => AnimationType.Nod,
        "punch" => AnimationType.Punch,
        _ => AnimationType.Nod
    };

    private static DirectionType ParseDirectionType(string str) => str.ToLower() switch
    {
        "left" => DirectionType.Left,
        "right" => DirectionType.Right,
        "center" => DirectionType.Center,
        "bottomleft" => DirectionType.BottomLeft,
        "bottomright" => DirectionType.BottomRight,
        "top" => DirectionType.Top,
        "runleft" => DirectionType.RunLeft,
        "runright" => DirectionType.RunRight,
        _ => DirectionType.Center
    };
}
