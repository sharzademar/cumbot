using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Discord.Commands;
using Discord;

namespace Cumbot
{
    public class Commands : ModuleBase
    {
        [Command("test")]
        [Summary("Is this shit working?")]
        public async Task Test()
        {
            //get lit emoji
            string lit = this.GetGuildEmojiString("lit");

            await ReplyAsync($"ayyy it's lit, {Context.User.Mention} {lit}");
        }

        [Command("8ball")]
        [Summary("Ask the magic 8-ball a question")]
        public async Task EightBall([Remainder, Summary("the question")] string question)
        {
            EightBallClass ball = new EightBallClass(Utility.numgen);

            await ReplyAsync($"{Context.User.Mention}'s question: {question}\n{ball.Query()}");
        }

        [Command("call")]
        [Summary("Get the bot to insult a user")]
        public async Task Call([Summary("The user to insult")] string username, [Remainder, Summary("What to call them")] string insult)
        {
            var users = await Context.Guild.GetUsersAsync();

            var insulted_user = users
                .Where(x => x.Username.ToLower().Equals(username.ToLower()) ||
                      (x.Nickname != null && x.Nickname.ToLower().Equals(username.ToLower())) ||
                       x.Mention.Equals(username.Contains("<@!") ? username : username.Replace("<@", "<@!")))
                .ToList();

            if (insulted_user.Count != 1)
            {
                await ReplyAsync("I can't call the user that because they don't exist");
            }
            else
            {
                await ReplyAsync($"{insulted_user[0].Mention}, ur {insult}");
            }
        }

        [Command("big")]
        [Summary("Output the text with regional indicators")]
        public async Task Big([Remainder, Summary("Text to convert")] string text)
        {
            Dictionary<char, string> nums = new Dictionary<char, string>();
            nums.Add('0', ":zero:");
            nums.Add('1', ":one:");
            nums.Add('2', ":two:");
            nums.Add('3', ":three:");
            nums.Add('4', ":four:");
            nums.Add('5', ":five:");
            nums.Add('6', ":six:");
            nums.Add('7', ":seven:");
            nums.Add('8', ":eight:");
            nums.Add('9', ":nine:");

            string output = string.Empty;

            foreach (char letter in text)
            {
                //numbers
                if (letter > 47 && letter < 58)
                {
                    output += nums[letter];
                    output += ' ';
                }
                //respect newline
                else if (letter == '\n')
                {
                    output += letter;
                }
                //enlarge space
                else if (letter == ' ')
                {
                    output += "    ";
                }
                //question mark
                else if (letter == '?')
                {
                    output += ":question:";
                    output += ' ';
                }
                //exclamation mark
                else if (letter == '!')
                {
                    output += ":exclamation:";
                    output += ' ';
                }
                //letters
                else if ((letter > 64 && letter < 91) || (letter > 96 && letter < 123))
                {
                    output += $":regional_indicator_{letter.ToString().ToLower()}:";
                    output += ' ';
                }
                //green texting
                else if (letter == '>')
                {
                    output += GetGuildEmojiString("grn");
                    output += ' ';
                }
            }

            output = output.Trim();

            await ReplyAsync(output);
        }

        [Command("insult")]
        [Summary("Get the bot to randomly insult a user")]
        public async Task Insult([Summary("The user to insult")] string username)
        {
            var users = await Context.Guild.GetUsersAsync();

            var insulted_user = users
                .Where(x => x.Username.ToLower().Equals(username.ToLower()) ||
                      (x.Nickname != null && x.Nickname.ToLower().Equals(username.ToLower())) ||
                       x.Mention.Equals(username.Contains("<@!") ? username : username.Replace("<@", "<@!")))
                .ToList();

            if (insulted_user.Count != 1)
            {
                await ReplyAsync("I can't insult the user because they don't exist");
            }
            else
            {
                //await ReplyAsync($"{insulted_user[0].Mention}, ur {insult}");
                await Utility.RandomMessage(insulted_user[0], Context.Channel);
            }
        }

        private bool MessageContainsEmojiReaction(IUserMessage message, string emoji)
        {
            var reactions = message.Reactions
                .Where(x => x.Key.Name.Equals(emoji))
                .ToList();

            return reactions.Count > 0;
        }

