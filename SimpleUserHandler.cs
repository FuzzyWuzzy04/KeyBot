using SteamKit2;
using SteamTrade;
using System;
using System.Collections.Generic;
using System.Timers;

namespace SteamBot
{
    public class SimpleUserHandler : UserHandler
    {
        public static int InviteTimerInterval = 2000;
        static float SellPricePerKeys = 62.28f;
        static float BuyPricePerKeys = 59.94f;
        static int SellPricePerKey = (int)SellPricePerKeys;
        static int BuyPricePerKey = (int)BuyPricePerKeys;
        int WhileLoop, ExcessInScrap, UserMetalAdded, UserScrapAdded, UserRecAdded, UserRefAdded, UserKeysAdded, BotMetalAdded, BotScrapAdded, BotRecAdded, BotRefAdded, BotKeysAdded, InventoryMetal, InventoryScrap, InventoryRec, InventoryRef, InventoryKeys, PreviousKeys, OverpayNumKeys, InvalidItem = 0;
        bool TimerEnabled = false;
        bool TimerDisabled = true;
        bool HasCounted = false;
        bool AskOverpay, IsOverpaying, HasRun, HasErrorRun = false;
        double ExcessRefined = 0.0;

        ulong uid;
        SteamID currentSID;

        Timer inviteMsgTimer = new System.Timers.Timer(InviteTimerInterval);

        public SimpleUserHandler(Bot bot, SteamID sid)
            : base(bot, sid)
        {
        }

        public override bool OnFriendAdd()
        {
            Bot.log.Success(Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " (" + OtherSID.ToString() + ") added me!");
            inviteMsgTimer = new System.Timers.Timer();
            inviteMsgTimer.Interval = InviteTimerInterval;
            inviteMsgTimer.Elapsed += (sender, e) => OnInviteTimerElapsed(sender, e, EChatEntryType.ChatMsg);
            inviteMsgTimer.Enabled = true;
            return true;
        }

        public override void OnFriendRemove()
        {
            Bot.log.Success(Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " (" + OtherSID.ToString() + ") removed me!");
        }

