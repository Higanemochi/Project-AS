using R3;

/// <summary>게임 흐름 상태 (모든 Drawer가 공통으로 구독)</summary>
public class FlowStore
{
    /// <summary>대화 스킵 중 여부 - DialogueDrawer, CharacterDrawer 등이 구독</summary>
    public ReactiveProperty<bool> IsSkipping { get; } = new(false);

    public void RequestSkip() => IsSkipping.Value = true;
    public void ResetSkip() => IsSkipping.Value = false;
}
