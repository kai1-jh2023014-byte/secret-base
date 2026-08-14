namespace SecretBase.Core.Ai;

/// <summary>Built-in AI tools Secret Base can open (launcher hub — not in-app LLM).</summary>
public enum AiToolKind
{
    Cursor = 0,
    ChatGpt = 1,
    Claude = 2,
    Gemini = 3,
    Other = 4
}
