using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Discord;
using Discord.WebSocket;
using Discord.Commands;

namespace Cumbot
{
    class Utility
    {
        public static ulong clientid = 275078911950323714;
        public static ulong guildid = 271050936833540103;
        public static ulong spamchannelid = 275106689538326529;
        public static ulong generalchannelid = 271050936833540103;
        public static ulong adventurechannelid = 271060974386020352;
        public static Dictionary<ulong, string> ids_to_names;
        public static Random numgen = new Random();
        public static string exe_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase).Substring(6);
        public static string code_path = Directory.GetParent(exe_path).Parent.ToString();
        public static char prefix = '$';
        public static TradeSession Trade;

        public static string RarityToEmoji(int rarity, ICommandContext Context)
        {
            string key = "";

            switch (rarity)
            {
                case 1:
                    key = "pepe_common";
                    break;
                case 2:
                    key = "pepe_uncommon";
                    break;
                case 3:
                    key = "pepe_rare";
                    break;
                case 4:
                    key = "pepe_mythic_rare";
                    break;
            }

            var es = Context.Guild.Emotes
                .Where(x => x.Name.Equals(key))
                .ToList()[0];



            return $"<:{es.Name}:{es.Id}>";
        }

        private static List<string> Edits(string query, List<string> dict)
        {
            List<char> letters = new List<char>();
            List<Tuple<string, string>> splits = new List<Tuple<string, string>>();
            List<string> deletes = new List<string>();
            List<string> transposes = new List<string>();
            List<string> replaces = new List<string>();
            List<string> inserts = new List<string>();
            List<string> editList = new List<string>();
            List<string> editSet = new List<string>();

            //get set of characters used in dict
            string longform = string.Join("", dict);
            foreach (char c in longform)
            {
                if (!letters.Contains(c))
                    letters.Add(c);
            }

            for (int i = 0; i < query.Length; i++)
            {
                splits.Add(new Tuple<string, string>(query.Substring(0, i), query.Substring(i)));
            }

            foreach (var split in splits)
            {
                if (!split.Item2.Equals(string.Empty))
                    deletes.Add(split.Item1 + split.Item2.Substring(1));

                if (split.Item2.Length > 1)
                    transposes.Add(split.Item1 + split.Item2.ElementAt(1) + split.Item2.ElementAt(0) + split.Item2.Substring(2));

                foreach (char c in letters)
                {
                    if (!split.Item2.Equals(string.Empty))
                        replaces.Add(split.Item1 + c + split.Item2.Substring(1));

                    inserts.Add(split.Item1 + c + split.Item2);
                }
            }

            editList.AddRange(deletes);
            editList.AddRange(transposes);
            editList.AddRange(replaces);
            editList.AddRange(inserts);
            foreach (string edit in editList)
            {
                if (!editSet.Contains(edit))
                    editSet.Add(edit);
            }

            return editSet;
        }

        public enum SpellCheckResultType
        {
            OK,
            BAD_SPELLING_MATCH_FOUND,
            BAD_SPELLING_NO_MATCH_FOUND
        }

        public static SpellCheckResultType CheckSpelling(string query, List<string> dict, out string correction, bool caseSensitive=false, int editNum=2)
        {
            correction = string.Empty;
            if (!caseSensitive)
            {
                dict = dict.Select(x => x.ToLower()).ToList();
                query = query.ToLower();
            }

            //save some computation power
            if (dict.Contains(query))
                return SpellCheckResultType.OK;

            List<string> results = new List<string>();
            List<string> currentRun = new List<string>() { query };
            List<string> prevRun = new List<string>() { query };
            for (int i = 0; i < editNum; i++)
            {
                currentRun.Clear();
                foreach (string s in prevRun)
                {
                    List<string> edits = Edits(s, dict);
                    results.AddRange(edits);
                    currentRun.AddRange(edits);
                }
                prevRun = currentRun.Select(x => x).ToList();
            }

            var MatchList = (
                from r in results
                join d in dict on
                r equals d
                select r
            );

            List<string> MatchSet = new List<string>();
            foreach (string match in MatchList)
            {
                if (!MatchSet.Contains(match))
                    MatchSet.Add(match);
            }

            if (MatchSet.Count == 0)
                return SpellCheckResultType.BAD_SPELLING_NO_MATCH_FOUND;
            else
            {
                correction = MatchSet[0];
                return SpellCheckResultType.BAD_SPELLING_MATCH_FOUND;
            }
        }

