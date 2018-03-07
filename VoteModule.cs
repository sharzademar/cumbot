using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Commands;

// TODO Should polls be case-insensitive?

namespace Cumbot
{
    [Group("vote")]
    public class VoteModule : ModuleBase
    {
        [Command("help")]
        public async Task Help([Remainder, Summary("dummy param")] string dummy = null)
        {
            string voteTimeToString = $"{CurrentVote.VoteTime.Hours} hour(s) and {CurrentVote.VoteTime.Minutes} minute(s)";
            string helpMsg = "The following sub-commands are available in the vote module:\n" +
                                $"\t**help**: access this message (e.g. **{Utility.prefix}vote help**)\n" +
                                $"\t**start yes/no <question>**: Start a vote with a yes/no answering option (e.g. **{Utility.prefix}vote start yes/no Is Ska Gay?**)\n" +
                                $"\t**start poll <question|option1|option2|option3|etc....>**: Start a poll\n\t\t(e.g. **{Utility.prefix}vote start poll Who is best girl?|D.va|Mercy|Mei|Symmetra|Tracer|Widowmaker|Zarya**)\n" +
                                $"\t**for <choice>**: Vote for your choice in the currently running poll. Valid entries are (y, yes, n, no), or any of the options if it is a poll. (e.g. **{Utility.prefix}vote for D.va**)\n" +
                                $"\t**display**: re-displays currently running poll (e.g. **{Utility.prefix}vote display**)\n" +
                                $"\nVotes will be closed when one of the following happens; a majority has been reached and the winner cannot be changed with further votes, or **{voteTimeToString}** have passed.";

            await ReplyAsync(helpMsg);
        }

        [Command("quick_test")]
        [RequireRole("Cumlord")]
        public async Task QuickTest()
        {
            await ReplyAsync(string.Join("\n", ((SocketGuild)Context.Guild).Users.Where(x => !x.IsBot)));
        }

        [Command("close")]
        [RequireRole("Cumlord")]
        public async Task Close()
        {
            if (CurrentVote.IsStarted)
                await CurrentVote.Close();
        }

        [Command("set-vote-time")]
        [RequireRole("Cumlord")]
        [Alias("set_vote_time", "set_votetime", "set-votetime", "set_VoteTime", "set-VoteTime")]
        public async Task SetVoteTime(string minutes)
        {
            int minInt;

            if (int.TryParse(minutes, out minInt))
            {
                CurrentVote.VoteTime = TimeSpan.FromMilliseconds(minInt);
                string voteTimeToString = $"{CurrentVote.VoteTime.Hours} hour(s) and {CurrentVote.VoteTime.Minutes} minute(s)";
                await ReplyAsync($"VoteTime set to {voteTimeToString}.");
            }
            else
            {
                await ReplyAsync($"{minutes} isn't a valid integer, you FUCK.");
            }
        }

        [Command("for")]
        public async Task For([Remainder, Summary("The user's choice for the current vote.")] string choice)
        {
            if (CurrentVote.IsStarted)
            {
                bool hasVoted = CurrentVote.Votes.Keys.Contains(Context.User.Id);

                if (CurrentVote._VoteType == CurrentVote.VoteType.YesOrNo)
                {
                    choice = choice.ToLower();
                    choice = choice.Trim();
                    if (choice.Equals("y") || choice.Equals("yes"))
                    {
                        if (hasVoted)
                            CurrentVote.Votes[Context.User.Id] = "yes";
                        else
                            CurrentVote.Votes.Add(Context.User.Id, "yes");
                    }
                    else if (choice.Equals("n") || choice.Equals("no"))
                    {
                        if (hasVoted)
                            CurrentVote.Votes[Context.User.Id] = "no";
                        else
                            CurrentVote.Votes.Add(Context.User.Id, "no");
                    }
                    else
                    {
                        await ReplyAsync($"{choice} is not a valid selection for this poll. Please try again.");
                        return;
                    }
                }
                else
                {
                    if (CurrentVote.Options.Contains(choice))
                    {
                        if (hasVoted)
                            CurrentVote.Votes[Context.User.Id] = choice;
                        else
                            CurrentVote.Votes.Add(Context.User.Id, choice);
                    }
                    else
                    {
                        await ReplyAsync($"{choice} is not a valid selection for this poll. Please try again.");
                        return;
                    }
                }

                if (hasVoted)
                    await ReplyAsync($"Your vote has been changed to {choice}");
                else
                    await ReplyAsync($"Your vote for {choice} has been submitted.");

                CurrentVote.ResolveIfAble();
            }
            else
                await ReplyAsync("There is no vote currently taking place.");
        }

        [Command("display")]
        public async Task Display([Remainder, Summary("dummy param")] string dummy = null)
        {
            await ReplyAsync(CurrentVote.VoteText());
        }

        [Group("start")]
        public class StartModule : ModuleBase
        {
            private async Task DisplayVote()
            {
                await ReplyAsync(CurrentVote.VoteText());
            }

            private async Task<bool> CheckIfVoteIsAlreadyRunning()
            {
                if (CurrentVote.IsStarted)
                {
                    TimeSpan tr = CurrentVote.TimeRemaining();
                    string timeRemaining = $"{tr.Hours} hours, {tr.Minutes} minutes, and {tr.Seconds} seconds";
                    await ReplyAsync($"There is already a vote running. {timeRemaining} remains. A vote will be closed if a majority has been reached. The current poll is:\n");
                    await DisplayVote();
                }

                return CurrentVote.IsStarted;
            }

