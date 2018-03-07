using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SQLite;
using System.Data.Common;
using System.IO;
using System.Diagnostics;
using System.Drawing.Imaging;
using Discord.Commands;
using Discord;
using ImageProcessor;
using ImageProcessor.Imaging;
using ImageProcessor.Imaging.Formats;

namespace Cumbot
{
    enum Rarity
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        MythicRare = 4
    }

    public class PepeModule : ModuleBase
    {
        private string dbfile;
        private string connString;
        private bool debug;

        public PepeModule()
        {
            this.dbfile = "pepe.sqlite";
            this.connString = "Data Source=pepe.sqlite;Version=3";
            this.debug = false;
            SetDebug();
        }

        [Conditional("DEBUG")]
        private void SetDebug()
        {
            this.debug = true;
        }
        
        private async Task<DataTable> GetCardList()
        {
            DataTable dt = new DataTable();

            using (var conn = new SQLiteConnection(connString))
            {
                await conn.OpenAsync();

                var command = new SQLiteCommand("select * from pepe_cards", conn);
                SQLiteDataAdapter da = new SQLiteDataAdapter();
                da.SelectCommand = command;
                da.Fill(dt);
            }
            return dt;
        }

        private async Task<DataTable> GetCollection(ulong id)
        {
            DataTable dt = new DataTable();

            using (var conn = new SQLiteConnection(connString))
            {
                await conn.OpenAsync();

                var command = new SQLiteCommand("select pepe_cards.id, name, rarity, image_extension, foil, count(name) as cnt from pepe_cards join collection on pepe_cards.id = collection.card_id where collection.user_id = ? group by name, foil order by rarity, name", conn);
                command.Parameters.AddWithValue("user_id", id.ToString());
                SQLiteDataAdapter da = new SQLiteDataAdapter();
                da.SelectCommand = command;
                da.Fill(dt);
            }
                return dt;
        }

        private String FilenameToCardname(string name)
        {
            name = name.Replace("[q]", "?");
            name = name.Replace("[e]", "!");

            return name;
        }

        private String CardnameToFilename(string name)
        {
            name = name.Replace("?", "[q]");
            name = name.Replace("!", "[e]");

            return name;
        }

        [Command("pepe-list-collection")]
        public async Task ListCollection()
        {
            await ReplyAsync($"{Context.User.Username}'s COLLECTION");

            DataTable dt = await GetCollection(Context.User.Id);
            string message = String.Empty;

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                string rarity = String.Empty;

                switch(Convert.ToInt32(dt.Rows[i]["rarity"]))
                {
                    case 1:
                        rarity = "Common";
                        break;
                    case 2:
                        rarity = "Uncommon";
                        break;
                    case 3:
                        rarity = "Rare";
                        break;
                    case 4:
                        rarity = "Mythic Rare";
                        break;
                }

                string aad = Convert.ToBoolean(dt.Rows[i]["foil"]) ? " (Foil)" : String.Empty;
                message += $"{i+1}) {dt.Rows[i]["name"]}{aad}, {rarity}. Quantity: {dt.Rows[i]["cnt"]}\n";
            }

            message.Trim();

            await ReplyAsync(message);
        }

        [Command("pepe-show-collection")]
        public async Task ShowCollection()
        {
            /*await ReplyAsync($"{Context.User.Username}'s COLLECTION");

            DataTable dt = await GetCollection(Context.User.Id);

            CollectionBox cb = new CollectionBox(dt, Context);
            cb.ImageStreamAsyncDelegate = GetCardImageStreamAsync;
            await cb.SendMessage(0);*/

            if (debug)
            {
                await ReplyAsync("Cumbot is currently in DEBUG mode. The following collection is not your real, live collection.");
            }

            List<Stream> pages = await GetCollectionPages(Context);

            if (pages.Count == 0)
            {
                await ReplyAsync("No cards to display, please collect a pepe with $pepe-collect....");
                return;
            }

            CollectionBox2 cb = new CollectionBox2(pages, Context);
            await cb.SendMessage();
        }
        
        private async Task<List<Stream>> GetCollectionPages(ICommandContext ctxt)
        {
            List<Stream> returnstream = new List<Stream>();

            byte[] bgBytes = File.ReadAllBytes(@"..\..\img\pepe\collection_bg.png");
            byte[] overBytes = File.ReadAllBytes(@"..\..\img\pepe\collection_overlay.png");
            byte[] frameBytes = File.ReadAllBytes(@"..\..\img\pepe\card_frame.png");
            MemoryStream frameStream = new MemoryStream(frameBytes); 

            //DataTable dt = await GetCollection(ctxt.User.Id);
            DataTable dt = await GetCollection(ctxt.User.Id);

            if (dt.Rows.Count == 0)
                return new List<Stream>();

            //card size
            int[] cardSize = new int[] { 182, 259 };
            //image dimensions
            int[] imageDimension = new int[] { 1920, 1080 };

            int maxCards = 30,
                maxstack = 3,
                maxPerRow = 10,
                xPadding = 4,
                yPadding = 60,
                yOffset = 160,
                yStackOffset = 16;

            int prevDataRow = -1;
            int currDataRow = 0;

            int slots = 0;

            foreach (DataRow row in dt.Rows)
            {
                int num = Convert.ToInt32(row["cnt"]) / maxstack;

                if (Convert.ToInt32(row["cnt"]) % maxstack != 0)
                    num++;

                slots += num;
            }

            int pages = slots / maxCards;

            if (slots % maxCards != 0)
                pages++;

            await ReplyAsync("Rendering (this may take up to 30 seconds)....");

            //run this code once per page
            for (int i = 0; i < pages; i++)
            {
                //determine number of cards on page
                int cards = slots > maxCards ? maxCards : slots;

                //determine number of rows
                int numRows = cards / maxPerRow;
                if (cards % maxPerRow != 0)
                    numRows++;

                //determine best distribution of cards e.g. 23 -> [7, 8, 8]
                //23 / 3 = 7
                //7 * 3 = 21
                //23 - 21 = 2
                //remainder of 2, ergo [7, 8, 8]
                //17 / 2 = 8
                //8 * 2 = 16
                //17-16 = 1
                //remainder of 1, ergo [6,7]
                int evenDiv = cards / numRows;
                int product = evenDiv * numRows;
                int remainder = cards - product;
                int[] rows = new int[numRows];
                for (int j = 0; j < numRows; j++)
                {
                    if (remainder == 0)
                        rows[j] = evenDiv;
                    else
                    {
                        rows[j] = evenDiv + 1;
                        remainder--;
                    }

                }

                rows = rows.Reverse().ToArray();

                //determine row positions based on cards on page
                Tuple<int, int>[] cardPos = new Tuple<int, int>[cards];
                int totalHeight = numRows * cardSize[1] + yPadding * (numRows - 1);
                int ySlack = imageDimension[1] - yOffset - totalHeight;
                int yStart = ySlack / 2 + yOffset;


                int counter = 0;
                for (int j = 0; j < numRows; j++)
                {
                    int rowWidth = cardSize[0] * rows[j] + (xPadding * (rows[j] - 1));
                    int xSlack = imageDimension[0] - rowWidth;
                    int xStart = xSlack / 2;
                    for(int k = 0; k < rows[j]; k++)
                    {
                        int xPos = xStart + (cardSize[0] + xPadding) * k;
                        int yPos = yStart + (cardSize[1] + yPadding) * j;
                        cardPos[counter] = new Tuple<int, int>(xPos, yPos);
                        counter++;
                    }
                }

                using (MemoryStream bgStream = new MemoryStream(bgBytes))
                {
                    using (MemoryStream overStream = new MemoryStream(overBytes))
                    {
                        using (ImageFactory imageFactory = new ImageFactory())
                        {
                            imageFactory.Format(new PngFormat());

                            MemoryStream outStream = new MemoryStream();
                            List<ImageLayer> overlays = new List<ImageLayer>();
                            int currCardPos = 0;
                            int prevCardPos = 0;
                            int currStackLvl = 0;

                            while (currCardPos < cards)
                            {
                                DataRow currCard = dt.Rows[currDataRow];
                                Stream cs;
                                if (prevDataRow == currDataRow)
                                    cs = frameStream;
                                else
                                    cs = await GetCardImageStreamAsync(currCard["name"].ToString(), currCard["image_extension"].ToString(), Convert.ToInt32(currCard["rarity"]), Convert.ToBoolean(currCard["foil"]));
                                cs.Position = 0;

                                //resize layer for resizing card
                                ResizeLayer cardResizeLayer = new ResizeLayer(new System.Drawing.Size(cardSize[0], cardSize[1]), ResizeMode.Stretch);

                                //resize card
                                imageFactory.Load(cs)
                                   .Resize(cardResizeLayer)
                                   .Save(outStream);


                                //overlay layer for overlaying card
                                int y = cardPos[currCardPos].Item2;
                                y -= yStackOffset * currStackLvl;

                                ImageLayer cardImageLayer = new ImageLayer();
                                cardImageLayer.Image = System.Drawing.Image.FromStream(outStream);
                                cardImageLayer.Position = new System.Drawing.Point(cardPos[currCardPos].Item1, y);
                                overlays.Add(cardImageLayer);

                                dt.Rows[currDataRow]["cnt"] = Convert.ToInt32(dt.Rows[currDataRow]["cnt"]) - 1;

                                if (Convert.ToInt32(dt.Rows[currDataRow]["cnt"]) == 0)
                                {
                                    currDataRow++;

                                    currStackLvl = 0;
                                    currCardPos++;
                                }
                                else
                                {
                                    currStackLvl++;

                                    if (currStackLvl >= maxstack)
                                    {
                                        currStackLvl = 0;
                                        currCardPos++;
                                    }
                                }

                                prevCardPos = currCardPos;

                                if (!cs.Equals(frameStream))
                                    cs.Dispose();
                            }

                            imageFactory.Load(bgStream);

                            foreach (ImageLayer layer in overlays)
                                imageFactory.Overlay(layer);


                            TextLayer titleTextLayer = new TextLayer();
                            titleTextLayer.Text = $"{ctxt.User.Username.ToUpper()}'s COLLECTION: PAGE {i + 1} OF {pages}";
                            titleTextLayer.FontFamily = new System.Drawing.FontFamily("Beleren");
                            titleTextLayer.FontSize = 34;
                            System.Drawing.SizeF titlesize = System.Drawing.Graphics.FromImage(overlays[0].Image).MeasureString(titleTextLayer.Text, new System.Drawing.Font(titleTextLayer.FontFamily, titleTextLayer.FontSize, System.Drawing.GraphicsUnit.Pixel));
                            titleTextLayer.Position = new System.Drawing.Point(imageDimension[0] / 2 - (int)titlesize.Width / 2, yOffset / 2 - (int)titlesize.Height / 2);
                            titleTextLayer.FontColor = System.Drawing.Color.Black;

                            ImageLayer overImageLayer = new ImageLayer();
                            overImageLayer.Image = System.Drawing.Image.FromStream(overStream);
                            overImageLayer.Position = new System.Drawing.Point(0, 0);
                            overImageLayer.Opacity = 40;

                            imageFactory.Overlay(overImageLayer)
                                        .Watermark(titleTextLayer)
                                        .Save(outStream);
                            returnstream.Add(outStream);
                        }
                    }
                }
                slots -= cards;
            }
            GC.Collect();
            frameStream.Dispose();
            return returnstream;
        }

        private Task<Stream> GetCardImageStreamAsync(string name, string extension, int rarity, bool foil)
        {
            var result = Task.Run(() => GetCardImageStream(name, extension, rarity, foil));
            return result;
        }

        private Stream GetCardImageStream(string name, string extension, int rarity, bool foil)
        {
            string filename = CardnameToFilename(name);

            //get pepe
            string path = @"..\..\img\pepe\",
                   emblemPath = @"..\..\img\pepe\rarity_system\",
                   rarityText = String.Empty;

            MemoryStream cardStream = new MemoryStream();

            switch (rarity)
            {
                case 1:
                    path += @"common\";
                    emblemPath += @"pepe_common.png";
                    rarityText = "Frog";
                    break;
                case 2:
                    path += @"uncommon\";
                    emblemPath += @"pepe_uncommon.png";
                    rarityText = "Dire Frog";
                    break;
                case 3:
                    path += @"rare\";
                    emblemPath += @"pepe_rare.png";
                    rarityText = "Hate Speech";
                    break;
                case 4:
                    path += @"mythic_rare\";
                    emblemPath += @"pepe_mythic_rare.png";
                    rarityText = "Legendary Hate Speech";
                    break;
            }

            path += filename + extension;

            byte[] pepeBytes = File.ReadAllBytes(path),
                   emblemBytes = File.ReadAllBytes(emblemPath),
                   logoBytes = File.ReadAllBytes(@"..\..\img\pepe\rarity_system\pepe.png"),
                   frameBytes = File.ReadAllBytes(@"..\..\img\pepe\card_frame.png"),
                   blankBytes = File.ReadAllBytes(@"..\..\img\pepe\blank_layer.png"),
                   foilBytes = File.ReadAllBytes(@"..\..\img\pepe\foil.png");

            using (MemoryStream blankStream = new MemoryStream(blankBytes))
            {
                using (MemoryStream pepeStream = new MemoryStream(pepeBytes))
                {
                    using (MemoryStream frameStream = new MemoryStream(frameBytes))
                    {
                        using (MemoryStream logoStream = new MemoryStream(logoBytes))
                        {
                            using (MemoryStream emblemStream = new MemoryStream(emblemBytes))
                            {
                                using (ImageFactory imageFactory = new ImageFactory())
                                {
                                    MemoryStream outStream = new MemoryStream();

                                    //resize layer for resizing pepe
                                    ResizeLayer pepeResizeLayer = new ResizeLayer(new System.Drawing.Size(659, 774), ResizeMode.Stretch);

                                    //resize pepe
                                    imageFactory.Load(pepeStream)
                                        .Resize(pepeResizeLayer)
                                        .Save(outStream);

                                    //overlay layer for overlaying pepe
                                    ImageLayer pepeImageLayer = new ImageLayer();
                                    pepeImageLayer.Image = System.Drawing.Image.FromStream(outStream);
                                    pepeImageLayer.Position = new System.Drawing.Point(38, 111);

                                    //overlay layer for overlaying card frame
                                    ImageLayer frameImageLayer = new ImageLayer();
                                    frameImageLayer.Image = System.Drawing.Image.FromStream(frameStream);

                                    //resize logo
                                    ResizeLayer logoResizeLayer = new ResizeLayer(new System.Drawing.Size(80, 64), ResizeMode.Stretch);
                                    imageFactory.Load(logoStream)
                                        .Resize(logoResizeLayer)
                                        .Save(outStream);

                                    //overlay layer for overlaying logo
                                    ImageLayer logoImageLayer = new ImageLayer();
                                    logoImageLayer.Image = System.Drawing.Image.FromStream(outStream);
                                    logoImageLayer.Position = new System.Drawing.Point(332, 886);

                                    //resize rarity emblem
                                    ResizeLayer emblemResizeLayer = new ResizeLayer(new System.Drawing.Size(40, 32), ResizeMode.Stretch);
                                    imageFactory.Load(emblemStream)
                                        .Resize(emblemResizeLayer)
                                        .Save(outStream);

                                    //overlay layer for overlaying emblem
                                    ImageLayer emblemImageLayer = new ImageLayer();
                                    emblemImageLayer.Image = System.Drawing.Image.FromStream(outStream);
                                    emblemImageLayer.Position = new System.Drawing.Point(640, 905);

                                    //calculate width of card name in Beleren font
                                    int fontSize = 42;

                                    System.Drawing.SizeF textsize = System.Drawing.Graphics.FromImage(emblemImageLayer.Image).MeasureString(name, new System.Drawing.Font("Beleren", fontSize, System.Drawing.GraphicsUnit.Pixel));


                                    while (textsize.Width > 644)
                                    {
                                        fontSize--;
                                        textsize = System.Drawing.Graphics.FromImage(emblemImageLayer.Image).MeasureString(name, new System.Drawing.Font("Beleren", fontSize, System.Drawing.GraphicsUnit.Pixel));
                                    }

                                    //text layer for card name
                                    TextLayer nameTextLayer = new TextLayer();
                                    nameTextLayer.Text = name;
                                    nameTextLayer.FontFamily = new System.Drawing.FontFamily("Beleren");
                                    nameTextLayer.FontSize = fontSize;
                                    nameTextLayer.Position = new System.Drawing.Point(46, 49 + 23 - (int)textsize.Height / 2);
                                    nameTextLayer.FontColor = System.Drawing.Color.Black;

                                    //text layer for rarity text

                                    fontSize = 34;
                                    textsize = System.Drawing.Graphics.FromImage(emblemImageLayer.Image).MeasureString(rarityText, new System.Drawing.Font("Beleren", fontSize, System.Drawing.GraphicsUnit.Pixel));

                                    while (textsize.Width > 260)
                                    {
                                        fontSize--;
                                        textsize = System.Drawing.Graphics.FromImage(emblemImageLayer.Image).MeasureString(rarityText, new System.Drawing.Font("Beleren", fontSize, System.Drawing.GraphicsUnit.Pixel));
                                    }

                                    TextLayer rarityTextLayer = new TextLayer();
                                    rarityTextLayer.Text = rarityText;
                                    rarityTextLayer.FontFamily = new System.Drawing.FontFamily("Beleren");
                                    rarityTextLayer.FontSize = fontSize;
                                    rarityTextLayer.Position = new System.Drawing.Point(51, 894 + 23 - (int)textsize.Height / 2);
                                    rarityTextLayer.FontColor = System.Drawing.Color.Black;

                                    //text layer for pepe text
                                    System.Drawing.SizeF pepesize = System.Drawing.Graphics.FromImage(emblemImageLayer.Image).MeasureString("Pepe", new System.Drawing.Font("Beleren", fontSize, System.Drawing.GraphicsUnit.Pixel));
                                    TextLayer pepeTextLayer = new TextLayer();
                                    pepeTextLayer.Text = "Pepe";
                                    pepeTextLayer.FontFamily = new System.Drawing.FontFamily("Beleren");
                                    pepeTextLayer.FontSize = 34;
                                    pepeTextLayer.Position = new System.Drawing.Point(580 - (int)pepesize.Width, 899);
                                    pepeTextLayer.FontColor = System.Drawing.Color.Black;

                                    imageFactory.Load(blankStream)
                                        .Overlay(pepeImageLayer)
                                        .Overlay(frameImageLayer)
                                        .Overlay(logoImageLayer)
                                        .Overlay(emblemImageLayer)
                                        .Watermark(nameTextLayer)
                                        .Watermark(rarityTextLayer)
                                        .Watermark(pepeTextLayer);

                                    if (foil)
                                    {
                                        using (MemoryStream foilStream = new MemoryStream(foilBytes))
                                        {
                                            //overlay layer for overlaying foil
                                            ImageLayer foilLayer = new ImageLayer();
                                            foilLayer.Image = System.Drawing.Image.FromStream(foilStream);
                                            foilLayer.Opacity = 20;

                                            imageFactory.Overlay(foilLayer);
                                        }
                                    }

                                    imageFactory.Format(new PngFormat())
                                    .Save(outStream);
                                    cardStream = outStream;
                                }
                            }
                        }
                    }
                }
            }
            return cardStream;

            //layers, from foreground to background
            //1. foil, if applicable
            //2. text
            //3. emblem
            //4. card frame
            //5. pepe

            //the pepe must be streched to 659 x 774 and will be placed on the image at (38,111)
            //the text must be sized to fit a box of size 644x46 at (46,44)
            //the emblem will be shrunk to 82x73 at sit at (327,879)
        }

        [Command("pepe-test-display")]
        [RequireRole("Cumlord")]
        public async Task TestDisplay(string holo = null)
        {
            bool h = false;
            if (holo != null)
                h = true;
            Stream img = await GetCardImageStreamAsync("Cool Pepe", ".jpg", 1, h);
            await Context.Channel.SendFileAsync(img, "test.png");
        }

        [Command("pepe-test-listcards")]
        [Summary("Is this shit working?")]
        [RequireRole("Cumlord")]
        public async Task Test()
        {
            using (SQLiteConnection conn = new SQLiteConnection(this.connString))
            {
                await conn.OpenAsync();
                SQLiteCommand command = new SQLiteCommand("select * from pepe_cards", conn);
                DbDataReader dr = await command.ExecuteReaderAsync();

                while (await dr.ReadAsync())
                {
                    await ReplyAsync($"{dr["id"]}: {dr["name"]} ({dr["rarity"]})");
                }
            }
        }
        [Command("pepe-test-rarities")]
        [Summary("Is this shit working?")]
        [RequireRole("Cumlord")]
        public async Task ListRarities()
        {
            using (SQLiteConnection conn = new SQLiteConnection(this.connString))
            {
                await conn.OpenAsync();
                SQLiteCommand command = new SQLiteCommand("select * from rarities", conn);
                DbDataReader dr = await command.ExecuteReaderAsync();

                while (await dr.ReadAsync())
                {
                    await ReplyAsync($"{dr["id"]}: {dr["rarity_name"]}");
                }
            }
        }

        [Command("pepe-populate-rarity")]
        [Summary("Init pepe rarity table")]
        [RequireRole("Cumlord")]
        public async Task PopulateRarity()
        {
            string query = @"INSERT INTO rarities (rarity_name)
                             VALUES (?)";
            //VALUES ('Common', 'Uncommon', 'Rare', 'Mythic Rare')";
            List<String> rarities = new List<string>
            {
                "Common",
                "Uncommon",
                "Rare",
                "Mythic Rare"
            };

            int numrows = 0;

            using (SQLiteConnection conn = new SQLiteConnection(this.connString))
            {
                await conn.OpenAsync();
                SQLiteCommand command = new SQLiteCommand("select * from rarities", conn);
                SQLiteDataAdapter da = new SQLiteDataAdapter();
                DataTable dt = new DataTable();
                da.SelectCommand = command;
                da.Fill(dt);
                numrows = dt.Rows.Count;
            }

            if (numrows == 0)
            {
                using (SQLiteConnection conn = new SQLiteConnection(this.connString))
                {
                    await conn.OpenAsync();

                    foreach (var rarity in rarities)
                    {
                        SQLiteCommand command = new SQLiteCommand(query, conn);
                        command.Parameters.AddWithValue("rarity_name", rarity);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                await ReplyAsync("Rarities populated.");
            }
            else
            {
                await ReplyAsync("Rarities already populated");
            }
        }

        [Command("pepe-update-cards")]
        [Summary("Updates local database with new cards in folders")]
        [RequireRole("Cumlord")]
        public async Task UpdateCards()
        {
            var common = Directory.GetFiles(@"..\..\img\pepe\common");
            var uncommon = Directory.GetFiles(@"..\..\img\pepe\uncommon");
            var rare = Directory.GetFiles(@"..\..\img\pepe\rare");
            var mythic_rare = Directory.GetFiles(@"..\..\img\pepe\mythic_rare");
            List<string[]> cards = new List<string[]> { common, uncommon, rare, mythic_rare };
            int rowsInserted = 0;

            Dictionary<Rarity, int> cardsInserted = new Dictionary<Rarity, int>();
            cardsInserted.Add(Rarity.Common, 0);
            cardsInserted.Add(Rarity.Uncommon, 0);
            cardsInserted.Add(Rarity.Rare, 0);
            cardsInserted.Add(Rarity.MythicRare, 0);

            using (var conn = new SQLiteConnection(connString))
            {
                await conn.OpenAsync();

                using (var transaction = conn.BeginTransaction())
                {
                    for (int i = 0; i < cards.Count; i++)
                    {
                        foreach (var card in cards[i])
                        {
                            String cardname = Path.GetFileNameWithoutExtension(card);
                            string extension = Path.GetExtension(card);
                            SQLiteCommand command = new SQLiteCommand("insert or ignore into pepe_cards (name, rarity, image_extension) VALUES (?,?,?)", conn);
                            command.Parameters.AddWithValue("name", FilenameToCardname(cardname));
                            command.Parameters.AddWithValue("rarity", i + 1);
                            command.Parameters.AddWithValue("image_extension", extension);

                            int rowNum = await command.ExecuteNonQueryAsync();
                            rowsInserted += rowNum;
                            cardsInserted[(Rarity)(i + 1)] += rowNum;
                        }
                    }

                    transaction.Commit();
                }
            }

            await ReplyAsync($"{cardsInserted[Rarity.Common]} common card(s) inserted.\n" +
                             $"{cardsInserted[Rarity.Uncommon]} uncommon card(s) inserted.\n" +
                             $"{cardsInserted[Rarity.Rare]} rare card(s) inserted.\n" +
                             $"{cardsInserted[Rarity.MythicRare]} mythic rare card(s) inserted.\n" +
                             $"{rowsInserted} card(s) inserted in total.");
        }

        [Command("pepe-trade")]
        public async Task TradeCards([Remainder] string argument)
        {
            /*To put up for trade one holo Cool Pepe, 2 Peponettas, and 3 Stoned Pepes, type the following:
                $pepe-trade Cool Pepe Holo, Peponetta 2, 3 Stoned Pepe

            The number that you want to trade or the word "Holo" can be in any position relative to the entry in the list. The card names are also case insensitive.
            To help explain this, the following commands all do the same thing as the first:
                $pepe-trade holo cool pepe, 2 Peponetta, 3 Stoned Pepe
                $pepe-trade Cool Pepe Holo, PEPONETTA 2, Stoned Pepe 3
                $pepe-trade Cool Pepe Holo 1, Peponetta 2, 3 StOnEd PePe
            
            The way this works is as follows:
            
            $pepe-trade Cool Pepe Holo, Peponetta 2, 3 Stoned Pepe
                        ------------------------------------------ = argument
                        --------------  -----------  ------------- = sub-arguments
                        ---- ---- ----  --------- -  - ------ ---- = semi-arguments
            split argument by comma
            split each sub-argument by space
                if semi-argument = holo or h (case insensitive), get holo card
                else if semi-argument = number, get that many of card
                then remove these semi-arguments and rejoin new sub-argument on space
            check for misspelled cards
                detect mispellings and, if they exist, list them and end command
            check if caller has the cards to trade, if not, inform them and end command
            if no initiator has been set, set caller as initiator and initialize a trade timer of five minutes. After five minutes, the trade request will be canceled (reset initiator and responder).
            else if initiator has been set, set caller as responder
            if initiator and caller are set, display ongoing trade message and await confirmation*/

            //to set the initiator and responder:
            //    the executing code in Program.cs needs a single TradeSession object reference to be used as the current trade session
            //    TradeSession fields: Context (for channel), InitiatorId, ResponderId, InitiatorCards, ResponderCards, InitiatorConfirmation, ResponderConfirmation
            //    TradeSession member functions: public TradeSession(), public Initialize(InitiatorId, InitiatorCards, Context), public Respond(ResponderId, ResponderCards), public Reset(id), public Confirm(id), private Trade()
            //    The constructor should be empty
            //    Initialize will send the initiator's info to the object, initialize the Context for the current session, and start the time to live counter before calling Reset()
            //    Respond will send the responder's info to the object and display the ongoing trade message. Extend the timer by two minutes.
            //    Reset() will be called by the timer event if it reaches zero, or if a trader cancels. The caller of cancel must be currently involved in the trade.
            //          This will reset this object to its initial state. Initialize all fields, cancel any running timer.
            //    Confirm() is called by the user with the $confirm command. The caller of confirm must be currently involved in the trade.
            //    Once both parties have confirmed, Trade() will be called by the TradeSession, followed by Reset()
            //    The trading must be done in a manner such that the collection dates do not change, or the collect timers may be switched for the parties involved

            List<string> sub_arguments = argument.Split(',').ToList();

            List<Tuple<string, string, Utility.SpellCheckResultType>> spelling_errors = new List<Tuple<string, string, Utility.SpellCheckResultType>>();

            List<CardToTrade> CardsToTrade = new List<CardToTrade>();

            for (int i = 0; i < sub_arguments.Count(); i++)
            {
                DataTable cl = await GetCardList();
                CardToTrade c = new CardToTrade();
                c.Holo = false;
                c.Quantity = 1;
                bool qset = false;

                List<string> semi_arguments = sub_arguments[i].Split(' ').ToList();

                List<string> semi_to_remove = new List<string>();

                foreach (string semi_argument in semi_arguments)
                {
                    int parse;
                    if (semi_argument.ToLower().Equals("holo") || semi_argument.ToLower().Equals("h"))
                    {
                        c.Holo = true;
                        semi_to_remove.Add(semi_argument);
                    }

                    else if (Int32.TryParse(semi_argument, out parse))
                    {
                        if (qset == false)
                        {
                            qset = true;
                            c.Quantity = parse;
                        }
                        else
                        {
                            await ReplyAsync($"More than one numerical semi-argument detected in sub-argument {sub_arguments[i]}. How am I supposed to know how many you want to trade, doofus?");
                            return;
                        }
                        semi_to_remove.Add(semi_argument);
                    }
                }

                if (semi_to_remove.Count > 0)
                    semi_to_remove.ForEach(x => semi_arguments.Remove(x));

                sub_arguments[i] = string.Join(" ", semi_arguments).Trim();
                
                string correction;
                Utility.SpellCheckResultType spellcheck = Utility.CheckSpelling(sub_arguments[i], cl.Rows.Cast<DataRow>().Select(x => x["name"].ToString()).ToList(), out correction, editNum:2);
                if (spellcheck != Utility.SpellCheckResultType.OK)
                {
                    spelling_errors.Add(new Tuple<string, string, Utility.SpellCheckResultType>(sub_arguments[i], correction, spellcheck));
                }

                c.Name = sub_arguments[i];
                CardsToTrade.Add(c);
            }

            if (spelling_errors.Count > 0)
            {
                string message = "The following cards were not found:\n";
                foreach (var sp in spelling_errors)
                {
                    if (sp.Item3 == Utility.SpellCheckResultType.BAD_SPELLING_MATCH_FOUND)
                        message += $"\t{sp.Item1} -- Did you mean *{sp.Item2}*?\n";
                    else
                        message += $"\t{sp.Item1} -- No appropriate match was found.\n";
                }
                message += "The trade was not initiated.";
                await ReplyAsync(message);
                return;
            }

            DataTable dt = await GetCollection(Context.User.Id);

            var CardsForTrade = (
                from userCard in CardsToTrade
                join dbCard in dt.AsEnumerable() on
                    new
                    {
                        Name = userCard.Name.ToLower(),
                        Holo = userCard.Holo
                    }
                    equals
                    new
                    {
                        Name = dbCard["name"].ToString().ToLower(),
                        Holo = Convert.ToBoolean(dbCard["foil"])
                    }
                into c
                from cc in c.DefaultIfEmpty()
                select new
                {
                    id = cc == null ? 0 : Convert.ToInt32(cc["id"]),
                    dbCardCnt = cc == null ? -1 : Convert.ToInt32(cc["cnt"]),
                    userCardCnt = userCard.Quantity,
                    name = userCard.Name,
                    holo = userCard.Holo,
                    rarity = cc == null ? -1 : Convert.ToInt32(cc["rarity"])
                }
            ).ToList();

            var CardsNotPossesed = (
                from c in CardsForTrade
                where c.dbCardCnt == -1
                select c
            ).ToList();

            if (CardsNotPossesed.Count > 0)
            {
                string error = "You do not possess the following cards you have attempted to put up for trade:\n";
                foreach (var np in CardsNotPossesed)
                {
                    string holo = np.holo ? " (Holo)" : string.Empty;
                    error += $"\t{np.name}{holo}\n";
                }
                error += "The trade was not initiated.";
                await ReplyAsync(error);
                return;
            }

            string cardAmountErrorMessage = string.Empty;

            foreach (var cft in CardsForTrade)
            {
                string holo = cft.holo ? " (Holo)" : string.Empty;

                if (cft.userCardCnt > cft.dbCardCnt)
                    cardAmountErrorMessage += $"\tCard: {cft.name}{holo} {Utility.RarityToEmoji(cft.rarity, Context)}, Put up for trade: {cft.userCardCnt}, Currently own: {cft.dbCardCnt}\n";
                else if (cft.userCardCnt < 0)
                    cardAmountErrorMessage += $"\tCard: {cft.name}{holo} {Utility.RarityToEmoji(cft.rarity, Context)}, You cannot put up for trade a negative number of cards ({cft.userCardCnt})\n";
            }

            if (!cardAmountErrorMessage.Equals(string.Empty))
            {
                cardAmountErrorMessage = $"There are problems with the amount of cards you have attempted to put up for trade:\n{cardAmountErrorMessage}The trade was not initialized.";
                await ReplyAsync(cardAmountErrorMessage);
                return;
            }

            CardsToTrade = (
                from c in CardsForTrade
                select new CardToTrade(c.id, c.name, c.userCardCnt, c.holo, c.rarity)
            ).ToList();

            if (!Utility.Trade.IsInitialized())
                await Utility.Trade.Initialize(Context.User.Id, CardsToTrade, Context);
            else
                await Utility.Trade.Respond(Context.User.Id, CardsToTrade);
        }

        [Command("confirm")]
        public async Task Confirm()
        {
            await Utility.Trade.Confirm(Context.User.Id);
        }

        [Command("cancel")]
        public async Task Cancel()
        {
            await Utility.Trade.Cancel(Context.User.Id);
        }

        [Command("pepe-collect")]
        public async Task CollectCard()
        {
            if (debug)
            {
                await ReplyAsync("Cumbot is currently in DEBUG mode, pepe collection is temporarily disabled....");
                return;
            }

            //determine if user has collected within the past day
            //get record of collection with user, order by date, compare highest record to current datetime
            using (SQLiteConnection conn = new SQLiteConnection(connString))
            {
                await conn.OpenAsync();
                SQLiteCommand command = new SQLiteCommand("select * from latest_collection_dates where user_id = ?", conn);
                command.Parameters.AddWithValue("user_id", Context.User.Id.ToString());
                SQLiteDataAdapter da = new SQLiteDataAdapter();
                DataTable dt = new DataTable();
                da.SelectCommand = command;
                da.Fill(dt);

                if (dt.Rows.Count > 0)
                {
                    DateTimeOffset lastDateOffset = DateTimeOffset.FromUnixTimeMilliseconds(dt.Rows[0].Field<long>("collection_date"));
                    DateTime lastDate = lastDateOffset.UtcDateTime;
                    TimeSpan timeElapsed = DateTime.UtcNow - lastDate;

                    double timeLimit = 12.0;

                    if (timeElapsed.TotalHours < timeLimit)
                    {
                        var timeLeft = TimeSpan.FromHours(timeLimit) - timeElapsed;
                        await ReplyAsync($"Sorry, but you have already collected a card today. You can collect a card again in {timeLeft.Hours} hours, {timeLeft.Minutes} minutes, and {timeLeft.Seconds} seconds.");
                        return;
                    }
                }
            }

            //generate random card
            //retrieve cards from sql, select random one, weight by rarity, and have a random chance to apply foil to it

            //50% chance common, 25% chance uncommon, 15% chance rare, 10% mythic rare
            int roll = Utility.numgen.Next(1, 101);
            int rarity = 0;
            DataRow selectedCard;
            bool isFoil = false;

            if (roll < 11)
                rarity = 4;
            else if (roll < 26)
                rarity = 3;
            else if (roll < 50)
                rarity = 2;
            else
                rarity = 1;

            using (var conn = new SQLiteConnection(connString))
            {
                await conn.OpenAsync();
                SQLiteCommand command = new SQLiteCommand("select * from pepe_cards where rarity = ?", conn);
                command.Parameters.AddWithValue("rarity", rarity);
                SQLiteDataAdapter da = new SQLiteDataAdapter();
                DataTable dt = new DataTable();
                da.SelectCommand = command;
                da.Fill(dt);

                int roll2 = Utility.numgen.Next(0, dt.Rows.Count);
                selectedCard = dt.Rows[roll2];

                int roll3 = Utility.numgen.Next(1, 101);

                if (roll3 < 11)
                    isFoil = true;
            }

            //insert new collection row into collection table
            using (var conn = new SQLiteConnection(connString))
            {
                await conn.OpenAsync();
                var command = new SQLiteCommand("insert into collection (user_id, card_id, foil) VALUES (?,?,?)", conn);
                command.Parameters.AddWithValue("user_id", Context.User.Id.ToString());
                command.Parameters.AddWithValue("card_id", selectedCard["id"]);
                command.Parameters.AddWithValue("foil", isFoil);
                await command.ExecuteNonQueryAsync();

                command = new SQLiteCommand("update latest_collection_dates set collection_date = ? where user_id = ?", conn);
                command.Parameters.AddWithValue("collection_date", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                command.Parameters.AddWithValue("user_id", Context.User.Id);
                await command.ExecuteNonQueryAsync();
            }

            //inform user of their new card, and display it
            //use image generation library to frame card, and apply hologrphics if holo card

            //determine the appropriate article to use
            string testString = selectedCard.Field<string>("name").ToLower(),
                   article = String.Empty;
            if (testString.StartsWith("the"))
                article = String.Empty;
            else
                article = " a(n)";


            await ReplyAsync($"You received{article} {selectedCard["name"]}! Rendering...");

            Stream img = await GetCardImageStreamAsync(selectedCard.Field<string>("name"), selectedCard.Field<string>("image_extension"), rarity, isFoil);
            await Context.Channel.SendFileAsync(img, "test.png");

            if (isFoil)
                await ReplyAsync("Wow! It's holographic!");

        }
    }
}
