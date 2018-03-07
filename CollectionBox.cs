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
    class CollectionBox
    {
        public Func<string, string, int, bool, Task<Stream>> ImageStreamAsyncDelegate;
        private ICommandContext context;
        private DataTable dt;
        private int currRecord;
        private IUserMessage msg;
        private DiscordSocketClient client;


        public CollectionBox(DataTable dt, ICommandContext context)
        {
            this.context = context;
            this.dt = dt;
            currRecord = 0;
            client = context.Client as DiscordSocketClient;
        }

        public async Task SendMessage(int index)
        {
            Stream img = await ImageStreamAsyncDelegate(dt.Rows[index]["name"].ToString(), dt.Rows[index]["image_extension"].ToString(), Convert.ToInt32(dt.Rows[index]["rarity"]), Convert.ToBoolean(dt.Rows[index]["foil"]));
            msg = await context.Channel.SendFileAsync(img, "test.png", text: $"Currently displaying card {currRecord+1} of {dt.Rows.Count}: {dt.Rows[index]["name"]}");
            await msg.AddReactionAsync(new Emoji("◀"));
            await msg.AddReactionAsync(new Emoji("▶"));
            await msg.AddReactionAsync(new Emoji("☠"));
            
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
                    if (currRecord < dt.Rows.Count-1)
                        currRecord++;
                }
                else if (arg3.Emote.Equals(new Emoji("☠")))
                {
                    client.ReactionAdded -= Navigation;
                    client.ReactionRemoved -= Navigation;
                    await msg.DeleteAsync();
                }

                if (prevRecord != currRecord)
                {
                    client.ReactionAdded -= Navigation;
                    client.ReactionRemoved -= Navigation;
                    await msg.DeleteAsync();

                    await SendMessage(currRecord);
                }
            }
        }
    }
}
