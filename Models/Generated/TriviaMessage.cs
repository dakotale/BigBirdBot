using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class TriviaMessage
{
    public long TriviaMessageId { get; set; }

    public string CorrectAnswer { get; set; } = null!;

    public DateTime CreatedOn { get; set; }
}
