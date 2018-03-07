using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cumbot
{
    class EightBallClass
    {
        private Random gen;
        private List<string> responses;

        public EightBallClass(Random gen)
        {
            this.gen = gen;

            this.responses = new List<string>();
            responses.Add("It is certain.");
            responses.Add("It is decidedly so.");
            responses.Add("Without a doubt.");
            responses.Add("Yes, definitely.");
            responses.Add("You may rely on it.");
            responses.Add("As I see it, yes.");
            responses.Add("Most likely.");
            responses.Add("Outlook good.");
            responses.Add("Yes.");
            responses.Add("Signs point to yes.");
            responses.Add("Reply hazy try again.");
            responses.Add("Ask again later.");
            responses.Add("Better not tell you now.");
            responses.Add("Cannot predict now.");
            responses.Add("Concentrate and ask again.");
            responses.Add("Don't count on it.");
            responses.Add("My reply is no.");
            responses.Add("My sources say no.");
            responses.Add("Outlook not so good.");
            responses.Add("Very doubtful.");
        }

        public string Query()
        {
            return responses[this.gen.Next(0, responses.Count)];
        }
    }
}
