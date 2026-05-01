namespace DiscordBot.Helper;

internal static class JournalHelper
{
    internal static readonly string[] Prompts =
    [
        "What made you smile today, even if just for a moment?",
        "What's one thing you're grateful for right now?",
        "Describe a challenge you faced recently and what you learned from it.",
        "What's something you're looking forward to?",
        "Write about a person who positively impacted your day.",
        "What would you do differently if you could redo today?",
        "What's a goal you've been putting off? What's one small step toward it?",
        "Describe your current emotional state in three words and explore why.",
        "What's something you want to remember about this period of your life?",
        "Write about a moment recently where you felt truly present.",
        "What's a belief you hold that you've never fully examined?",
        "What does your ideal self look like one year from now?",
        "Write about something that's been weighing on you. How can you release it?",
        "What are three things you did well today?",
        "Describe a recent interaction that stuck with you. Why did it matter?",
        "What's something you've been avoiding, and what's the real reason?",
        "Write a short letter to your future self.",
        "What does self-care look like for you right now?",
        "What's a pattern in your life you'd like to change?",
        "Describe a moment of unexpected kindness you witnessed or experienced.",
        "What's something new you learned this week?",
        "What boundaries do you need to set or reinforce in your life?",
        "Write about a time you were genuinely proud of yourself.",
        "What's a fear you'd like to work through? What's one small step forward?",
        "How have you grown in the past year?",
        "What's draining your energy lately, and what's one way to change it?",
        "Write about a decision you're wrestling with. What does your gut say?",
        "What does a perfect day look like for you right now?",
        "Who in your life deserves more appreciation from you?",
        "What are you holding onto that you could let go of?",
    ];

    internal static string GetRandomPrompt() => Prompts[Random.Shared.Next(Prompts.Length)];

    internal static string[] GetRandomPrompts(int count) =>
        Prompts.OrderBy(_ => Random.Shared.Next()).Take(count).ToArray();
}
