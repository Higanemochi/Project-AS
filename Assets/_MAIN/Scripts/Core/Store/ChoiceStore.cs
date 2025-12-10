using System.Collections.Generic;
using R3;

/// <summary>선택지 옵션 (런타임용 - 변수 치환 완료)</summary>
public class ChoiceOption
{
    public string Text { get; set; }
    public string TargetLabel { get; set; }
    public int TargetIndex { get; set; }
}

/// <summary>선택지 상태 관리 Store</summary>
public class ChoiceStore
{
    public ReactiveProperty<bool> IsVisible { get; } = new(false);
    public ReactiveProperty<List<ChoiceOption>> Options { get; } = new(new());

    public void Show(List<ChoiceOption> choices)
    {
        Options.Value = choices;
        IsVisible.Value = true;
    }

    public void Hide()
    {
        IsVisible.Value = false;
        Options.Value = new();
    }
}
