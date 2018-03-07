using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Data;
using System.Data.SQLite;
using System.Data.Common;
using Discord;
using Discord.Commands;

namespace Cumbot
{
    //to set the initiator and responder:
    //    the executing code in Program.cs needs a single TradeSession object reference to be used as the current trade session
    //    TradeSession fields: Context (for channel), InitiatorId, ResponderId, InitiatorCards, ResponderCards, InitiatorConfirmation, ResponderConfirmation
    //    TradeSession member functions: public TradeSession(), public Initialize(InitiatorId, InitiatorCards, Context), public Respond(ResponderId, ResponderCards), public Cancel(id), public Confirm(id), private Reset(), private Trade()
    //    The constructor should be empty
    //    Initialize will send the initiator's info to the object, initialize the Context for the current session, and start the time to live counter before calling Reset()
    //    Respond will send the responder's info to the object and display the ongoing trade message. Extend the timer by two minutes.
    //    Cancel is called by a user and cancels the current trade. The caller of cancel must be currently involved in the trade.
    //    Confirm() is called by the user with the $confirm command. The caller of confirm must be currently involved in the trade.
    //    Reset() will be called by the timer event if it reaches zero, or if a trader cancels.
    //          This will reset this object to its initial state. Initialize all fields, cancel any running timer.
    //    Once both parties have confirmed, Trade() will be called by the TradeSession, followed by Reset()
    //    The trading must be done in a manner such that the collection dates do not change, or the collect timers may be switched for the parties involved

    struct CardToTrade
    {
        public int id;
        public string Name;
        public int Quantity;
        public bool Holo;
        public int Rarity;

        public CardToTrade(int id, string Name, int Quantity, bool Holo, int Rarity)
        {
            this.id = id;
            this.Name = Name;
            this.Quantity = Quantity;
            this.Holo = Holo;
            this.Rarity = Rarity;
        }
    }

    class TradeSession
    {
        private ICommandContext Context;
        private ulong InitiatorId;
        private ulong ResponderId;
        private List<CardToTrade> InitiatorCards;
        private List<CardToTrade> ResponderCards;
        private bool InitiatorConfirmation;
        private bool ResponderConfirmation;
        Timer resetTimer;

        private string connString;

        public TradeSession()
        {
            resetTimer = new Timer(1000/*mil*/ * 60/*sec*/ * 5/*min*/);
            connString = "Data Source=pepe.sqlite;Version=3";
            Reset();
        }

        public bool IsInitialized()
        {
            return InitiatorId != 0;
        }

        public async Task Initialize(ulong id, List<CardToTrade> cards, ICommandContext context)
        {
            InitiatorId = id;
            InitiatorCards = cards;
            Context = context;
            
            resetTimer.Start();
            await Context.Channel.SendMessageAsync("A trade has been successfully initialized! Waiting for response...");
        }

        public async Task Respond(ulong id, List<CardToTrade> cards)
        {
            if (id == InitiatorId)
            {
                await Context.Channel.SendMessageAsync("You cannot respond to a trade you've initialized!");
                return;
            }

            ResponderId = id;
            ResponderCards = cards;
            resetTimer.Interval += TimeSpan.FromMinutes(2).TotalMilliseconds;

            IGuildUser init = await Context.Guild.GetUserAsync(InitiatorId);
            IGuildUser resp = await Context.Guild.GetUserAsync(ResponderId);

            string tradeMessage = $"Response received! The current trade is as follows:\n\t{init.Username} is trading\n";

            foreach (CardToTrade c in InitiatorCards)
            {
                string holo = c.Holo ? " (Holo)" : string.Empty;
                tradeMessage += $"\t\t{c.Quantity} {c.Name}{holo} {Utility.RarityToEmoji(c.Rarity, Context)}\n";
            }

            tradeMessage += $"\tfor {resp.Username}'s\n";

            foreach (CardToTrade c in ResponderCards)
            {
                string holo = c.Holo ? " (Holo)" : string.Empty;
                tradeMessage += $"\t\t{c.Quantity} {c.Name}{holo} {Utility.RarityToEmoji(c.Rarity, Context)}\n";
            }

            tradeMessage += "If both parties are satisfied with this trade, please enter $confirm. If not, please enter $cancel.";

            await Context.Channel.SendMessageAsync(tradeMessage);
        }

        public async Task Cancel(ulong id)
        {
            if (InitiatorId == id || ResponderId == id)
            {
                Reset();

                IGuildUser canceler = await Context.Guild.GetUserAsync(id);
                await Context.Channel.SendMessageAsync($"Trade cancelled by {canceler.Username}!");
            }
            else
            {
                await Context.Channel.SendMessageAsync("Hey buddy, you can't cancel a trade you're not involved in!");
            }
        }