        public static async Task RandomMessage(SocketGuildUser insulted_user, SocketTextChannel channel)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(@"..\..\Insults.xml");

            XmlNodeList gameNodes = doc.DocumentElement.SelectNodes("/insult_group/game/insult");
            XmlNodeList generalNodes = doc.DocumentElement.SelectNodes("/insult_group/general/insult");
            XmlNodeList userNodes = doc.DocumentElement.SelectNodes($"/insult_group/{Utility.ids_to_names[insulted_user.Id]}/insult");
            //XmlNodeList userNodes = doc.DocumentElement.SelectNodes($"/insult_group/ska/insult");
            XmlNodeList groupToUse;

            int percentage = Utility.numgen.Next(1, 101);

            if (insulted_user.Game == null)
            {
                if (percentage <= 25)
                    groupToUse = userNodes;
                else
                    groupToUse = generalNodes;
            }
            else
            {
                if (percentage <= 25)
                    groupToUse = userNodes;
                else if (percentage <= 50)
                    groupToUse = gameNodes;
                else
                    groupToUse = generalNodes;
            }

            XmlNode insultNode = groupToUse[Utility.numgen.Next(0, groupToUse.Count)];
            string insult = insultNode.InnerText;

            insult = insult.Replace("^user^", insulted_user.Mention);
            if (insulted_user.Game != null)
                insult = insult.Replace("^game^", insulted_user.Game.ToString());

            if (insult.Contains("^image^"))
            {
                insult = insult.Replace("^image^", "");
                await channel.SendFileAsync(insultNode.NextSibling.InnerText, insult);
            }
            else
            {
                await channel.SendMessageAsync(insult);
            }
        }

        public static async Task RandomMessage(IUser insulted_user, IMessageChannel channel)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(@"..\..\Insults.xml");

            XmlNodeList gameNodes = doc.DocumentElement.SelectNodes("/insult_group/game/insult");
            XmlNodeList generalNodes = doc.DocumentElement.SelectNodes("/insult_group/general/insult");
            XmlNodeList userNodes = doc.DocumentElement.SelectNodes($"/insult_group/{Utility.ids_to_names[insulted_user.Id]}/insult");
            //XmlNodeList userNodes = doc.DocumentElement.SelectNodes($"/insult_group/ska/insult");
            XmlNodeList groupToUse;

            int percentage = Utility.numgen.Next(1, 101);

            if (insulted_user.Game == null)
            {
                if (percentage <= 25)
                    groupToUse = userNodes;
                else
                    groupToUse = generalNodes;
            }
            else
            {
                if (percentage <= 25)
                    groupToUse = userNodes;
                else if (percentage <= 50)
                    groupToUse = gameNodes;
                else
                    groupToUse = generalNodes;
            }

            XmlNode insultNode = groupToUse[Utility.numgen.Next(0, groupToUse.Count)];
            string insult = insultNode.InnerText;

            insult = insult.Replace("^user^", insulted_user.Mention);
            if (insulted_user.Game != null)
                insult = insult.Replace("^game^", insulted_user.Game.ToString());

            if (insult.Contains("^image^"))
            {
                insult = insult.Replace("^image^", "");
                await channel.SendFileAsync(insultNode.NextSibling.InnerText, insult);
            }
            else
            {
                await channel.SendMessageAsync(insult);
            }
        }
    }
}
