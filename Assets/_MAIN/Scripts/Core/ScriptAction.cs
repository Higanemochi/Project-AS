using System;
using Cysharp.Threading.Tasks;

/// <summary>컴파일된 스크립트 액션 - 실행 가능한 최소 단위</summary>
public class ScriptAction
{
    /// <summary>
    /// 실행 함수 - UniTask&lt;ScriptResult&gt; 반환
    /// </summary>
    public Func<ScriptContext, UniTask<ScriptResult>> Execute { get; set; }

    /// <summary>디버깅용 원본 타입 (label, msg, char 등)</summary>
    public string DebugType { get; set; }

    /// <summary>디버깅용 추가 정보</summary>
    public string DebugInfo { get; set; }
}
