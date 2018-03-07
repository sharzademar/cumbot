using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Cumbot
{
    public class CommandHandler
    {
        private CommandService commands;
        private DiscordSocketClient client;
        private IServiceProvider services;

        public async Task Install(IServiceProvider services)
        {
            client = services.GetService<DiscordSocketClient>();
            commands = services.GetService<CommandService>();
            this.services = services;

            await commands.AddModulesAsync(Assembly.GetEntryAssembly());

            client.MessageReceived += HandleCommand;
        }

        private async Task HandleCommand(SocketMessage parameterMessage)
        {
            var message = parameterMessage as SocketUserMessage;
            if (message == null) return;
            var context = new CommandContext(client, message);

            int argpos = 0;
            if (!(message.HasMentionPrefix(client.CurrentUser, ref argpos) || message.HasCharPrefix(Utility.prefix, ref argpos)))
            {
                int percentage = Utility.numgen.Next(1, 101);

                if (context.Message.Content.Replace("<@", "<@!").Contains(client.CurrentUser.Mention))
                {
                    await context.Channel.SendMessageAsync($"{context.User.Mention} I heard u were talkin shit??");
                }
                /*else //random insults
                {
                    if (percentage <= 1 &&
                        !context.User.IsBot &&
                        context.Channel.Id != Utility.adventurechannelid)
                    {
                        await Utility.RandomMessage(context.User, context.Channel);
                    }
                }*/
            }
            else
            {
                int test;
                if (!int.TryParse(message.Content.ElementAt(1).ToString(), out test) || message.Content.ToLower().StartsWith("$8ball")) //exclude numbers
                {
                    var result = await commands.ExecuteAsync(context, argpos, services);

                    if (!result.IsSuccess)
                    {
                        if (result.Error == CommandError.Exception)
                        {
                            ExecuteResult eresult = (ExecuteResult)result;
                            Trace.WriteLine($"{DateTime.Now}: {eresult.Exception.StackTrace}");
                            Trace.WriteLine(eresult.Exception.Message);
                        }
                        var senterino = await message.Channel.SendMessageAsync($"**Error:** {result.ErrorReason}");

                        await message.AddReactionAsync(Discord.Emote.Parse("<:thenking:272823488748716032>"));
                        System.Threading.Thread.Sleep(1000);

                        //thenking to the command is funny, this is unecessary and takes up too much space
                        //await senterino.AddReactionAsync(Discord.Emote.Parse("<:thenkighn:275886030232748042>"));
                    }
                    else
                    {
                        //kinda annoying and takes up a lot of space
                        //await message.AddReactionAsync(new Discord.Emoji("👌"));
                    }
                }
            }
        }
    }
}
