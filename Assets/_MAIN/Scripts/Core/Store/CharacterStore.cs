using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

// ===== 캐릭터 관련 Enums =====
public enum DirectionType
{
    Left,
    Right,
    BottomLeft,
    BottomRight,
    Center,
    Top,
    RunLeft,
    RunRight
}

public enum AnimationType
{
    Jump,
    Shake,
    Nod,
    Punch,
    Run
}

/// <summary>개별 캐릭터의 상태</summary>
public class CharacterState
{
    public string Name { get; }

    // 시각적 상태
    public ReactiveProperty<Sprite> Sprite { get; } = new(null);
    public ReactiveProperty<float> Alpha { get; } = new(1f);
    public ReactiveProperty<float> SlotWidth { get; } = new(0f);
    public ReactiveProperty<Vector2> Position { get; } = new(Vector2.zero);

    // 퇴장 상태
    public ReactiveProperty<bool> IsExiting { get; } = new(false);
    public ReactiveProperty<DirectionType?> ExitDirection { get; } = new(null);

    public CharacterState(string name)
    {
        Name = name;
    }
}

/// <summary>캐릭터 전체 상태 관리 Store</summary>
public class CharacterStore
{
    private readonly Dictionary<string, CharacterState> _characters = new();

    public event Action<string, CharacterState, DirectionType> OnCharacterAdded;
    public event Action<string, CharacterState, DirectionType> OnCharacterExiting;
    public event Action<string> OnCharacterRemoved;

    // 명령형 이벤트 (ReactiveProperty 대신 직접 이벤트 사용으로 빠른 연타 시에도 씹히지 않음)
    public event Action<string, AnimationType> OnActionRequested;
    public event Action<string, Sprite> OnExpressionRequested;

    public CharacterState Get(string name)
    {
        if (!_characters.ContainsKey(name))
        {
            _characters[name] = new CharacterState(name);
        }
        return _characters[name];
    }

    public bool Exists(string name) => _characters.ContainsKey(name);

    public void Add(string name, Sprite sprite, DirectionType direction)
    {
        if (Exists(name))
        {
            Debug.LogWarning($"이미 존재하는 캐릭터입니다: {name}");
            return;
        }

        var state = Get(name);
        state.Sprite.Value = sprite;
        state.Alpha.Value = 0f;
        state.SlotWidth.Value = 0f;
        state.Position.Value = Vector2.zero;
        state.IsExiting.Value = false;
        state.ExitDirection.Value = null;

        OnCharacterAdded?.Invoke(name, state, direction);
    }

    public void Remove(string name, DirectionType direction)
    {
        if (!Exists(name))
        {
            Debug.LogWarning($"존재하지 않는 캐릭터입니다: {name}");
            return;
        }

        var state = Get(name);
        state.IsExiting.Value = true;
        state.ExitDirection.Value = direction;

        // 즉시 Store에서 제거 (퇴장 애니메이션은 Drawer에서 독립적으로 처리)
        _characters.Remove(name);

        OnCharacterExiting?.Invoke(name, state, direction);
    }

    public void FinalRemove(string name)
    {
        // 이미 Remove에서 삭제했으므로 남은 정리 작업만
        OnCharacterRemoved?.Invoke(name);
    }

    public void PlayAction(string name, AnimationType action)
    {
        if (!Exists(name))
        {
            Debug.LogWarning($"액션 실패: '{name}' 캐릭터를 찾을 수 없습니다.");
            return;
        }

        OnActionRequested?.Invoke(name, action);
    }

    public void ChangeExpression(string name, Sprite newSprite)
    {
        if (!Exists(name))
        {
            Debug.LogWarning($"표정 변경 실패: '{name}' 캐릭터를 찾을 수 없습니다.");
            return;
        }

        OnExpressionRequested?.Invoke(name, newSprite);
    }

    public IEnumerable<string> AllNames => _characters.Keys;
}
