// There is no need to implement IDisposable like before as we are
// using dependency injection, which handles calling Dispose for us.
using Discord.WebSocket;
using Discord;
using Discord.Commands;
using DiscordBot.Services;
using DiscordBot.Constants;
using Microsoft.Extensions.DependencyInjection;
using System.Data.SqlClient;
using System.Data;
using System.Net;
using Victoria.Node;
using Victoria;
using Microsoft.Extensions.Logging;
using DiscordBot.Helper;
using KillersLibrary.Services;
using OpenAI_API.Models;
using Fergun.Interactive;

class Program
{
    static DiscordSocketClient _client = new DiscordSocketClient();
    static void Main(string[] args)
    {
        System.Timers.Timer myTimer;
        myTimer = new System.Timers.Timer(50000); // Check every 50 seconds
        myTimer.Elapsed += OnTimedEvent;
        myTimer.AutoReset = true;
        myTimer.Enabled = true;
        myTimer.Start();
        new Program().MainAsync().GetAwaiter().GetResult();
    }
    public async Task MainAsync()
    {
        // You should dispose a service provider created using ASP.NET
        // when you are finished using it, at the end of your app's lifetime.
        // If you use another dependency injection framework, you should inspect
        // its documentation for the best way to do this.
        using (var services = ConfigureServices())
        {
            _client = services.GetRequiredService<DiscordSocketClient>();
            var lavaNode = services.GetRequiredService<LavaNode>();

            _client.Log += LogAsync;
            services.GetRequiredService<CommandService>().Log += LogAsync;

            // Tokens should be considered secret data and never hard-coded.
            // We can read from the environment variable to avoid hard coding.
            await _client.LoginAsync(TokenType.Bot, Constants.botToken);
            await _client.StartAsync();

            // Here we initialize the logic required to register our commands.
            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

            _client.ReactionAdded += HandleReactionAsync;

            _client.JoinedGuild += JoinedGuild;
            _client.UserJoined += UserJoined;
            _client.UserLeft += UserLeft;

            _client.UserVoiceStateUpdated += (user, before, after) =>
            {
                if (user.IsBot && after.VoiceChannel == null)
                {
                    if (lavaNode.TryGetPlayer(before.VoiceChannel.Guild, out var player))
                        lavaNode.LeaveAsync(before.VoiceChannel);
                }
                
                if (user.IsBot)
                    return Task.CompletedTask;

                // This should be the voice channel
                // Commenting out the last part to handle moves or disconnects
                if (before.VoiceChannel != null && before.VoiceChannel.ConnectedUsers.Where(s => !s.IsBot).ToList().Count == 0 && after.VoiceChannel == null) //&& after.VoiceChannel == null)
                {
                    // If true, disconnect the bot
                    if (lavaNode.TryGetPlayer(before.VoiceChannel.Guild, out var player))
                        lavaNode.LeaveAsync(before.VoiceChannel);
                }

                return Task.CompletedTask;
            };

            await _client.SetGameAsync("-help");

            _client.MessageReceived += async (msg) =>
            {
                if (msg != null && !msg.Author.IsBot && msg.Channel as SocketGuildChannel != null)
                {
                    string message = msg.Content.Trim().ToLower();
                    string connStr = Constants.discordBotConnStr;
                    var msgChannel = msg.Channel as SocketGuildChannel;
                    var serverId = msgChannel.Guild.Id.ToString();
                    bool isActive = false;
                    int totalActive = 0;
                    StoredProcedure stored = new StoredProcedure();
                    DataTable dtActive = stored.Select(connStr, "CheckIfKeywordsAreActivePerServer", new List<SqlParameter>
                    {
                        new SqlParameter("ServerUID", Int64.Parse(serverId))
                    });

                    if (dtActive.Rows.Count > 0)
                    {
                        foreach (DataRow row in dtActive.Rows)
                        {
                            totalActive = int.Parse(row["TotalActive"].ToString());
                            if (totalActive > 0)
                                isActive = true;
                        }
                    }

                    if (isActive)
                    {
                        if (message.StartsWith("$") && message.Length > 1)
                        {
                            try
                            {
                                message = message.Replace("$", "");
                                await msg.Channel.TriggerTypingAsync(new RequestOptions { Timeout = 30 });
                                var api = new OpenAI_API.OpenAIAPI(Constants.openAiSecret);
                                var result = await api.Completions.CreateCompletionAsync(new OpenAI_API.Completions.CompletionRequest(message, model: Model.DavinciText, max_tokens: 1000, temperature: 0.9, null, null, 1, null, null));
                                var response = result.ToString();

                                int length = response.Length;

                                if (response.Length > 2000)
                                {
                                    await msg.Channel.SendMessageAsync(response.Substring(0, 2000));
                                    await msg.Channel.SendMessageAsync(response.Substring(2000, length - 2000));
                                }
                                else
                                    await msg.Channel.SendMessageAsync(response);

                                await msg.Channel.SendMessageAsync("---END RESPONSE---");
                            }
                            catch (Exception ex)
                            {
                                var embed = new EmbedBuilder
                                {
                                    Title = "BigBirdBot - Error",
                                    Color = Color.Red,
                                    Description = ex.Message,
                                    ThumbnailUrl = Constants.errorImageUrl
                                };

                                await msg.Channel.SendMessageAsync(embed: embed.Build());
                            }
                        }
                        else if ((message.Contains("https://twitter.com") || message.Contains("https://x.com")) && !message.Contains("-"))
                        {
                            DataTable dtTwitter = stored.Select(connStr, "GetTwitterBroken", new List<SqlParameter> { new SqlParameter("@ServerID", Int64.Parse(serverId)) });
                            bool isTwitterBroken = false;
                            foreach (DataRow dr in dtTwitter.Rows)
                                isTwitterBroken = bool.Parse(dr["TwitterBroken"].ToString());
                            
                            if (isTwitterBroken)
                            {
                                if (message.Contains("https://twitter.com"))
                                    message = message.Replace("twitter", "fxtwitter");
                                if (message.Contains("https://x.com"))
                                    message = message.Replace("x.com", "fxtwitter.com");

                                message = message.Split("https://")[1];

                                if (message.Split(' ').Count() > 1)
                                    message = message.Split(' ')[0];

                                message = "https://" + message;
                                await msg.Channel.SendMessageAsync(message);
                            }
                            else
                            {
                                var urlStuff = message.Split(new string[] { "https://twitter.com/", "https://x.com" }, StringSplitOptions.None);
                                try
                                {
                                    if (urlStuff.Length > 0)
                                    {
                                        urlStuff = urlStuff[1].Split("/");
                                        if (urlStuff.Length > 0)
                                        {
                                            var user = urlStuff[0];
                                            var id = urlStuff[2];
                                            if (id.Contains("?"))
                                            {
                                                id = id.Split('?')[0];

                                                if (id.Contains(' '))
                                                    id = id.Split(' ')[0];
                                            }
                                            else
                                            {
                                                if (id.Contains(' '))
                                                    id = id.Split(' ')[0];
                                            }

                                            string apiUrl = $"https://api.fxtwitter.com/{user}/status/{id}/en";

                                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                                            request.AutomaticDecompression = DecompressionMethods.GZip;
                                            string results = string.Empty;

                                            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                                            using (Stream stream = response.GetResponseStream())
                                            using (StreamReader reader = new StreamReader(stream))
                                            {
                                                results = reader.ReadToEnd();
                                                StoredProcedure storedProcedure = new StoredProcedure();
                                                DataTable dt = storedProcedure.Select(connStr, "GetTwitterType", new List<SqlParameter> { new SqlParameter("@json", results) });
                                                if (dt.Rows.Count > 0)
                                                {
                                                    foreach (DataRow dr in dt.Rows)
                                                    {
                                                        if (dr["videoUrl"].ToString().Length > 0)
                                                        {
                                                            string url = dr["tweetUrl"].ToString();
                                                            url = url.Replace("twitter", "fxtwitter");

                                                            await msg.Channel.SendMessageAsync(url);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                { }
                            }
                        }
                        else if (message.Contains("remind "))
                        {
                            var channel = msg.Channel as SocketGuildChannel;
                            var sender = _client.GetChannel(channel.Id) as IMessageChannel;
                            StoredProcedure storedProcedure = new StoredProcedure();
                            List<SqlParameter> parameters = new List<SqlParameter>();
                            string createdBy = msg.Author.Mention;

                            parameters.Add(new SqlParameter("@Message", message));
                            DataTable dt = storedProcedure.Select(connStr, "GetEventTimeRange", parameters);

                            if (dt.Rows.Count > 0)
                            {
                                if (msg.MentionedUsers.Count > 0)
                                    createdBy = string.Join(",", msg.MentionedUsers.Select(s => s.Mention.ToString()));
                                if (msg.MentionedRoles.Count > 0)
                                    createdBy = string.Join(",", msg.MentionedRoles.Select(s => s.Mention.ToString()));

                                foreach (DataRow dr in dt.Rows)
                                {
                                    string split = message.Split(dr["EventKeyword"].ToString())[1];
                                    if (split.Contains("to")) { split = split.Replace("to", ""); }

                                    DataTable dtNewEvent = storedProcedure.Select(Constants.discordBotConnStr, "AddEvent", new List<SqlParameter>
                                    {
                                        new SqlParameter("@EventDateTime", DateTime.Now.AddMinutes(double.Parse(dr["Minutes"].ToString()))),
                                        new SqlParameter("@EventName", split),
                                        new SqlParameter("@EventDescription", split),
                                        new SqlParameter("@EventUserUTCDate", TimeZoneInfo.ConvertTimeToUtc(DateTime.Now.AddMinutes(double.Parse(dr["Minutes"].ToString())), TimeZoneInfo.Local)),
                                        new SqlParameter("@EventChannelSource", long.Parse(channel.Id.ToString())),
                                        new SqlParameter("@CreatedBy", createdBy)
                                    });

                                    foreach (DataRow drEvent in dtNewEvent.Rows)
                                    {
                                        // Defaulting to 15 minutes reminder
                                        storedProcedure.UpdateCreate(Constants.discordBotConnStr, "AddEventReminder", new List<SqlParameter>
                                        {
                                            new SqlParameter("@EventID", int.Parse(drEvent["EventID"].ToString())),
                                            new SqlParameter("@EventDateTime", DateTime.Now.AddMinutes(double.Parse(dr["Minutes"].ToString()))),
                                            new SqlParameter("@EventName", split),
                                            new SqlParameter("@EventDescription", split),
                                            new SqlParameter("@EventReminderTime", 15),
                                            new SqlParameter("@EventUserUTCDate", TimeZoneInfo.ConvertTimeToUtc(DateTime.Now.AddMinutes(int.Parse(dr["Minutes"].ToString())), TimeZoneInfo.Local)),
                                            new SqlParameter("@CreatedBy", createdBy)
                                        });

                                        var embed = new EmbedBuilder
                                        {
                                            Title = ":calendar_spiral: BigBirdBot - Event - " + split,
                                            Color = Color.Gold
                                        };
                                        embed
                                            .AddField("Time (EST)", DateTime.Now.AddMinutes(double.Parse(dr["Minutes"].ToString())))
                                            .WithFooter(footer => footer.Text = "Created by " + msg.Author.Username)
                                            .WithCurrentTimestamp();
                                        await sender.SendMessageAsync(embed: embed.Build());
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (message.StartsWith("-"))
                            {
                                string keyword = "";
                                if (message.Split(' ').Count() == 1)
                                    keyword = message.Replace("-", "");
                                if (message.Split(' ').Count() > 1)
                                    keyword = message.Split(' ')[0].Replace("-", "");

                                // Check if it's in the ThirstMap and run the add command
                                List<SqlParameter> parameters = new List<SqlParameter>();
                                parameters.Add(new SqlParameter("@AddKeyword", keyword));

                                DataTable dt = stored.Select(connStr, "GetThirstTableByMap", parameters);
                                if (dt.Rows.Count > 0)
                                {
                                    if (msg.Attachments.Count > 0)
                                    {
                                        foreach (DataRow dr in dt.Rows)
                                        {
                                            var attachments = msg.Attachments;
                                            foreach (var attachment in attachments)
                                            {
                                                string path = @"C:\Users\Unmolded\Desktop\DiscordBot\" + dr["TableName"].ToString() + @"\" + attachment.Filename;

                                                // Check if link exists for thirst table
                                                DataTable dtExists = stored.Select(connStr, "CheckIfThirstURLExists", new List<SqlParameter>
                                                {
                                                    new SqlParameter("@FilePath", path),
                                                    new SqlParameter("@TableName", dt.Rows[0]["TableName"].ToString())
                                                });

                                                if (dtExists.Rows.Count > 0)
                                                {
                                                    var embedError = new EmbedBuilder
                                                    {
                                                        Title = "BigBirdBot - Error",
                                                        Color = Color.Red,
                                                        Description = $"The image provided was already added for this Thirst Command."
                                                    }.WithCurrentTimestamp();

                                                    await msg.Channel.SendMessageAsync(embed: embedError.Build());
                                                    await Task.CompletedTask;
                                                }
                                                else
                                                {
                                                    using (WebClient client = new WebClient())
                                                    {
                                                        client.DownloadFileAsync(new Uri(attachment.Url), path);
                                                    }

                                                    stored.UpdateCreate(connStr, "AddThirstByMap", new List<System.Data.SqlClient.SqlParameter>
                                                    {
                                                        new SqlParameter("@FilePath", path),
                                                        new SqlParameter("@TableName", dr["TableName"].ToString())
                                                    });
                                                }
                                            }
                                            var embed = new EmbedBuilder
                                            {
                                                Title = "BigBirdBot - Added Image",
                                                Color = Color.Blue,
                                                Description = "Added attachment(s) successfully."
                                            };

                                            await msg.Channel.SendMessageAsync(embed: embed.Build());
                                        }
                                    }
                                    if (message.Split(' ').Count() > 1)
                                    {
                                        string content = message.Split(' ')[1].Trim();

                                        Uri uriResult;
                                        bool result = Uri.TryCreate(content, UriKind.Absolute, out uriResult)
                                            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                                        if (!result)
                                        {
                                            var embed = new EmbedBuilder
                                            {
                                                Title = "BigBirdBot - Error",
                                                Color = Color.Red,
                                                Description = $"The URL provided for this command is invalid."
                                            }.WithCurrentTimestamp();

                                            await msg.Channel.SendMessageAsync(embed: embed.Build());
                                        }
                                        else
                                        {
                                            if (message.Contains("https://fxtwitter.com"))
                                                content = content.Replace("fxtwitter", "dl.fxtwitter.com");
                                            if (message.Contains("https://vxtwitter.com"))
                                                content = content.Replace("vxtwitter", "dl.vxtwitter.com");
                                            if (message.Contains("https://twitter.com"))
                                                content = content.Replace("twitter.com", "dl.fxtwitter.com");
                                            if (message.Contains("https://x.com"))
                                                content = content.Replace("x.com", "dl.fxtwitter.com");

                                            // Check if link exists for thirst table
                                            DataTable dtExists = stored.Select(connStr, "CheckIfThirstURLExists", new List<SqlParameter>
                                            {
                                                new SqlParameter("@FilePath", content),
                                                new SqlParameter("@TableName", dt.Rows[0]["TableName"].ToString())
                                            });

                                            if (dtExists.Rows.Count > 0)
                                            {
                                                var embed = new EmbedBuilder
                                                {
                                                    Title = "BigBirdBot - Error",
                                                    Color = Color.Red,
                                                    Description = $"The URL provided was already added for this Thirst Command."
                                                }.WithCurrentTimestamp();

                                                await msg.Channel.SendMessageAsync(embed: embed.Build());
                                            }
                                            else
                                            {
                                                foreach (DataRow dr in dt.Rows)
                                                {
                                                    stored.UpdateCreate(connStr, "AddThirstByMap", new List<System.Data.SqlClient.SqlParameter>
                                                    {
                                                        new SqlParameter("@FilePath", content),
                                                        new SqlParameter("@TableName", dr["TableName"].ToString())
                                                    });

                                                    var embed = new EmbedBuilder
                                                    {
                                                        Title = "BigBirdBot - Added Image",
                                                        Color = Color.Blue,
                                                        Description = "Added attachment(s) successfully."
                                                    };

                                                    await msg.Channel.SendMessageAsync(embed: embed.Build());
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // Todo, check all the commands eventually but for now let's stop the accidently double triggering.
                            if (!message.StartsWith("-") && !message.StartsWith("$"))
                            {
                                var channel = msg.Channel as SocketGuildChannel;
                                StoredProcedure storedProcedure = new StoredProcedure();
                                List<SqlParameter> parameters = new List<SqlParameter>();
                                parameters.Add(new SqlParameter("@ServerID", Int64.Parse(channel.Guild.Id.ToString())));
                                parameters.Add(new SqlParameter("@Message", message));
                                DataTable dt = storedProcedure.Select(connStr, "GetChatAction", parameters);

                                var sender = _client.GetChannel(channel.Id) as IMessageChannel;

                                if (dt.Rows.Count > 0 && sender != null)
                                {
                                    foreach (DataRow dr in dt.Rows)
                                    {
                                        string chatAction = dr["ChatAction"].ToString();

                                        if (!string.IsNullOrEmpty(chatAction))
                                        {
                                            await msg.Channel.TriggerTypingAsync(new RequestOptions { Timeout = 30 });
                                            if (chatAction.Contains("C:\\"))
                                                await msg.Channel.SendFileAsync(dr["ChatAction"].ToString());
                                            else
                                                await sender.SendMessageAsync(dr["ChatAction"].ToString());

                                            parameters.Clear();
                                            parameters.Add(new SqlParameter("@ChatKeywordID", int.Parse(dr["ChatKeywordID"].ToString())));
                                            parameters.Add(new SqlParameter("@MessageText", message));
                                            parameters.Add(new SqlParameter("@CreatedBy", msg.Author.Username));
                                            storedProcedure.UpdateCreate(connStr, "AddAuditKeyword", parameters);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            _client.Ready += async () =>
            {
                if (!lavaNode.IsConnected)
                    await lavaNode.ConnectAsync();
                
                return;
            };
            await Task.Delay(Timeout.Infinite);
        }
    }

    private async Task UserLeft(SocketGuild arg1, SocketUser arg2)
    {
        string title = "BigBirdBot - User Left";
        string desc = $"{arg2.Username} left the server.";
        string thumbnailUrl = arg2.GetAvatarUrl(ImageFormat.Png, 256);
        string createdBy = "BigBirdBot";
        string imageUrl = "";

        // Let's pull the first channel and hope for the best.....
        var textChannels = arg1.TextChannels.ToList();
        var firstTextChannel = arg1.GetTextChannel(textChannels[0].Id);
        var channel = _client.GetChannel(firstTextChannel.Id) as SocketTextChannel;

        EmbedHelper embed = new EmbedHelper();
        if (channel != null && !arg2.IsBot)
            await channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Gold, imageUrl).Build());
    }

    private async Task UserJoined(SocketGuildUser arg)
    {
        string title = "BigBirdBot - Introductions";
        string desc = $"Everyone welcome {arg.Mention} to the server!";
        string thumbnailUrl = arg.GetAvatarUrl(ImageFormat.Png, 256);
        string createdBy = "BigBirdBot";
        string imageUrl = "";

        // Let's pull the first channel and hope for the best.....
        List<SocketTextChannel> textChannels = new List<SocketTextChannel>();
        textChannels = arg.Guild.TextChannels.Where(s => s.Name.Contains("general") || s.Name.Contains("no-mic")).ToList();
        SocketTextChannel? firstTextChannel;

        if (textChannels.Count > 0)
        {
            if (textChannels.Count > 1)
                firstTextChannel = arg.Guild.GetTextChannel(textChannels[1].Id);
            else
                firstTextChannel = arg.Guild.GetTextChannel(textChannels[0].Id);
        }
        else
            firstTextChannel = arg.Guild.DefaultChannel;
        
        if (firstTextChannel != null)
        {
            var channel = _client.GetChannel(firstTextChannel.Id) as SocketTextChannel;

            EmbedHelper embed = new EmbedHelper();
            if (channel != null && !arg.IsBot)
                await channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Gold, imageUrl).Build());
        }

        StoredProcedure stored = new StoredProcedure();
        stored.UpdateCreate(Constants.discordBotConnStr, "AddUser", new List<SqlParameter>
        {
            new SqlParameter("@UserID", arg.Id.ToString()),
            new SqlParameter("@Username", arg.Username),
            new SqlParameter("@JoinDate", arg.JoinedAt),
            new SqlParameter("@GuildName", arg.Guild.Name),
            new SqlParameter("@Nickname", arg.Nickname)
        });

        string userId = arg.Id.ToString();
        var getUser = await _client.GetUserAsync(ulong.Parse(userId));
        var dmChannel = await getUser.CreateDMChannelAsync();

        EmbedHelper helper = new EmbedHelper();
        string output = "Welcome to the server, I'm BigBirdBot!\nI wanted to give you a proper welcome and hope you have a great time!\nFeel free to type -help in the server to get more information on what I can offer!";

        if (dmChannel != null)
            await dmChannel.SendMessageAsync(embed: helper.BuildMessageEmbed("BigBirdBot - Help Commands", output, "", "BigBirdBot", Discord.Color.Gold, null, null).Build());
    }
    private Task JoinedGuild(SocketGuild arg)
    {
        StoredProcedure stored = new StoredProcedure();

        DataTable dt = stored.Select(Constants.discordBotConnStr, "GetServers", new List<SqlParameter>());
        List<string> serverIds = new List<string>();
        foreach (DataRow dr in dt.Rows)
        {
            serverIds.Add(dr["ServerUID"].ToString());
        }

        if (serverIds.Where(s => s.Equals(arg.Id.ToString())).Count() == 0)
        {
            stored.UpdateCreate(Constants.discordBotConnStr, "AddServer", new List<SqlParameter>
            {
                new SqlParameter("@ServerUID", Int64.Parse(arg.Id.ToString())),
                new SqlParameter("@ServerName", arg.Name)
            });
        }

        using (SqlConnection conn = new SqlConnection(Constants.discordBotConnStr))
        {
            arg.DownloadUsersAsync();
            foreach (var user in arg.Users.ToList())
            {
                if (!user.IsBot)
                {
                    conn.Open();

                    // 1.  create a command object identifying the stored procedure
                    SqlCommand cmd = new SqlCommand("AddUser", conn);

                    // 2. set the command object so it knows to execute a stored procedure
                    cmd.CommandType = CommandType.StoredProcedure;

                    // 3. add parameter to command, which will be passed to the stored procedure
                    cmd.Parameters.Add(new SqlParameter("@UserID", user.Id.ToString()));
                    cmd.Parameters.Add(new SqlParameter("@Username", user.Username));
                    cmd.Parameters.Add(new SqlParameter("@JoinDate", user.JoinedAt));
                    cmd.Parameters.Add(new SqlParameter("@GuildName", user.Guild.Name));
                    cmd.Parameters.Add(new SqlParameter("@Nickname", user.Nickname));
                    // execute the command
                    cmd.ExecuteNonQuery();

                    conn.Close();
                    cmd.Dispose();
                }
            }
        }
        return Task.CompletedTask;
    }

    private async Task LogAsync(LogMessage log)
    {
        StoredProcedure stored = new StoredProcedure();
        string connStr = Constants.discordBotConnStr;
        List<SqlParameter> parameters = new List<SqlParameter>();
        string exception = "";
        EmbedHelper embedHelper = new EmbedHelper();

        // Send an error to the specific server and channel
        ulong guildId = ulong.Parse("880569055856185354");
        ulong textChannelId = ulong.Parse("1156625507840954369");

        if (log.Exception != null)
        {
            exception = log.Exception.Message;

            if (_client.GetGuild(guildId) != null)
            {
                if (_client.GetGuild(guildId).GetTextChannel(textChannelId) != null && log.Message.Length > 0)
                {
                    await _client.GetGuild(guildId).GetTextChannel(textChannelId).SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Exception Thrown", $"Exception: {exception}\nMessage: {log.Message}", "", "BigBirdBot", Discord.Color.Red, null, null).Build());
                }
            }
        }

        parameters.Add(new SqlParameter("@Severity", log.Severity));
        parameters.Add(new SqlParameter("@Source", log.Source));
        parameters.Add(new SqlParameter("@Message", log.Message));
        parameters.Add(new SqlParameter("@Exception", exception));

        stored.UpdateCreate(connStr, "AddLog", parameters);

        Console.WriteLine("Log written successfully to the database.");
    }

    private ServiceProvider ConfigureServices()
    {
        return new ServiceCollection()
            .AddSingleton(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
                AlwaysDownloadUsers = true,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                LogLevel = LogSeverity.Warning
            })
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton<CommandService>()
            .AddSingleton<CommandHandlingService>()
            .AddSingleton<HttpClient>()
            .AddLavaNode(x =>
            {
                x.SelfDeaf = true;
                x.Authorization = Constants.lavaLinkPwd;
                x.SocketConfiguration = new Victoria.WebSocket.WebSocketConfiguration { BufferSize = 2048, ReconnectAttempts = 10, ReconnectDelay = TimeSpan.FromSeconds(3) };
            })
            .AddSingleton<AudioService>()
            .AddSingleton<SpotifyHelper>()
            .AddSingleton<EmbedPagesService>()
            .AddSingleton<MultiButtonsService>()
            .AddSingleton(new InteractiveConfig { DefaultTimeout = TimeSpan.FromMinutes(15) })
            .AddSingleton<InteractiveService>()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();
    }

    private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        Emoji triviaA = new Emoji("🇦");
        Emoji triviaB = new Emoji("🇧");
        Emoji triviaC = new Emoji("🇨");
        Emoji triviaD = new Emoji("🇩");

        var embed = message.GetOrDownloadAsync().Result.Embeds;
        StoredProcedure stored = new StoredProcedure();
        if (_client.GetUser(reaction.UserId).IsBot) return;

        if (reaction.Emote.Name == triviaA.Name || reaction.Emote.Name == triviaB.Name || reaction.Emote.Name == triviaC.Name || reaction.Emote.Name == triviaD.Name)
        {
            string connStr = Constants.discordBotConnStr;
            try
            {
                if (embed.Count > 0)
                {
                    var messageId = Int64.Parse(message.Id.ToString());
                    DataTable dt = stored.Select(Constants.discordBotConnStr, "GetTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                    var theReaction = reaction.Emote.Name;
                    if (dt.Rows.Count > 0)
                    {
                        string correctAnswer = "";
                        foreach (DataRow dr in dt.Rows)
                        {
                            correctAnswer = dr["CorrectAnswer"].ToString();
                        }

                        foreach (var e in embed)
                        {
                            foreach (var f in e.Fields.Where(s => s.Name.Contains(".")).ToList())
                            {
                                if (correctAnswer.Equals(f.Value))
                                {
                                    EmbedHelper embedHelper = new EmbedHelper();
                                    // We have the correct answer, so let's get the reaction that it equals
                                    if (f.Name == "A." && theReaction.Equals(triviaA.Name))
                                    {
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Correct", $"{reaction.User.Value.Mention} answered correctly with **{correctAnswer}**!", "", "BigBirdBot", Discord.Color.Green, null, null).Build());
                                        stored.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                                    }
                                    else if (f.Name == "B." && theReaction.Equals(triviaB.Name))
                                    {
                                        // Check if the reaction was 'B'
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Correct", $"{reaction.User.Value.Mention} answered correctly with **{correctAnswer}**!", "", "BigBirdBot", Discord.Color.Green, null, null).Build());
                                        stored.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                                    }
                                    else if (f.Name == "C." && theReaction.Equals(triviaC.Name))
                                    {
                                        // Check if the reaction was 'C'
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Correct", $"{reaction.User.Value.Mention} answered correctly with **{correctAnswer}**!", "", "BigBirdBot", Discord.Color.Green, null, null).Build());
                                        stored.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                                    }
                                    else if (f.Name == "D." && theReaction.Equals(triviaD.Name))
                                    {
                                        // Check if the reaction was 'D'
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Correct", $"{reaction.User.Value.Mention} answered correctly with **{correctAnswer}**!", "", "BigBirdBot", Discord.Color.Green, null, null).Build());
                                        stored.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage", new List<SqlParameter> { new SqlParameter("@TriviaMessageID", messageId) });
                                    }
                                    else
                                    {
                                        // Ya wrong
                                        await channel.Value.SendMessageAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Wrong", $"{reaction.User.Value.Mention} you didn't answer correctly, try again!", "", "BigBirdBot", Discord.Color.Red, null, null).Build());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EmbedHelper errorEmbed = new EmbedHelper();
                await channel.Value.SendMessageAsync(embed: errorEmbed.BuildMessageEmbed("BigBirdBot - Error", ex.Message, Constants.errorImageUrl, "", Color.Red, "").Build());
            }
        }
    }

    // When the timer is kicked off, call this function to pull the EventText or ReminderText from SPs.
    private static async void OnTimedEvent(object sender, EventArgs e)
    {
        StoredProcedure storedProcedure = new StoredProcedure();
        string connStr = Constants.discordBotConnStr;
        DataTable dt = new DataTable();

        // 1. Check Event falls into specific timeframe
        // 2. Send ping to person in the same channel they sent the event in
        // 3. Repeat
        dt = storedProcedure.Select(connStr, "GetEvent", new List<SqlParameter> { });
        if (dt.Rows.Count > 0)
        {
            foreach (DataRow dr in dt.Rows)
            {
                IMessageChannel channel = (IMessageChannel)_client.GetChannel(ulong.Parse(dr["EventChannelSource"].ToString()));
                storedProcedure.UpdateCreate(Constants.discordBotConnStr, "DeleteEvent", new List<SqlParameter> { new SqlParameter("@EventID", dr["EventID"].ToString()) });

                await channel.SendMessageAsync(dr["EventText"].ToString());
            }
        }
        else
        {
            dt = storedProcedure.Select(connStr, "GetEventReminder", new List<SqlParameter> { });
            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    IMessageChannel channel = (IMessageChannel)_client.GetChannel(ulong.Parse(dr["EventChannelSource"].ToString()));
                    await channel.SendMessageAsync(dr["EventReminderText"].ToString());
                }
            }
        }
    }
}

