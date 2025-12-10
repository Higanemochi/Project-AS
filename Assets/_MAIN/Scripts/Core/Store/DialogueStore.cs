using R3;

public class DialogueStore
{
    public ReactiveProperty<string> Dialogue { get; } = new("");
    public ReactiveProperty<string> Speaker { get; } = new("");
    public ReactiveProperty<bool> IsDrawing { get; } = new(false);
}