using System;
using UnityEngine;

/// <summary>스크립트 실행에 필요한 컨텍스트</summary>
public class ScriptContext
{
    // ===== Stores =====
    public DialogueStore DialogueStore { get; set; }
    public ChoiceStore ChoiceStore { get; set; }
    public CharacterStore CharacterStore { get; set; }
    public FlowStore FlowStore { get; set; }

    /// <summary>변수 저장소</summary>
    public VariableStore VariableStore { get; set; }

    // ===== 흐름 제어 =====
    /// <summary>다음 액션의 타입 확인 (choices 자동 진행 등에 사용)</summary>
    public Func<string> PeekNextType { get; set; }

    /// <summary>스크립트 교체 요청 콜백 (scriptPath)</summary>
    public Action<string> OnScriptChange { get; set; }

    // ===== 리소스 로딩 헬퍼 =====
    private const string CharacterPathPrefix = "Images/Characters/";

    public Sprite LoadCharacterSprite(string fileName)
    {
        string path = CharacterPathPrefix + fileName;
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"캐릭터 이미지 로드 실패: {path}");
        return sprite;
    }
}