        public override void OnMessage(string message, EChatEntryType type)
        {
            message = message.ToLower();

            // Admin commands
            if (IsAdmin)
            {
                if (message == "bot.restart")
                {
                    // Starts a new instance of the program itself
                    var filename = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    System.Diagnostics.Process.Start(filename);

                    // Closes the current process
                    Environment.Exit(0);
                }
                else if (message == "bot.stop")
                {
                    Environment.Exit(0);
                }
                else if (message == "trade.cancel")
                {
                    Trade.CancelTrade();
                    Bot.SteamFriends.SendChatMessage(currentSID, EChatEntryType.ChatMsg, "My owner has forcefully cancelled the trade.");
                }
                else if (message == "admin.help")
                {
                    Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "bot.restart ==== restarts the bot");
                    Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "bot.stop ==== stops the bot");
                    Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "admin.help ==== shows command help");
                    Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "trade.cancel ==== cancels the trade with the current user");
                }
                else
                {
                    Bot.SteamFriends.SendChatMessage(OtherSID, type, Bot.ChatResponse);
                }
            }

        }

        public override void OnLoginCompleted()
        {
        }

        public override bool OnTradeRequest()
        {
            Bot.log.Success(Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " (" + OtherSID.ToString() + ") has requested to trade with me!");
            return true;
        }

        public override void OnTradeError(string error)
        {
            Bot.SteamFriends.SendChatMessage(OtherSID,
                                              EChatEntryType.ChatMsg,
                                              "Error: " + error + "."
                                              );
            Bot.log.Warn(error);
            Bot.SteamFriends.SetPersonaState(EPersonaState.LookingToTrade);
        }

        public override void OnTradeTimeout()
        {
            Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg,
                                              "Sorry, but you were either AFK or took too long and the trade was canceled.");
            Bot.log.Warn("User was kicked because he was AFK.");
            Bot.SteamFriends.SetPersonaState(EPersonaState.LookingToTrade);
        }

        public override void OnTradeInit()
        {
            Bot.SteamFriends.SetPersonaState(EPersonaState.Busy);
            ReInit();
            TradeCountInventory(true);
            Trade.SendMessage("Welcome to the Key Banking Bot. To use this bot, just add your metal or keys, and the bot will automatically add keys or metal when you have put up enough.");
            if (InventoryKeys == 0)
            {
                Trade.SendMessage("I don't have any keys to sell right now! I am currently buying keys for " + String.Format("{0:0.00}", (BuyPricePerKey / 9.0)) + " ref.");
            }
            else if (InventoryMetal < BuyPricePerKey)
            {
                Trade.SendMessage("I don't have enough metal to buy keys! I am selling keys for " + String.Format("{0:0.00}", (SellPricePerKey / 9.0)) + " ref.");
            }
            else
            {
                Trade.SendMessage("I am currently buying keys for " + String.Format("{0:0.00}", (BuyPricePerKey / 9.0)) + " ref, and selling keys for " + String.Format("{0:0.00}", (SellPricePerKey / 9.0)) + " ref.");
            }
            Bot.SteamFriends.SetPersonaState(EPersonaState.Busy);
        }

        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem)
        {
            var item = Trade.CurrentSchema.GetItem(schemaItem.Defindex);
            if (!HasCounted)
            {
                Trade.SendMessage("ERROR: I haven't finished counting my inventory yet! Please remove any items you added, and then re-add them or there could be errors.");
            }
            else if (InvalidItem >= 4)
            {
                Trade.CancelTrade();
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Please stop messing around. I am used for buying and selling keys only. I can only accept metal or keys as payment.");
                Bot.log.Warn("Booted user for messing around.");
                Bot.SteamFriends.SetPersonaState(EPersonaState.Online);
            }
            else if (item.Defindex == 5000)
            {
                // Put up scrap metal
                UserMetalAdded++;
                UserScrapAdded++;
                Bot.log.Success("User added: " + item.ItemName);
            }
            else if (item.Defindex == 5001)
            {
                // Put up reclaimed metal
                UserMetalAdded += 3;
                UserRecAdded++;
                Bot.log.Success("User added: " + item.ItemName);
            }
            else if (item.Defindex == 5002)
            {
                // Put up refined metal
                UserMetalAdded += 9;
                UserRefAdded++;
                Bot.log.Success("User added: " + item.ItemName);
            }
            else if (schemaItem.ItemName == "Mann Co. Supply Crate Key" || schemaItem.ItemName == "#TF_Tool_DecoderRing")
            {
                // Put up keys
                UserKeysAdded++;
                Bot.log.Success("User added: " + item.ItemName);

                // BOT ADDS METAL
                int KeysToScrap = UserKeysAdded * BuyPricePerKey;
                if (InventoryMetal < KeysToScrap)
                {
                    Trade.SendMessage("I only have " + InventoryMetal + " scrap. You need to remove some keys.");
                    Bot.log.Warn("I don't have enough metal for the user.");
                }
                else
                {
                    Trade.SendMessage("You have given me " + UserKeysAdded + " key(s). I will give you " + KeysToScrap + " scrap.");
                    Bot.log.Success("User gave me " + UserKeysAdded + " key(s). I will now give him " + KeysToScrap + " scrap.");
                    // Put up required metal
                    bool DoneAddingMetal = false;
                    while (!DoneAddingMetal)
                    {
                        if (InventoryRef > 0 && BotMetalAdded + 9 <= KeysToScrap)
                        {
                            Trade.AddItemByDefindex(5002);
                            Bot.log.Warn("I added Refined Metal.");
                            BotMetalAdded += 9;
                            BotRefAdded++;
                            InventoryRef--;
                        }
                        else if (InventoryRec > 0 && BotMetalAdded + 3 <= KeysToScrap)
                        {
                            Trade.AddItemByDefindex(5001);
                            Bot.log.Warn("I added Reclaimed Metal.");
                            BotMetalAdded += 3;
                            BotRecAdded++;
                            InventoryRec--;
                        }
                        else if (InventoryScrap > 0 && BotMetalAdded + 1 <= KeysToScrap)
                        {
                            Trade.AddItemByDefindex(5000);
                            Bot.log.Warn("I added Scrap Metal.");
                            BotMetalAdded++;
                            BotScrapAdded++;
                            InventoryScrap--;
                        }
                        else if (InventoryScrap == 0 && BotMetalAdded + 2 == KeysToScrap)
                        {
                            Trade.SendMessage("Sorry, but I don't have enough scrap metal to give you! Please remove some keys or add two keys.");
                            Bot.log.Warn("Couldn't add enough metal for the user!");
                            DoneAddingMetal = true;
                        }
                        else if (InventoryScrap == 0 && BotMetalAdded + 1 == KeysToScrap)
                        {
                            Trade.SendMessage("Sorry, but I don't have enough scrap metal to give you! Please remove some keys or add a key.");
                            Bot.log.Warn("Couldn't add enough metal for the user!");
                            DoneAddingMetal = true;
                        }
                        else if (BotMetalAdded == KeysToScrap)
                        {
                            Trade.SendMessage("Added enough metal. " + BotRefAdded + " ref, " + BotRecAdded + " rec, " + BotScrapAdded + " scrap.");
                            Bot.log.Success("Gave user enough metal!");
                            DoneAddingMetal = true;
                        }
                    }
                }
            }
            else
            {
                // Put up other items
                Trade.SendMessage("Sorry, I don't accept " + item.ItemName + "! I only accept metal/keys. Please remove it from the trade to continue.");
                Bot.log.Warn("User added:  " + item.ItemName);
                InvalidItem++;
            }
            // USER IS BUYING KEYS

            if (UserMetalAdded % SellPricePerKey >= 0 && UserMetalAdded > 0)
            {
                // Count refined and convert to keys -- X scrap per key
                int NumKeys = UserMetalAdded / SellPricePerKey;
                if (NumKeys > 0 && NumKeys != PreviousKeys)
                {
                    Trade.SendMessage("You put up enough metal for " + NumKeys + " key(s). Adding your keys now...");
                    Bot.log.Success("User put up enough metal for " + NumKeys + " key(s).");
                    if (NumKeys > InventoryKeys)
                    {
                        double excess = ((NumKeys - BotKeysAdded) * SellPricePerKey) / 9.0;
                        string refined = string.Format("{0:N2}", excess);
                        Trade.SendMessage("I only have " + InventoryKeys + " in my backpack. :(");
                        Bot.log.Warn("User wanted to buy " + NumKeys + " key(s), but I only have " + InventoryKeys + " key(s).");
                        Trade.SendMessage("Please remove " + refined + " ref.");
                        NumKeys = InventoryKeys;
                    }
                    // Add the keys to the trade window
                    for (int count = BotKeysAdded; count < NumKeys; count++)
                    {
                        if (AddKey())
                        {
                            Bot.log.Warn("I am adding Mann Co. Supply Crate Key.");
                            BotKeysAdded++;
                            InventoryKeys--;
                        }
                    }
                    Trade.SendMessage("I have added " + BotKeysAdded + " key(s) for you.");
                    Bot.log.Success("I have added " + BotKeysAdded + " key(s) for the user.");
                    PreviousKeys = NumKeys;
                }
            }
        }

        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem)
        {
            var item = Trade.CurrentSchema.GetItem(schemaItem.Defindex);
            if (item.Defindex == 5000)
            {
                // Removed scrap metal
                UserMetalAdded--;
                UserScrapAdded--;
                Bot.log.Success("User removed: " + item.ItemName);
            }
            else if (item.Defindex == 5001)
            {
                // Removed reclaimed metal
                UserMetalAdded -= 3;
                UserRecAdded--;
                Bot.log.Success("User removed: " + item.ItemName);
            }
            else if (item.Defindex == 5002)
            {
                // Removed refined metal
                UserMetalAdded -= 9;
                UserRefAdded--;
                Bot.log.Success("User removed: " + item.ItemName);
            }
            else if (schemaItem.ItemName == "Mann Co. Supply Crate Key" || schemaItem.ItemName == "#TF_Tool_DecoderRing")
            {
                // Removed keys
                UserKeysAdded--;
                Bot.log.Success("User removed: " + item.ItemName);
            }
            else
            {
                // Removed other items
                Bot.log.Warn("User removed: " + item.ItemName);
            }
            // User removes key from trade
            if (UserKeysAdded < (float)BotMetalAdded / BuyPricePerKey)
            {
                int KeysToScrap = UserKeysAdded * BuyPricePerKey;
                bool DoneAddingMetal = false;
                while (!DoneAddingMetal)
                {
                    WhileLoop++;
                    if (BotRefAdded > 0 && BotMetalAdded - 9 >= KeysToScrap)
                    {
                        Trade.RemoveItemByDefindex(5002);
                        Bot.log.Warn("I removed Refined Metal.");
                        BotMetalAdded -= 9;
                        BotRefAdded--;
                        InventoryRef++;
                    }
                    else if (BotRecAdded > 0 && BotMetalAdded - 3 >= KeysToScrap)
                    {
                        Trade.RemoveItemByDefindex(5001);
                        Bot.log.Warn("I removed Reclaimed Metal.");
                        BotMetalAdded -= 3;
                        BotRecAdded--;
                        InventoryRec++;
                    }
                    else if (BotScrapAdded > 0 && BotMetalAdded - 1 >= KeysToScrap)
                    {
                        Trade.RemoveItemByDefindex(5000);
                        Bot.log.Warn("I removed Scrap Metal.");
                        BotMetalAdded--;
                        BotScrapAdded--;
                        InventoryScrap++;
                    }
                    else if (BotMetalAdded == KeysToScrap)
                    {
                        DoneAddingMetal = true;
                    }
                    else if (WhileLoop > 50)
                    {
                        Trade.SendMessage("Error: I could not remove the proper amounts of metal from the trade. I might be out of scrap metal - try adding more keys if possible, or remove a few keys.");
                        WhileLoop = 0;
                        DoneAddingMetal = true;
                        break;
                    }
                }
            }
            // User removes metal from trade
            while ((float)UserMetalAdded / SellPricePerKey < BotKeysAdded)
            {
                if (RemoveKey())
                {
                    Bot.log.Warn("I removed Mann Co. Supply Crate Key.");
                    BotKeysAdded--;
                    InventoryKeys++;
                    PreviousKeys = BotKeysAdded;
                    IsOverpaying = false;
                }
            }
        }

        public override void OnTradeMessage(string message)
        {
            Bot.log.Info("[TRADE MESSAGE] " + message);
            message = message.ToLower();

            if (message == "continue")
            {
                if (AskOverpay)
                {
                    IsOverpaying = true;
                    Trade.SendMessage("You have chosen to continue overpaying. Click \"Ready to Trade\" again to complete the trade.");
                    Bot.log.Warn("User has chosen to continue overpaying!");
                }
                else
                {
                    Trade.SendMessage("You cannot use this command right now!");
                    Bot.log.Warn("User typed \"continue\" for no reason.");
                }

            }
        }

        public override void OnTradeReady(bool ready)
        {
            if (!ready)
            {
                Trade.SetReady(false);
            }
            else
            {
                Bot.log.Success("User is ready to trade!");
                if (Validate())
                {
                    Trade.SetReady(true);
                }
                else
                {
                    if (AskOverpay && OverpayNumKeys != 0)
                    {
                        double AdditionalRefined = (SellPricePerKey / 9.0) - ExcessRefined;
                        string addRef = string.Format("{0:N2}", AdditionalRefined);
                        string refined = string.Format("{0:N2}", ExcessRefined);
                        Trade.SendMessage("WARNING: You will be overpaying. If you'd like to continue, type \"continue\", otherwise remove " + refined + " ref, or add " + addRef + " ref. You cannot complete the trade unless you do so.");
                        Bot.log.Warn("User has added an excess of " + refined + " ref. He can add " + addRef + " ref for another key. Asking user if they want to continue.");
                    }
                    else
                    {
                        ResetTrade(false);
                    }
                }
            }
        }

        public override void OnTradeAccept()
        {
            if (Validate() || IsAdmin)
            {
                bool success = Trade.AcceptTrade();
                if (success)
                {
                    Log.Success("Trade was successful!");
                    Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Thanks for a successful trade!");
                    Bot.SteamFriends.SetPersonaState(EPersonaState.Online);
                }
                else
                {
                    Log.Warn("Trade might have failed.");
                    Bot.SteamFriends.SetPersonaState(EPersonaState.Online);
                }
            }
            OnTradeClose();
        }

        public override void OnTradeClose()
        {
            Bot.SteamFriends.SetPersonaState(EPersonaState.Online);
            base.OnTradeClose();
        }

        public bool Validate()
        {
            int ScrapCount = 0;
            int KeyCount = 0;

            List<string> errors = new List<string>();

            foreach (ulong id in Trade.OtherOfferedItems)
            {
                var item = Trade.OtherInventory.GetItem(id);
                var schemaItem = Trade.CurrentSchema.GetItem(item.Defindex);
                if (item.Defindex == 5000)
                {
                    ScrapCount++;
                }
                else if (item.Defindex == 5001)
                {
                    ScrapCount += 3;
                }
                else if (item.Defindex == 5002)
                {
                    ScrapCount += 9;
                }
                else if (schemaItem.ItemName == "Mann Co. Supply Crate Key" || schemaItem.ItemName == "#TF_Tool_DecoderRing")
                {
                    KeyCount++;
                }
                else
                {
                    errors.Add("I can't accept " + schemaItem.ItemName + "!");
                }
            }
            if (UserKeysAdded > 0)
            {
                Bot.log.Warn("User has " + KeyCount + " key(s) put up. Verifying if " + (float)BotMetalAdded / BuyPricePerKey + " == " + KeyCount + ".");
                if (KeyCount != (float)BotMetalAdded / BuyPricePerKey)
                {
                    errors.Add("Something went wrong. Either you do not have the correct amount of keys or I don't have the correct amount of metal.");
                }
            }
            else if (UserMetalAdded % SellPricePerKey != 0 && !IsOverpaying)
            {
                // Count refined and convert to keys -- X scrap per key
                OverpayNumKeys = UserMetalAdded / SellPricePerKey;
                ExcessInScrap = UserMetalAdded - (OverpayNumKeys * SellPricePerKey);
                ExcessRefined = (ExcessInScrap / 9.0);
                string refined = string.Format("{0:N2}", ExcessRefined);
                Trade.SendMessage("You put up enough metal for " + OverpayNumKeys + " key(s), with " + refined + " ref extra.");
                Bot.log.Success("User put up enough metal for " + OverpayNumKeys + " key(s), with " + refined + " ref extra.");
                if (OverpayNumKeys == 0)
                {
                    double AdditionalRefined = (SellPricePerKey / 9.0) - ExcessRefined;
                    string addRef = string.Format("{0:N2}", AdditionalRefined);
                    errors.Add("ERROR: You need to add " + addRef + " ref for a key.");
                    Bot.log.Warn("User doesn't have enough metal added, and needs add " + addRef + " ref for a key.");
                }
                else if (OverpayNumKeys >= 1)
                {
                    errors.Add("You have put up more metal than what I'm asking.");
                    AskOverpay = true;
                }
            }
            else if (UserMetalAdded > 0 && !IsOverpaying)
            {
                if (ScrapCount < BotKeysAdded * SellPricePerKey || (ScrapCount > BotKeysAdded * SellPricePerKey))
                {
                    errors.Add("You must put up exactly " + String.Format("{0:0.00}", (SellPricePerKey / 9.0)) + " ref per key.");
                }
            }

            // send the errors
            if (errors.Count != 0)
                Trade.SendMessage("There were errors in your trade: ");

            foreach (string error in errors)
            {
                Trade.SendMessage(error);
            }

            return errors.Count == 0;

        }

        public void TradeCountInventory(bool message)
        {
            // Let's count our inventory
            Inventory.Item[] inventory = Trade.MyInventory.Items;
            InventoryMetal = 0;
            InventoryKeys = 0;
            InventoryRef = 0;
            InventoryRec = 0;
            InventoryScrap = 0;
            foreach (Inventory.Item item in inventory)
            {
                var schemaItem = Trade.CurrentSchema.GetItem(item.Defindex);
                if (item.Defindex == 5000)
                {
                    InventoryMetal++;
                    InventoryScrap++;
                }
                else if (item.Defindex == 5001)
                {
                    InventoryMetal += 3;
                    InventoryRec++;
                }
                else if (item.Defindex == 5002)
                {
                    InventoryMetal += 9;
                    InventoryRef++;
                }
                else if (schemaItem.ItemName == "Mann Co. Supply Crate Key" || schemaItem.ItemName == "#TF_Tool_DecoderRing")
                {
                    InventoryKeys++;
                }
            }
            if (message)
            {
                double MetalToRef = (InventoryMetal / 9.0) - 0.01;
                string refined = string.Format("{0:N2}", MetalToRef);
                Trade.SendMessage("Current stock: I have " + refined + " ref (" + InventoryRef + " ref, " + InventoryRec + " rec, and " + InventoryScrap + " scrap) and " + InventoryKeys + " key(s) in my backpack.");
                Bot.log.Success("Current stock: I have " + refined + " ref (" + InventoryRef + " ref, " + InventoryRec + " rec, and " + InventoryScrap + " scrap) and " + InventoryKeys + " key(s) in my backpack.");
            }
            HasCounted = true;
        }

        public void ReInit()
        {
            UserMetalAdded = 0;
            UserRefAdded = 0;
            UserRecAdded = 0;
            UserScrapAdded = 0;
            UserKeysAdded = 0;
            BotKeysAdded = 0;
            BotMetalAdded = 0;
            BotRefAdded = 0;
            BotRecAdded = 0;
            BotScrapAdded = 0;
            OverpayNumKeys = 0;
            PreviousKeys = 0;
            ExcessInScrap = 0;
            ExcessRefined = 0.0;
            WhileLoop = 0;
            InvalidItem = 0;
            HasErrorRun = false;
            AskOverpay = false;
            IsOverpaying = false;
            HasCounted = false;
            currentSID = OtherSID;
        }

        public bool AddKey()
        {
            bool added = false;
            while (!added)
            {
                foreach (var item in Trade.MyInventory.Items)
                {
                    var schemaItem = Trade.CurrentSchema.GetItem(item.Defindex);
                    if (schemaItem.ItemName == "Mann Co. Supply Crate Key" || schemaItem.ItemName == "#TF_Tool_DecoderRing")
                    {
                        added = Trade.AddItem(item.Id);
                        if (added) break;
                    }
                }
                break;
            }
            return added;
        }

        public bool RemoveKey()
        {
            bool removed = false;
            while (!removed)
            {
                foreach (var item in Trade.MyInventory.Items)
                {
                    var schemaItem = Trade.CurrentSchema.GetItem(item.Defindex);
                    if (schemaItem.ItemName == "Mann Co. Supply Crate Key" || schemaItem.ItemName == "#TF_Tool_DecoderRing")
                    {
                        removed = Trade.RemoveItem(item.Id);
                        if (removed) break;
                    }
                }
                break;
            }
            return removed;
        }

        private void OnInviteTimerElapsed(object source, ElapsedEventArgs e, EChatEntryType type)
        {
            Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Hi. You have added the Key Banking Bot! Just trade me, and add your keys or metal to begin!");
            Bot.log.Success("Sent welcome message.");
            inviteMsgTimer.Enabled = false;
            inviteMsgTimer.Stop();
        }

        public void ResetTrade(bool message)
        {
            foreach (var item in Trade.MyInventory.Items)
            {
                Trade.RemoveItem(item.Id);
            }
            BotKeysAdded = 0;
            BotMetalAdded = 0;
            BotRefAdded = 0;
            BotRecAdded = 0;
            BotScrapAdded = 0;
            TradeCountInventory(message);
            Trade.SendMessage("Something went wrong! Scroll up to read the errors.");
            Bot.log.Warn("Something went wrong! I am resetting the trade.");
            Trade.SendMessage("I have reset the trade.");
            Bot.log.Success("Reset trade.");
        }


    }
}