        [Command("test-output-length")]
        [RequireRole("Cumlord")]
        public async Task TestOutputLength(string len = "3000")
        {
            int ilen = Int32.Parse(len);
            string lorem = "Lorem ipsum dolor sit amet, consectetuer adipiscing elit. Aenean commodo ligula eget dolor. Aenean massa. Cum sociis natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Donec quam felis, ultricies nec, pellentesque eu, pretium quis, sem. Nulla consequat massa quis enim. Donec pede justo, fringilla vel, aliquet nec, vulputate eget, arcu. In enim justo, rhoncus ut, imperdiet a, venenatis vitae, justo. Nullam dictum felis eu pede mollis pretium. Integer tincidunt. Cras dapibus. Vivamus elementum semper nisi. Aenean vulputate eleifend tellus. Aenean leo ligula, porttitor eu, consequat vitae, eleifend ac, enim. Aliquam lorem ante, dapibus in, viverra quis, feugiat a, tellus. Phasellus viverra nulla ut metus varius laoreet. Quisque rutrum. Aenean imperdiet. Etiam ultricies nisi vel augue. Curabitur ullamcorper ultricies nisi. Nam eget dui. Etiam rhoncus. Maecenas tempus, tellus eget condimentum rhoncus, sem quam semper libero, sit amet adipiscing sem neque sed ipsum. Nam quam nunc, blandit vel, luctus pulvinar, hendrerit id, lorem. Maecenas nec odio et ante tincidunt tempus. Donec vitae sapien ut libero venenatis faucibus. Nullam quis ante. Etiam sit amet orci eget eros faucibus tincidunt. Duis leo. Sed fringilla mauris sit amet nibh. Donec sodales sagittis magna. Sed consequat, leo eget bibendum sodales, augue velit cursus nunc, quis gravida magna mi a libero. Fusce vulputate eleifend sapien. Vestibulum purus quam, scelerisque ut, mollis sed, nonummy id, metus. Nullam accumsan lorem in dui. Cras ultricies mi eu turpis hendrerit fringilla. Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia Curae; In ac dui quis mi consectetuer lacinia. Nam pretium turpis et arcu. Duis arcu tortor, suscipit eget, imperdiet nec, imperdiet iaculis, ipsum. Sed aliquam ultrices mauris. Integer ante arcu, accumsan a, consectetuer eget, posuere ut, mauris. Praesent adipiscing. Phasellus ullamcorper ipsum rutrum nunc. Nunc nonummy metus. Vestibulum volutpat pretium libero. Cras id dui. Aenean ut eros et nisl sagittis vestibulum. Nullam nulla eros, ultricies sit amet, nonummy id, imperdiet feugiat, pede. Sed lectus. Donec mollis hendrerit risus. Phasellus nec sem in justo pellentesque facilisis. Etiam imperdiet imperdiet orci. Nunc nec neque. Phasellus leo dolor, tempus non, auctor et, hendrerit quis, nisi. Curabitur ligula sapien, tincidunt non, euismod vitae, posuere imperdiet, leo. Maecenas malesuada. Praesent congue erat at massa. Sed cursus turpis vitae tortor. Donec posuere vulputate arcu. Phasellus accumsan cursus velit. Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia Curae; Sed aliquam, nisi quis porttitor congue, elit erat euismod orci, ac placerat dolor lectus quis orci. Phasellus consectetuer vestibulum elit. Aenean tellus metus, bibendum sed, posuere ac, mattis non, nunc. Vestibulum fringilla pede sit amet augue. In turpis. Pellentesque posuere. Praesent turpis. Aenean posuere, tor";
            lorem = lorem.Substring(0, ilen);
            await ReplyAsync(lorem);
        }

        [Command("remove-eggplant")]
        [Summary("Test command to remove all eggplant reacts given by the bot, only prints can run")]
        [RequireRole("Cumlord")]
        public async Task RemoveEggplant()
        {
            var messages = Context.Channel.GetMessagesAsync().Flatten().Result
                .Cast<IUserMessage>()
                .Where(x => this.MessageContainsEmojiReaction(x, "🍆"));

            await ReplyAsync("Removing all :eggplant:'s");
            foreach (var message in messages)
            {
                await (message).RemoveReactionAsync(new Emoji("🍆"), Context.Client.CurrentUser);
            }
            await ReplyAsync("Finished removing all :eggplant:'s");
        }

        [Command("call-gay")]
        [Summary("insult a specified user")]
        public async Task CallGay([Summary("The user to call gay")] string username)
        {
            var users = await Context.Guild.GetUsersAsync();

            var gay_user = users
                .Where(x => x.Username.ToLower().Equals(username.ToLower()) ||
                      (x.Nickname != null && x.Nickname.ToLower().Equals(username.ToLower())) ||
                       x.Mention.Equals(username))
                .ToList();

            if (gay_user.Count != 1)
            {
                await ReplyAsync("I can't call the user gay because they don't exist");
            }
            else
            {
                await ReplyAsync($"{gay_user[0].Mention}, ur gay");
            }
        }

        [Command("thenking")]
        [Alias("thenkin", "thonkin", "thonking", "thinking", "thinkin", "thenk", "thonk", "think")]
        public async Task Think()
        {
            List<string> emojis = new List<string> { "thenking", "thenkighn", "hmm" };
            string emoji = emojis[Utility.numgen.Next(0, emojis.Count)];
            string gemoji = this.GetGuildEmojiString(emoji);

            await ReplyAsync($"Please wait, {gemoji} in progress...");

            var thinks = Directory.GetFiles(@"..\..\img\think");
            string file = thinks[Utility.numgen.Next(0, thinks.Length)];

            await Context.Channel.SendFileAsync(file);
        }

        [Command("trithink")]
        [Alias("trithenk", "trithonk", "tri-think", "tri-thonk", "tri-thenk", "triplethink", "triplethenk", "triplethonk", "triple-think", "triple-thenk", "triple-thonk", "trithenkvirate", "spin-think", "spinning-think", "thinkfinity")]
        public async Task SpinThenk()
        {
            await Context.Channel.SendFileAsync(@"..\..\img\spinthink\spinthink.gif");
        }

        private string GetGuildEmojiString(string name)
        {
            var es = Context.Guild.Emotes
                .Where(x => x.Name.Equals(name))
                .ToList();

            if (es.Count != 1)
                throw new ArgumentException($"\"{name}\" is not a guild emoji, you dringus!");
            return $"<:{es[0].Name}:{ es[0].Id}>";
        }

    }
}
