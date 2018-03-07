using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Rest;

namespace Cumbot
{
    class CollectionBox2
    {
        private ICommandContext context;
        private List<Stream> pages;
        private int currRecord;
        private IUserMessage msg;
        private DiscordSocketClient client;


        public CollectionBox2(List<Stream> pages, ICommandContext context)
        {
            this.context = context;
            this.pages = pages;
            currRecord = 0;
            client = context.Client as DiscordSocketClient;
        }

        public async Task SendMessage()
        {
            Stream cs = new MemoryStream();
            await pages[currRecord].CopyToAsync(cs);
            cs.Position = 0;
            pages[currRecord].Position = 0;

            using (Stream currStream = cs)
                msg = await context.Channel.SendFileAsync(cs, "test.png");
            await msg.AddReactionAsync(new Emoji("◀"));
            await msg.AddReactionAsync(new Emoji("▶"));
            await msg.AddReactionAsync(new Emoji("☠"));
            cs.Dispose();

            client.ReactionAdded += Navigation;
            client.ReactionRemoved += Navigation;
        }

        private async Task Navigation(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg1.Id == msg.Id && !arg3.User.Value.IsBot)
            {
                int prevRecord = currRecord;
                if (arg3.Emote.Equals(new Emoji("◀")))
                {
                    if (currRecord > 0)
                        currRecord--;
                }
                else if (arg3.Emote.Equals(new Emoji("▶")))
                {
                    if (currRecord < pages.Count - 1)
                        currRecord++;
                }
                else if (arg3.Emote.Equals(new Emoji("☠")))
                {
                    pages.ForEach(x => x.Dispose());
                    pages.Clear();
                    client.ReactionAdded -= Navigation;
                    client.ReactionRemoved -= Navigation;
                    await msg.DeleteAsync();
                }

                if (prevRecord != currRecord)
                {
                    client.ReactionAdded -= Navigation;
                    client.ReactionRemoved -= Navigation;
                    await msg.DeleteAsync();

                    await SendMessage();
                }
            }
        }
    }
}