        public async Task Confirm(ulong id)
        {
            if (InitiatorId != 0 && ResponderId != 0)
            {
                IGuildUser confirmer = await Context.Guild.GetUserAsync(id);

                if (id == InitiatorId)
                {
                    InitiatorConfirmation = true;
                    await Context.Channel.SendMessageAsync($"Initiator {confirmer.Username} has confirmed the trade!");
                }
                else if (id == ResponderId)
                {
                    ResponderConfirmation = true;
                    await Context.Channel.SendMessageAsync($"Responder {confirmer.Username} has confirmed the trade!");
                }
                else
                {
                    await Context.Channel.SendMessageAsync("Hey buddy, you can't confirm a trade you're not involved in!");
                }

                if (ResponderConfirmation && InitiatorConfirmation)
                    await Trade();
            }
            else
            {
                await Context.Channel.SendMessageAsync("A trade has not been established to confirm!");
            }
        }

        private async Task Trade()
        {
            using (var conn = new SQLiteConnection(connString))
            {
                await conn.OpenAsync();

                //add initiator's cards to responder's collection
                //remove initiator's cards from collection
                foreach (CardToTrade card in InitiatorCards)
                {
                    var command = new SQLiteCommand(
                        @"insert into collection (user_id, card_id, foil)
                          select ? as id, card_id, foil from collection
                          where user_id = ? and foil = ? and card_id = ?
                          limit ?", conn);
                    command.Parameters.AddWithValue("responder_id", ResponderId);
                    command.Parameters.AddWithValue("user_id", InitiatorId);
                    command.Parameters.AddWithValue("foil", card.Holo);
                    command.Parameters.AddWithValue("card_id", card.id);
                    command.Parameters.AddWithValue("limit", card.Quantity);
                    await command.ExecuteNonQueryAsync();

                    command = new SQLiteCommand(
                        @"delete from collection
                          where col_id in (
                            select col_id from collection
                            where user_id = ? and card_id = ? and foil = ?
                            limit 2
                          )", conn);
                    command.Parameters.AddWithValue("user_id", InitiatorId);
                    command.Parameters.AddWithValue("card_id", card.id);
                    command.Parameters.AddWithValue("foil", card.Holo);
                    command.Parameters.AddWithValue("limit", card.Quantity);
                    await command.ExecuteNonQueryAsync();
                }

                //now do the opposite
                foreach (CardToTrade card in ResponderCards)
                {
                    var command = new SQLiteCommand(
                        @"insert into collection (user_id, card_id, foil)
                          select ? as id, card_id, foil from collection
                          where user_id = ? and foil = ? and card_id = ?
                          limit ?", conn);
                    command.Parameters.AddWithValue("initiator_id", InitiatorId);
                    command.Parameters.AddWithValue("user_id", ResponderId);
                    command.Parameters.AddWithValue("foil", card.Holo);
                    command.Parameters.AddWithValue("card_id", card.id);
                    command.Parameters.AddWithValue("limit", card.Quantity);
                    await command.ExecuteNonQueryAsync();

                    command = new SQLiteCommand(
                        @"delete from collection
                          where col_id in (
                            select col_id from collection
                            where user_id = ? and card_id = ? and foil = ?
                            limit 2
                          )", conn);
                    command.Parameters.AddWithValue("user_id", ResponderId);
                    command.Parameters.AddWithValue("card_id", card.id);
                    command.Parameters.AddWithValue("foil", card.Holo);
                    command.Parameters.AddWithValue("limit", card.Quantity);
                    await command.ExecuteNonQueryAsync();
                }

            }

            await Context.Channel.SendMessageAsync("Trade complete!");

            Reset();
        }
        
        private void Reset()
        {
            InitiatorId = 0;
            ResponderId = 0;
            InitiatorCards = new List<CardToTrade>();
            ResponderCards = new List<CardToTrade>();
            InitiatorConfirmation = false;
            ResponderConfirmation = false;
            resetTimer.Stop();
            resetTimer.Close();
            resetTimer = new Timer(1000/*mil*/ * 60/*sec*/ * 5/*min*/);
            resetTimer.Elapsed += new ElapsedEventHandler(this.OnResetTimer);
        }

        private async void OnResetTimer(object source, ElapsedEventArgs e)
        {
            Reset();
            await Context.Channel.SendMessageAsync("The trade timer has elapsed. Trade cancelled....");
        }
    }
}
