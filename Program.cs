// There is no need to implement IDisposable like before as we are
// using dependency injection, which handles calling Dispose for us.
using Discord.WebSocket;
using Discord;
using Discord.Commands;
using DiscordBot.Services;
using DiscordBot.Constants;
using Microsoft.Extensions.DependencyInjection;
using DiscordBot.Anime;
using DiscordBot.Currency;
using System.Data.SqlClient;
using System.Data;
using Google.Apis.Customsearch.v1;
using DiscordBot.Disease;
using DiscordBot.Food;
using DiscordBot.Json;
using System.Net;
using Victoria.Node;
using Victoria;
using Microsoft.Extensions.Logging;
using DiscordBot.Helper;
using KillersLibrary.Services;
using OpenAI_API.Models;
using Fergun.Interactive;
using System.Runtime.InteropServices;

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
                        // Anything in the IFs are global commands
                        // Ex: bear, dog, cat, fox
                        if (message.StartsWith("^") && message.Length > 1)
                        {
                            message = message.Replace("$", "");

                            message = "Provide a response like a cat to the following: \"" + message + "\"";
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
                        else if (message.StartsWith("$") && message.Length > 1)
                        {
                            try
                            {
                                message = message.Replace("$", "");

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
                        else if (message.Contains(" bear ") || message.StartsWith("bear") || message.EndsWith("bear"))
                        {
                            DiscordBot.Misc.Bear bear = new DiscordBot.Misc.Bear();
                            List<DiscordBot.Misc.Bear> bears = bear.GetBear(connStr);

                            EmbedHelper embed = new EmbedHelper();
                            await msg.Channel.SendMessageAsync(bears[0].BearUrl);
                        }
                        else if (message.Contains(" advice ") || message.StartsWith("advice") || message.EndsWith("advice"))
                        {
                            string apiUrl = "https://api.adviceslip.com/advice";
                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                            request.AutomaticDecompression = DecompressionMethods.GZip;
                            string results = string.Empty;

                            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                            using (Stream stream = response.GetResponseStream())
                            using (StreamReader reader = new StreamReader(stream))
                            {
                                results = reader.ReadToEnd();
                                Advice advice = new Advice();
                                List<Advice> advices = advice.GetAdvice(connStr, results);
                                foreach (var o in advices)
                                {
                                    var embed = new EmbedBuilder
                                    {
                                        Title = "BigBirdBot - Advice",
                                        Color = Color.Green,
                                        Description = o.AdviceText,
                                        ImageUrl = "https://englishlive.ef.com/blog/wp-content/uploads/sites/2/2015/05/how-to-give-advice-in-english.jpg"
                                    };

                                    await msg.Channel.SendMessageAsync(embed: embed.Build());
                                }
                            }
                        }
                        else if (message.Contains(" disease ") || message.StartsWith("disease") || message.EndsWith("disease"))
                        {
                            Disease disease = new Disease();
                            Random r = new Random();
                            List<Disease> diseases = disease.GetDisease(connStr);
                            var item = r.Next(1, diseases.Count);
                            int days = diseases.Where(s => s.ID == item).Select(s => s.ExpendencyDays).FirstOrDefault() ?? default(int);

                            string title = "BigBirdBot - Disease";
                            string desc = "I'm sorry " + msg.Author.Mention + ", you have **" + diseases.Where(s => s.ID == item).Select(s => s.DiseaseName).FirstOrDefault() +
                                    "**.\n50/50 chance you will perish: **" + DateTime.Now.AddDays(days).ToString("F") + "**";
                            string thumbnailUrl = "";
                            string createdBy = "";

                            EmbedHelper embed = new EmbedHelper();
                            await msg.Channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Green).Build());
                        }
                        else if (message.Contains(" dinner ") || message.StartsWith("dinner") || message.EndsWith("dinner"))
                        {
                            FoodItem food = new FoodItem();
                            List<FoodItem> foods = food.GetFood(connStr);

                            string title = "BigBirdBot - Dinner";
                            string desc = $"{msg.Author.Mention}, you're going to eat {foods[0].FoodName}";
                            string thumbnailUrl = "";
                            string createdBy = "";

                            EmbedHelper embed = new EmbedHelper();
                            await msg.Channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Green).Build());
                        }
                        else if (message.Contains(" bird ") || message.StartsWith("bird") || message.EndsWith("bird"))
                        {
                            DiscordBot.Misc.Bird bird = new DiscordBot.Misc.Bird();
                            Random r = new Random();
                            List<DiscordBot.Misc.Bird> flops = bird.GetBird(connStr);

                            string title = "BigBirdBot - Bird";
                            string desc = $"";
                            string thumbnailUrl = "";
                            string createdBy = "";
                            string imageUrl = flops[0].BirdUrl;

                            EmbedHelper embed = new EmbedHelper();
                            await msg.Channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Green, imageUrl).Build());
                        }
                        else if (message.Contains(" emily ") || message.StartsWith("emily") || message.EndsWith("emily"))
                        {
                            char[] chars = { 'a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l', ';', '\'' };//, 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L'};

                            Random r = new Random();
                            string bottom = "";
                            var item = r.Next(20, 60);
                            for (int i = 0; i < item; i++)
                            {
                                r = new Random();
                                var idx = r.Next(0, chars.Length);
                                bottom += chars[idx];
                            }

                            string title = "BigBirdBot - Emily";
                            string desc = $"I really am bilingual, I can speak in bottom, watch -> {bottom}";
                            string thumbnailUrl = "";
                            string createdBy = "";

                            EmbedHelper embed = new EmbedHelper();
                            await msg.Channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Green).Build());
                        }
                        else if (message.Contains(" frog ") || message.StartsWith("frog") || message.EndsWith("frog"))
                        {
                            string htmlUrl = "http://www.allaboutfrogs.org/funstuff/random/";
                            Random r = new Random();
                            var i = r.Next(1, 55);
                            string val = i.ToString().PadLeft(4, '0') + ".jpg";

                            string title = "BigBirdBot - Frog";
                            string desc = $"";
                            string thumbnailUrl = "";
                            string createdBy = "";
                            string imageUrl = htmlUrl + val;

                            EmbedHelper embed = new EmbedHelper();
                            await msg.Channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Green, imageUrl).Build());
                        }
                        else if (message.Contains(" docker ") || message.StartsWith("docker") || message.EndsWith("docker"))
                        {
                            string userId = "305085887639978004";
                            var test = await _client.GetUserAsync(ulong.Parse(userId));
                            var channel = await test.CreateDMChannelAsync();
                            await channel.SendMessageAsync("We've been trying to reach you about your car's extended warranty. \nContact here for further details: https://secure.square-enix.com/account/app/svc/ffxivregister?lng=en-gb");
                        }
                        else if (message.Contains("https://twitter.com"))
                        {
                            DataTable dtTwitter = stored.Select(connStr, "UpdateTwitterBroken", new List<SqlParameter>());
                            bool isTwitterBroken = false;
                            foreach (DataRow dr in dtTwitter.Rows)
                            {
                                isTwitterBroken = bool.Parse(dr["TwitterBroken"].ToString());
                            }
                            if (isTwitterBroken)
                            {
                                await msg.Channel.SendMessageAsync(message.Replace("twitter", "fxtwitter"));
                            }
                            else
                            {
                                var urlStuff = message.Split(new string[] { "https://twitter.com/" }, StringSplitOptions.None);
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
                        else
                        {
                            // Todo, check all the commands eventually but for now let's stop the accidently double triggering.
                            if (!message.StartsWith("-") && !message.StartsWith("^") && !message.StartsWith("$"))
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
                                        await sender.SendMessageAsync(dr["ChatAction"].ToString());
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
        {
            await channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Gold, imageUrl).Build());
        }
    }

    private async Task UserJoined(SocketGuildUser arg)
    {
        string title = "BigBirdBot - Introductions";
        string desc = $"Everyone welcome {arg.Mention} to the server!";
        string thumbnailUrl = arg.GetAvatarUrl(ImageFormat.Png, 256);
        string createdBy = "BigBirdBot";
        string imageUrl = "";

        // Let's pull the first channel and hope for the best.....
        var textChannels = arg.Guild.TextChannels.Where(s => s.Name.Contains("general") || s.Name.Contains("no-mic")).ToList();
        SocketTextChannel? firstTextChannel;
        if (textChannels.Count > 1)
        {
            firstTextChannel = arg.Guild.GetTextChannel(textChannels[1].Id);
        }
        else
        {
            firstTextChannel = arg.Guild.GetTextChannel(textChannels[0].Id);
        }
        
        var channel = _client.GetChannel(firstTextChannel.Id) as SocketTextChannel;

        EmbedHelper embed = new EmbedHelper();
        if (channel != null && !arg.IsBot)
        {
            await channel.SendMessageAsync(embed: embed.BuildMessageEmbed(title, desc, thumbnailUrl, createdBy, Color.Gold, imageUrl).Build());
        }
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
        return Task.CompletedTask;
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());

        return Task.CompletedTask;
    }

    private ServiceProvider ConfigureServices()
    {
        return new ServiceCollection()
            .AddSingleton(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers
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
            .AddSingleton<InteractiveService>()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();
    }

    private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        Emoji emoji = new Emoji("\uD83E\uDD0D");
        Emoji questionEmoji = new Emoji("\u2754");
        Emoji deactivateEmoji = new Emoji("❌");

        Emoji triviaA = new Emoji("🇦");
        Emoji triviaB = new Emoji("🇧");
        Emoji triviaC = new Emoji("🇨");
        Emoji triviaD = new Emoji("🇩");

        var embed = message.GetOrDownloadAsync().Result.Embeds;
        StoredProcedure stored = new StoredProcedure();
        if (_client.GetUser(reaction.UserId).IsBot) return;

        if (reaction.Emote.Name == deactivateEmoji.Name)
        {
            string connStr = Constants.discordBotConnStr;
            try
            {
                if (embed.Count > 0)
                {
                    //Marriage marriage = new Marriage();
                    //List<Marriage> marriages = marriage.GetMarriages(connStr);
                    //MarriageCharacter marriageCharacter = new MarriageCharacter();
                    //List<MarriageCharacter> marriageCharacters = marriageCharacter.GetMarriageCharacters(connStr);
                    foreach (var e in embed)
                    {
                        //var marriageCount = marriages.Where(s => s.ImageURL.Equals(e.Image.Value.Url)).Count();
                        // SP: GetMarriageCharactersByURL
                        stored.UpdateCreate(connStr, "UpdateMarriageCharacters", new List<SqlParameter>
                        {
                            new SqlParameter("@CharacterImageURL", e.Image.Value.Url),
                            new SqlParameter("@DeactivatedBy", _client.GetUser(reaction.UserId).Username)
                        });
                        
                        await channel.Value.SendMessageAsync(e.Title + " was deactivated by " + _client.GetUser(reaction.UserId).Username + " and won't pop up again, thanks for cleaning up the database! You are the best :smile:");
                        await reaction.Channel.DeleteMessageAsync(reaction.MessageId);
                    }
                }
            }
            catch (Exception ex)
            {
                EmbedHelper errorEmbed = new EmbedHelper();
                await channel.Value.SendMessageAsync(embed: errorEmbed.BuildMessageEmbed("BigBirdBot - Error", ex.Message, Constants.errorImageUrl, "", Color.Red, "").Build());
            }
        }
        if (reaction.Emote.Name == questionEmoji.Name)
        {
            string connStr = Constants.discordBotConnStr;
            try
            {
                if (embed.Count > 0)
                {
                    foreach (var e in embed)
                    {
                        CustomsearchService customSearchService = new CustomsearchService(new Google.Apis.Services.BaseClientService.Initializer() { ApiKey = Constants.googleSearchApiKey });
                        CseResource.ListRequest listRequest = customSearchService.Cse.List();
                        listRequest.Cx = Constants.googleSearchId;
                        listRequest.ExactTerms = e.Title;
                        listRequest.Start = 1;
                        listRequest.Num = 1;
                        var results = listRequest.Execute();
                        if (results.Items != null)
                        {
                            if (results.Items.Count > 0)
                            {
                                await channel.Value.SendMessageAsync("Here is more information on " + e.Title + ": " + results.Items.Select(s => s.Link).FirstOrDefault());
                            }
                            else
                            {
                                await channel.Value.SendMessageAsync("No results found for " + e.Title);
                            }
                        }
                        else
                        {
                            await channel.Value.SendMessageAsync("No results found for " + e.Title);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                EmbedHelper errorEmbed = new EmbedHelper();
                await channel.Value.SendMessageAsync(embed: errorEmbed.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.errorImageUrl, "", Color.Red, "").Build());
            }
        }
        if (reaction.Emote.Name == emoji.Name)
        {
            string connStr = Constants.discordBotConnStr;
            try
            {
                if (embed.Count > 0)
                {
                    Marriage marriage = new Marriage();
                    List<Marriage> marriages = marriage.GetMarriages(connStr);
                    MarriageCharacter marriageCharacter = new MarriageCharacter();
                    List<MarriageCharacter> marriageCharacters = marriageCharacter.GetMarriageCharacters(connStr);

                    foreach (var e in embed)
                    {
                        var marriageCount = marriages.Where(s => s.ImageURL.Equals(e.Image.Value.Url)).Count();
                        if (marriageCount > 0)
                        {
                            foreach (var m in marriages)
                            {
                                if (m.CharacterName.Equals(e.Title))
                                {
                                    await channel.Value.SendMessageAsync(":frowning: Sorry " + reaction.User.Value.Username + ", " + m.CharacterName + " is already married to " + m.CreatedBy + ":frowning:");
                                }
                            }

                        }
                        else
                        {
                            Currency currency = new Currency();
                            var userId = Convert.ToInt64(reaction.User.Value.Id);
                            var animeId = marriageCharacters.Where(s => s.CharacterURL.Equals(e.Image.Value.Url)).Select(s => s.AnimeID).FirstOrDefault();
                            stored.UpdateCreate(Constants.discordBotConnStr, "AddAnimeMarriage", new List<SqlParameter>
                            {
                                new SqlParameter("@AnimeID", animeId),
                                new SqlParameter("@CharacterName", e.Title),
                                new SqlParameter("@ImageURL", e.Image.Value.Url),
                                new SqlParameter("@CreatedBy", reaction.User.Value.Username)
                            });

                            var currencyVal = marriageCharacters.Where(s => s.CharacterURL.Equals(e.Image.Value.Url)).Select(s => s.CurrencyValue).FirstOrDefault();
                            var currentUser = currency.GetCurrencyUser(Constants.discordBotConnStr, userId);
                            foreach (var c in currentUser)
                            {
                                currency.UpdateCurrencyUser(Constants.discordBotConnStr, userId, (c.CurrencyValue + currencyVal));
                            }

                            await channel.Value.SendMessageAsync(":two_hearts: Congratulations!  **" + reaction.User.Value.Username + "** and **" + e.Title + "** are now married :two_hearts:");
                            await reaction.Channel.DeleteMessageAsync(reaction.MessageId);
                        }
                    }

                }
            }
            catch (Exception e)
            {
                EmbedHelper errorEmbed = new EmbedHelper();
                await channel.Value.SendMessageAsync(embed: errorEmbed.BuildMessageEmbed("BigBirdBot - Error", e.Message, Constants.errorImageUrl, "", Color.Red, "").Build());
            }
        }
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
                // 940432494291988571 - Bot Commands in Garuda Fan Club
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
                    // 940432494291988571 - Bot Commands in Garuda Fan Club
                    IMessageChannel channel = (IMessageChannel)_client.GetChannel(ulong.Parse(dr["EventChannelSource"].ToString()));
                    await channel.SendMessageAsync(dr["EventReminderText"].ToString());
                }
            }
        }
    }
}