            [Command("yes/no")]
            [Alias("yes\\no")]
            public async Task YesNo([Remainder, Summary("The question that is being voted on")] string question)
            {
                if (CheckIfVoteIsAlreadyRunning().Result)
                    return;

                CurrentVote.Question = question;
                CurrentVote.Options.Add("yes");
                CurrentVote.Options.Add("no");
                CurrentVote._VoteType = CurrentVote.VoteType.YesOrNo;

                CurrentVote.Start(Context);
                await ReplyAsync($"A yes or no vote has been initiated by {Context.User.Username}! \"{question}?\"\ntype \"$vote for <your choice>\", your choice being yes or no, to vote.");
            }

            [Command("poll")]
            public async Task Poll([Remainder, Summary("The question and options for the poll")] string poll)
            {
                if (CheckIfVoteIsAlreadyRunning().Result)
                    return;

                string optionDisplay = string.Empty;
                string[] pollOptions1 = poll.Split('|');
                string[] pollOptions2 = new string[pollOptions1.Length - 1];
                string question = pollOptions1[0];
                Array.Copy(pollOptions1, 1, pollOptions2, 0, pollOptions2.Length);
                optionDisplay = string.Join("\n", pollOptions2);

                //test for duplicates
                var groupings = pollOptions2.GroupBy(x => x);
                var duplicate = groupings.Where(x => x.Count() > 1);

                if (duplicate.Count() > 0)
                {
                    await ReplyAsync("Error! Duplicate poll options were found. Poll has not been started");
                    return;
                }

                CurrentVote.Question = question;
                pollOptions2.ToList().ForEach(x => CurrentVote.Options.Add(x.Trim()));
                CurrentVote._VoteType = CurrentVote.VoteType.Poll;

                CurrentVote.Start(Context);

                await ReplyAsync($"A poll has been initiated by {Context.User.Username}! \"{question}?\"\ntype \"$vote for <your choice>\", your choice being one of the following:\n{optionDisplay}");
            }
        }
    }

    public static class CurrentVote
    {
        public enum VoteType
        {
            YesOrNo,
            Poll
        }
        public static string Question = string.Empty;
        public static List<string> Options = new List<string>();
        public static Dictionary<ulong, string> Votes = new Dictionary<ulong, string>();
        public static bool IsStarted = false;
        public static VoteType _VoteType;
        public static TimeSpan VoteTime = TimeSpan.FromMinutes(10);

        private static Timer votetimer;
        private static DateTime dueTime;
        private static List<SocketGuildUser> GuildUsers;
        private static ICommandContext context;

        public static void Start(ICommandContext Context)
        {
            IsStarted = true;
            context = Context;
            GuildUsers = ((SocketGuild)Context.Guild).Users.Where(x => !x.IsBot).ToList();

            // start timer
            votetimer = new Timer(VoteTime.TotalMilliseconds);
            votetimer.AutoReset = false;
            votetimer.Elapsed += new ElapsedEventHandler(OnVoteTimer);
            votetimer.Start();
            dueTime = DateTime.Now + VoteTime;
        }

        public static void ResolveIfAble()
        {
            // resolve vote if majority has been reached
            int majority = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(GuildUsers.Count) / 2.0));

            var currResults = Votes.Select(x => x.Value).GroupBy(x => x);

            foreach (var group in currResults)
            {
                if (group.Count() == majority)
                    Close();
            }
        }

        public static TimeSpan TimeRemaining()
        {
            return dueTime - DateTime.Now;
        }

        public static string VoteText(bool closing = false)
        {
            string votetext;

            if (IsStarted)
            {
                TimeSpan tr = TimeRemaining();
                string starter = closing ? string.Empty : "Current poll: ";
                string options = resultsText();
                string time = closing ? string.Empty : $"\n\n{tr.Hours} hour(s), {tr.Minutes} minute(s), and {tr.Seconds} second(s) remaining";

                votetext = $"{starter}{Question}?\n{options}{time}";
            }
            else
                votetext = "There is no vote currently taking place.";

            return votetext;
        }

        public async static Task Close()
        {
            if (Votes.Count > 0)
            {
                var winner = Votes.Select(x => x.Value).GroupBy(x => x).OrderByDescending(x => x.Count()).ElementAt(0);

                var ties = Votes.Select(x => x.Value).GroupBy(x => x).Where(x => x.Count() == winner.Count());

                if (ties.Count() > 1)
                {
                    await context.Channel.SendMessageAsync($"The vote has closed with a tie! The running poll was:\n{VoteText(closing: true)}\n\nThe winners were ({string.Join(", ", ties.Select(x => x.Key))}) with {winner.Count()} vote(s) each!");
                }
                else
                {
                    await context.Channel.SendMessageAsync($"A winner has been decided! The running poll was:\n{VoteText(closing: true)}\n\nThe winner was {winner.Key} with {winner.Count()} vote(s)!");
                }
            }
            else
                await context.Channel.SendMessageAsync($"The running poll was:\n{VoteText(closing: true)}\n\nThe vote has closed with no votes being cast.");

            Question = string.Empty;
            Options = new List<string>();
            Votes = new Dictionary<ulong, string>();
            IsStarted = false;
        }

        private static string resultsText()
        {
            List<string> resultsText = new List<string>();
            var currResults = Votes.Select(x => x.Value).GroupBy(x => x);

            foreach (string option in Options)
            {
                var grouping = currResults.Where(x => x.Key.Equals(option));

                if (grouping.Count() == 1)
                {
                    resultsText.Add($"{option}: {grouping.ElementAt(0).Count()}");
                }
                else
                {
                    resultsText.Add($"{option}: 0");
                }
            }

            return string.Join("\n", resultsText);
        }

        private static void OnVoteTimer(object source, ElapsedEventArgs e)
        {
            Close();
        }
    }
}
