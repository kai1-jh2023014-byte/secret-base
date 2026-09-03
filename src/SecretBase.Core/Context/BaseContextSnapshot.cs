namespace SecretBase.Core.Context;

/// <summary>
/// Cached composition of Base context slices. Built without calling an LLM.
/// </summary>
public sealed record BaseContextSnapshot(
    DateTimeOffset CapturedAt,
    IReadOnlyList<BaseContextSlice> Slices)
{
    public BaseContextSlice? Find(string id) =>
        Slices.FirstOrDefault(slice => string.Equals(slice.Id, id, StringComparison.OrdinalIgnoreCase));

    public string ToPromptBlock()
    {
        if (Slices.Count == 0)
        {
            return "Base context: empty.";
        }

        var lines = new List<string> { "Base context (local, scoped):" };
        foreach (var slice in Slices)
        {
            lines.Add($"[{slice.Id}] {slice.Summary}");
            foreach (var fact in slice.Facts.Take(8))
            {
                lines.Add($"  - {fact}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
