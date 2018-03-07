using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

//TODO set an arbitrary time as the starting collection time of day instead of setting collection timer to last collection, seems more fair
//TODO $pepe-update-cards should delete from the database cards that it doesn't find in the folders
//TODO investigate using imgur to store the photos

namespace Cumbot
{
    class Program
    {
        static void Main(string[] args) =>
            new Program().Start().GetAwaiter().GetResult();

        private DiscordSocketClient client;
        private CommandHandler handler;
        private IServiceProvider services;
        private CommandService commands;

        public async Task Start()
        {
            //logfile
            Trace.Listeners.Clear();
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            TextWriterTraceListener twtl = new TextWriterTraceListener("log.txt")
            {
                Name = "TextLogger"
            };

            Utility.numgen = new Random();

            //init user dict
            Utility.ids_to_names = new Dictionary<ulong, string>();
            Utility.ids_to_names.Add(121098391248830464, "prints");
            Utility.ids_to_names.Add(121101306336116736, "andre");
            Utility.ids_to_names.Add(163156714697261056, "demens");
            Utility.ids_to_names.Add(121099193631768577, "bigbox123");
            Utility.ids_to_names.Add(157364552869216258, "capitalist");
            //Utility.ids_to_names.Add(121053059634823170, "kreamy");
            Utility.ids_to_names.Add(157368607653756928, "ska");
            Utility.ids_to_names.Add(245077608721678336, "lyn");
            Utility.ids_to_names.Add(163189943525441536, "xenu");
            Utility.ids_to_names.Add(102763008001859584, "jumcum");
            Utility.ids_to_names.Add(275078911950323714, "cumbot");

            //init trade session object
            Utility.Trade = new TradeSession();

            //console log
            ConsoleTraceListener ctl = new ConsoleTraceListener(false);

            Trace.Listeners.Add(twtl);
            Trace.Listeners.Add(ctl);
            Trace.AutoFlush = true;

            //instantiate client
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance,
                LogLevel = LogSeverity.Info
            });

            //super secret
            string token = "Mjc1MDc4OTExOTUwMzIzNzE0.C28rXA.1MJnLNhU9cYQda2XRx3QzjsVcwA";

            //logging function
            client.Log += this.Log;

            //mirror reactions
            //client.ReactionAdded += MirrorReaction;

            //random eggplants
            Timer eggtimer = new Timer(1000/*mil*/ * 60/*sec*/ * 60/*min*/ * 2/*hour*/);
            eggtimer.Elapsed += new ElapsedEventHandler(this.OnEggplantTimer);
            //eggtimer.Start();

            //random insults
            //Timer insulttimer = new Timer(1000/*mil*/ * 60/*sec*/ * 70 * 4/*min*/);
            //Timer insulttimer = new Timer(2200/*mil*/ * 1/*sec*/ * 1/*min*/);
            //insulttimer.Elapsed += new ElapsedEventHandler(this.OnInsultTimer);
            //insulttimer.Start();

            //connect
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            //setup command handler
            commands = new CommandService();
            services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(commands)
                .BuildServiceProvider();

            handler = new CommandHandler();
            await handler.Install(services);

            //run until dead
            await Task.Delay(-1);
        }

        //hee-hee
        private async Task MirrorReaction(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            //var message = arg3.Channel.GetMessageAsync(arg1).Result;
            var message = await arg2.GetMessageAsync(arg1.Id);
            var reactor = arg3.UserId;

            if (message.Author.Id == Utility.clientid)
            {
                IUserMessage last_message = (IUserMessage)arg3.Channel.GetMessagesAsync().Flatten().Result
                    .Where(x => x.Author.Id == reactor)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList()[0];
            }
        }

        private async void OnEggplantTimer(object source, ElapsedEventArgs e)
        {
            var channel = (SocketTextChannel)client.GetChannel(Utility.generalchannelid);

            var messages = channel.GetMessagesAsync(limit: 5).Flatten().Result
                .Cast<IUserMessage>()
                .Where(x => !this.MessageContainsEmojiReaction(x, "🍆"))
                .ToList();

            if (messages.Count > 0)
            {
                await messages[Utility.numgen.Next(0, messages.Count)].AddReactionAsync(Discord.Emote.Parse("🍆"));
            }
        }

        private async void OnInsultTimer(object source, ElapsedEventArgs e)
        {
            var guild = client.GetGuild(Utility.guildid);
            var channel = (SocketTextChannel)client.GetChannel(Utility.generalchannelid);

            var users = guild.Users
                //.Where(x => !x.IsBot && x.Status < UserStatus.Offline && x.Status != UserStatus.Unknown)
                .Where(x => !x.IsBot)
                .ToList();

            SocketGuildUser insulted_user = users[Utility.numgen.Next(0, users.Count)];

            await Utility.RandomMessage(insulted_user, channel);
        }

        private bool MessageContainsEmojiReaction(IUserMessage message, string emoji)
        {
            var reactions = message.Reactions
                .Where(x => x.Key.Name.Equals(emoji))
                .ToList();

            return reactions.Count > 0;
        }

        private Task Log(LogMessage msg)
        {
            Trace.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private bool IsSkaGay()
        {
            return true;
        }
    }
}
