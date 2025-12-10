/// <summary>스크립트 실행 결과 - 흐름 제어</summary>
public struct ScriptResult
{
    public enum ResultType { Continue, Wait, Jump, End }

    public ResultType Type;
    public int NextIndex;

    public static ScriptResult Continue => new() { Type = ResultType.Continue };
    public static ScriptResult Wait => new() { Type = ResultType.Wait };
    public static ScriptResult End => new() { Type = ResultType.End };

    public static ScriptResult JumpTo(int index) =>
        new() { Type = ResultType.Jump, NextIndex = index };
}
