using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Threading;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;

namespace JailPrison
{
    [ApiVersion(1, 16)]
    public class Jail : TerrariaPlugin
    {
        public static JPConfigFile JPConfig { get; set; }
        public Player[] Players { get; set; }
        internal static string JPConfigPath { get { return Path.Combine(TShock.SavePath, "jpconfig.json"); } }

        public override string Name
        {
            get { return "Jail, Prison, AFK, Chest & Kill Rooms, Cmd Limits"; }
        }
        public override string Author
        {
            get { return "Created by DarkunderdoG"; }
        }
        public override string Description
        {
            get { return "Jail & Prison Plugin"; }
        }
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, (args) => { OnInitialize(); });
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, (args) => { OnInitialize(); });
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
            }

            base.Dispose(disposing);
        }

        public Jail(Main game)
            : base(game)
        {
            Order = 3;
            JPConfig = new JPConfigFile();
            Players = new Player[256];
        }

        public void OnInitialize()
        {
            SetupConfig();
            if (JPConfig.jailmode)
                Commands.ChatCommands.Add(new Command("jailcomm", warpjail, JPConfig.jailcomm));
            if (JPConfig.prisonmode)
            {
                Commands.ChatCommands.Add(new Command("prison", imprison, "imprison"));
                Commands.ChatCommands.Add(new Command("prison", setfree, "setfree"));
            }
            Commands.ChatCommands.Add(new Command("cfg", jailreload, "jailreload"));

            if (JPConfig.chestzones)
                Commands.ChatCommands.Add(new Command("chestroom", chests, "allowchests"));
            if (JPConfig.afkmode)
            {
                Commands.ChatCommands.Add(new Command("idlecomm", idlecomm, "afktime"));
                Commands.ChatCommands.Add(new Command("idlecomm", sendafk, "afk"));
                Commands.ChatCommands.Add(new Command("idlecomm", sendback, "back"));
                Commands.ChatCommands.Add(new Command("cfg", setidletime, "setidletime"));
            }
            if (JPConfig.cmdlimit)
            {
                Commands.ChatCommands.Add(new Command("cfg", setcmdtime, "setcmdtime"));
                Commands.ChatCommands.Add(new Command("killzone", killzone, "killzone"));
            }
        }

        public void OnGreetPlayer(GreetPlayerEventArgs e)
        {
            Players[e.Who] = new Player(e.Who);
        }

        public class Player
        {
            public int Index { get; set; }
            public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
            public bool prisonMode { get; set; }
            public bool rulesMode { get; set; }
            public bool prisonModeSpam { get; set; }
            public bool rulesModeSpam { get; set; }
            public bool chestMode { get; set; }
            public bool killMode { get; set; }
            public bool killModeSpam { get; set; }
            public bool chestModeSpam { get; set; }
            public int idle { get; set; }
            public int lasttileX { get; set; }
            public int lasttileY { get; set; }
            public int backtileX { get; set; }
            public int backtileY { get; set; }
            public int chestAmount { get; set; }
            public int itemAmount { get; set; }
            public int healAmount { get; set; }
            public bool bannedarmor { get; set; }
            public string bannedarmorname { get; set; }
            public int devItemAmount { get; set; }
            public Player(int index)
            {
                Index = index;
                prisonMode = false;
                rulesMode = true;
                prisonModeSpam = true;
                rulesModeSpam = true;
                lasttileX = TShock.Players[Index].TileX;
                lasttileY = TShock.Players[Index].TileY;
                backtileX = 0;
                backtileY = 0;
                chestMode = true;
                killMode = false;
                killModeSpam = true;
                chestModeSpam = true;
                idle = 0;
                chestAmount = 0;
                itemAmount = 0;
                healAmount = 0;
                bannedarmor = false;
                bannedarmorname = "";
                devItemAmount = 0;
            }
        }

        public static void SetupConfig()
        {
            try
            {
                if (File.Exists(JPConfigPath))
                {
                    JPConfig = JPConfigFile.Read(JPConfigPath);
                    // Add all the missing config properties in the json file
                }
                JPConfig.Write(JPConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in jail config file");
                Console.ForegroundColor = ConsoleColor.Gray;
                Log.Error("Jail Config Exception");
                Log.Error(ex.ToString());
            }
        }

        private DateTime LastCheck = DateTime.UtcNow;

        public void OnUpdate(EventArgs e)
        {
            if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 1)
            {
                LastCheck = DateTime.UtcNow;
                lock (Players)
                    foreach (Player player in Players)
                    {
                        if (player != null && player.TSPlayer != null)
                        {
                            if (Jail.JPConfig.armorban)
                            {
                                Item[] armor = player.TSPlayer.TPlayer.armor;
                                for (int j = 0; j < armor.Length; j++)
                                {
                                    Item item = armor[j];
                                    if (!player.TSPlayer.Group.HasPermission(Permissions.usebanneditem) && TShock.Itembans.ItemIsBanned(item.name, player.TSPlayer))
                                    {
                                        player.bannedarmor = true;
                                        player.bannedarmorname = item.name;
                                    }
                                }
                                if (player.bannedarmor)
                                {
                                    player.devItemAmount++;
                                    if (player.devItemAmount == 1)
                                    {
                                        player.TSPlayer.SendMessage(string.Format("You have 10 seconds left to remove {0} or get kicked", player.bannedarmorname), Color.Red);
                                    }
                                    else
                                    {
                                        if (player.devItemAmount == 6)
                                        {
                                            player.TSPlayer.SendMessage(string.Format("You have 5 seconds left to remove {0} or get kicked", player.bannedarmorname), Color.Red);
                                        }
                                        else
                                        {
                                            if (player.devItemAmount == 11)
                                            {
                                                TShock.Utils.Kick(player.TSPlayer, "Please Remove " + player.bannedarmorname, false, false, null, false);
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    player.devItemAmount = 0;
                                }
                                player.bannedarmor = false;
                                player.bannedarmorname = "";
                            }
                            string currentregionlist = "";
                            var currentregion = TShock.Regions.InAreaRegionName(player.TSPlayer.TileX, player.TSPlayer.TileY);
                            if (currentregion.Count > 0)
                                currentregionlist = string.Join(",", currentregion.ToArray());
                            if (player.prisonMode && JPConfig.prisonmode)
                            {
                                if (!currentregionlist.Contains("prison"))
                                {
                                    string warpName = "prison";
                                    var warp = TShock.Warps.Find(warpName);
                                    if (warp != null)
                                    {
                                        if (player.TSPlayer.Teleport((int)warp.Position.X * 16, (int)warp.Position.Y * 16))
                                        {
                                            if (player.prisonModeSpam)
                                            {
                                                player.TSPlayer.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...", Color.Red);
                                                player.prisonModeSpam = false;
                                            }
                                        }
                                    }
                                }
                            }
                            if (player.rulesMode && !player.TSPlayer.Group.HasPermission("jail") && JPConfig.jailmode && !player.prisonMode)
                            {
                                if (!currentregionlist.Contains("jail"))
                                {
                                    string warpName = "jail";
                                    var warp = TShock.Warps.Find(warpName);
                                    if (warp != null)
                                    {
                                        if (player.TSPlayer.Teleport((int)warp.Position.X * 16, (int)(warp.Position.Y) * 16))
                                        {
                                            if (player.rulesModeSpam)
                                            {
                                                player.TSPlayer.SendMessage("You Cannot Get Out Of Jail Without Reading The Rules", Color.Red);
                                                player.rulesModeSpam = false;
                                            }
                                        }
                                    }
                                }
                            }

                            if (!player.chestMode && JPConfig.chestzones)
                            {
                                if (player.chestAmount > 0)
                                    player.chestAmount--;
                                else
                                {
                                    player.chestMode = true;
                                    player.chestModeSpam = true;
                                    player.TSPlayer.SendMessage("Your Allowed Time In The Chest Room Is Now Up!", Color.Green);
                                    player.chestAmount = 0;
                                }
                            }
                            if (player.itemAmount > 0)
                                player.itemAmount--;

                            if (player.healAmount > 0)
                                player.healAmount--;

                            if (player.chestMode && !player.TSPlayer.Group.HasPermission("chests") && JPConfig.chestzones)
                            {
                                string region = currentregionlist;
                                string prefix = player.TSPlayer.Group.Prefix;
                                if (prefix == null || prefix == "")
                                    prefix = "nothing3252";
                                if (region == "" || region == null)
                                    region = "nothing3132";
                                else region = region.ToLower();
                                if (region.Contains("chests"))
                                {
                                    if (!region.Contains(player.TSPlayer.Name.ToLower()) && !region.Contains(player.TSPlayer.Group.Name.ToLower()) && !region.Contains(prefix.ToLower()))
                                    {
                                        if (player.TSPlayer.Teleport(Main.spawnTileX * 16, Main.spawnTileY + 3 * 16))
                                        {
                                            if (player.chestModeSpam)
                                            {
                                                player.TSPlayer.SendMessage("You Do Not Have Permission To Enter This Chest Room", Color.Red);
                                                player.chestModeSpam = false;
                                            }
                                        }
                                    }
                                }
                            }

                            if (!player.killMode && !player.TSPlayer.Group.HasPermission("killzone") && JPConfig.killzones)
                            {
                                string region = currentregionlist;
                                string prefix = player.TSPlayer.Group.Prefix;
                                if (prefix == null || prefix == "")
                                    prefix = "nothing3252";
                                if (region == "" || region == null)
                                    region = "nothing3132";
                                else region = region.ToLower();
                                if (!region.Contains("kill"))
                                    player.killModeSpam = true;
                                if (region.Contains("kill"))
                                {
                                    if (!region.Contains(player.TSPlayer.Name.ToLower()) && !region.Contains(player.TSPlayer.Group.Name.ToLower()) && !region.Contains(prefix.ToLower()))
                                    {
                                        if (player.killModeSpam)
                                        {
                                            player.TSPlayer.SendMessage("You Entered The " + region + " ZonE", Color.Green);
                                            TShock.Utils.Broadcast(player.TSPlayer.Name + " Was Killed Because They Entered The " + region + " ZonE!", Color.Red);
                                            player.killModeSpam = false;
                                        }
                                        player.TSPlayer.DamagePlayer(10000);
                                    }
                                }
                            }
                            if (player.lasttileX != 0 && player.lasttileY != 0 && !currentregionlist.Contains("afk") && JPConfig.afkmode)
                            {
                                if (player.TSPlayer.TileX == player.lasttileX && player.TSPlayer.TileY == player.lasttileY)
                                {
                                    player.idle = player.idle + 1;
                                }
                                else
                                    player.idle = 1;
                                player.lasttileX = player.TSPlayer.TileX;
                                player.lasttileY = player.TSPlayer.TileY;
                                if (player.idle > JPConfig.afktime && player.rulesMode && !player.TSPlayer.Group.HasPermission("jail") && JPConfig.jailmode)
                                    TShock.Utils.Kick(player.TSPlayer, "You Were Idle For Too Long!!!!!");
                                else if (player.idle > JPConfig.afktime && !player.prisonMode)
                                {
                                    var warp = TShock.Warps.Find("afk");
                                    player.backtileX = player.TSPlayer.TileX;
                                    player.backtileY = player.TSPlayer.TileY;
                                    if (player.TSPlayer.Teleport((int)warp.Position.X * 16, (int)(warp.Position.Y + 3) * 16))
                                    {
                                        player.TSPlayer.SendMessage("You Have Been Warped To The AFK Zone. Use the /back command to go back!", Color.Green);
                                        TShock.Utils.Broadcast(player.TSPlayer.Name + " Is Away From His/Her Keyboard And Has Been Warped To The AFK Zone!", Color.Green);
                                    }
                                }
                            }
                        }
                        if (JPConfig.diemobmode)
                        {
                            for (int i = 0; i < Main.maxNPCs; i++)
                            {
                                var npcregion = TShock.Regions.InAreaRegionName(((int)Main.npc[i].position.X) / 16, ((int)Main.npc[i].position.Y) / 16);
                                string npcregionlist = string.Join(",", npcregion.ToArray());
                                if (Main.npc[i].active && !Main.npc[i].friendly && npcregion.Count > 0)
                                {
                                    if (npcregionlist.Contains("diemob"))
                                    {
                                        Main.npc[i].netDefaults(0);
                                        TSPlayer.Server.StrikeNPC(i, 99999, 90f, 1);
                                    }
                                }
                            }
                        }
                    }
            }
        }

        private void OnLeave(LeaveEventArgs e)
        {
            try
            {
                Players[e.Who] = null;
            }
            catch { }
        }

        public void OnChat(ServerChatEventArgs e)
        {
            var msg = e.Buffer;
            var ply = e.Who;
            var text = e.Text;
            var jpPly = Players[e.Who];

            string cmd = text.Split(' ')[0];
            var tsplr = TShock.Players[ply];
            string currentregionlist = "";
            var currentregion = TShock.Regions.InAreaRegionName(tsplr.TileX, tsplr.TileY);
            if (currentregion.Count > 0)
                currentregionlist = string.Join(",", currentregion.ToArray());
            if (cmd == "/warp")
            {
                if (text.Split(' ').Length > 1)
                {
                    if (TShock.Warps.Find(text.Split(' ')[1]) != null)
                    {
                        if (jpPly.rulesMode && !tsplr.Group.HasPermission("jail") && !currentregionlist.Contains("jail") && JPConfig.jailmode)
                        {
                            tsplr.SendMessage("You Can't Teleport Without Reading / Following The Rules!", Color.Red);
                            e.Handled = true;
                            return;
                        }
                        if (currentregionlist.Contains("jail") && !tsplr.Group.HasPermission("jail") && jpPly.rulesMode && JPConfig.jailmode)
                        {
                            tsplr.SendMessage("You Can't Exit Jail Without Reading / Following The Rules!", Color.Red);
                            e.Handled = true;
                            return;
                        }
                        if (jpPly.prisonMode && JPConfig.prisonmode)
                        {
                            tsplr.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...", Color.Red);
                            e.Handled = true;
                            return;
                        }
                    }
                    else return;
                }
                return;
            }

            else if (cmd == "/tp")
            {
                if (text.Split(' ').Length > 1)
                {
                    if (jpPly.rulesMode && !tsplr.Group.HasPermission("jail") && !currentregionlist.Contains("jail") && JPConfig.jailmode)
                    {
                        tsplr.SendMessage("You Can't Teleport Without Reading / Following The Rules!", Color.Red);
                        e.Handled = true;
                        return;
                    }
                    if (currentregionlist.Contains("jail") && !tsplr.Group.HasPermission("jail") && jpPly.rulesMode && JPConfig.jailmode)
                    {
                        tsplr.SendMessage("You Can't Exit Jail Without Reading / Following The Rules!", Color.Red);
                        e.Handled = true;
                        return;
                    }
                    if (jpPly.prisonMode && JPConfig.prisonmode)
                    {
                        tsplr.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...", Color.Red);
                        e.Handled = true;
                        return;
                    }
                }
                else return;
            }
            else if (cmd == "/home")
            {
                if (jpPly.rulesMode && !tsplr.Group.HasPermission("jail") && !currentregionlist.Contains("jail") && JPConfig.jailmode)
                {
                    tsplr.SendMessage("You Can't Teleport Without Reading / Following The Rules!", Color.Red);
                    e.Handled = true;
                    return;
                }
                if (currentregionlist.Contains("jail") && !tsplr.Group.HasPermission("jail") && jpPly.rulesMode && JPConfig.jailmode)
                {
                    tsplr.SendMessage("You Can't Exit Jail Without Reading / Following The Rules!", Color.Red);
                    e.Handled = true;
                    return;
                }
                if (jpPly.prisonMode && JPConfig.prisonmode)
                {
                    tsplr.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...", Color.Red);
                    e.Handled = true;
                    return;
                }
                tsplr.Spawn();
                tsplr.SendMessage("Teleported to your spawnpoint.", Color.Green);
                e.Handled = true;
                return;
            }

            else if (cmd == "/spawn")
            {
                if (jpPly.rulesMode && !tsplr.Group.HasPermission("jail") && !currentregionlist.Contains("jail") && JPConfig.jailmode)
                {
                    tsplr.SendMessage("You Can't Teleport Without Reading / Following The Rules!", Color.Red);
                    e.Handled = true;
                    return;
                }
                if (currentregionlist.Contains("jail") && !tsplr.Group.HasPermission("jail") && jpPly.rulesMode && JPConfig.jailmode)
                {
                    tsplr.SendMessage("You Can't Exit Jail Without Reading / Following The Rules!", Color.Red);
                    e.Handled = true;
                    return;
                }
                if (jpPly.prisonMode && JPConfig.prisonmode)
                {
                    tsplr.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...", Color.Red);
                    e.Handled = true;
                    return;
                }
                else
                {
                    if (tsplr.Teleport(Main.spawnTileX * 16, (Main.spawnTileY * 16) - 48))
                        tsplr.SendMessage("Teleported to the map's spawnpoint.", Color.Green);
                    e.Handled = true;
                    return;
                }
            }
            else if ((cmd == "/afk" || cmd == "/i" || cmd == "/give") && JPConfig.cmdlimit)
            {
                if (text.Split(' ').Length > 1)
                {
                    if (!tsplr.Group.HasPermission("alwaysitem") && jpPly.itemAmount > 0)
                    {
                        tsplr.SendMessage("You Can Only Use An Item Command Every " + JPConfig.cmdtime + " Seconds You Have " + jpPly.itemAmount + " Seconds Left", Color.Red);
                        e.Handled = true;
                        return;
                    }
                    else if (!tsplr.Group.HasPermission("alwaysitem") && tsplr.Group.HasPermission("item"))
                        jpPly.itemAmount = JPConfig.cmdtime;
                    return;
                }
                return;
            }

            else if (cmd == "/heal" && JPConfig.cmdlimit)
            {
                if (!tsplr.Group.HasPermission("alwaysheal") && jpPly.healAmount > 0)
                {
                    tsplr.SendMessage("You Can Only Use The Heal Command Every " + JPConfig.cmdtime + " Seconds You Have " + jpPly.healAmount + " Seconds Left", Color.Red);
                    e.Handled = true;
                    return;
                }
                else if (!tsplr.Group.HasPermission("alwaysheal") && tsplr.Group.HasPermission("heal"))
                    jpPly.healAmount = JPConfig.cmdtime;
                return;
            }
            else if (cmd == "/afktime" && JPConfig.afkmode)
            {
                return;
            }
            jpPly.idle = 1;
        }

        private void warpjail(CommandArgs args)
        {

            if (!args.Player.RealPlayer)
            {
                args.Player.SendMessage("You cannot use teleport commands!", Color.Red);
                return;
            }

            var jpPly = Players[args.Player.Index];

            if (jpPly.prisonMode)
            {
                args.Player.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...", Color.Red);
                return;
            }
            var currentregion = TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY);
            if (currentregion.Count > 0)
            {
                string currentregionlist = string.Join(",", currentregion.ToArray());
                if (currentregionlist.Contains("jail"))
                    args.Player.SendMessage("You May Now Exit The Jail!", Color.Pink);
            }
            jpPly.rulesMode = false;
            args.Player.SendMessage("Thanks For Reading The Rules!", Color.Pink);
            var foundplr = TShock.Utils.FindPlayer(args.Player.Name);
            if (JPConfig.groupname != "" && args.Player.Group.HasPermission("rulerank") && foundplr[0].IsLoggedIn)
            {
                var foundgrp = FindGroup(JPConfig.groupname);
                if (foundgrp.Count == 1)
                {
                    var loggeduser = TShock.Users.GetUserByName(args.Player.UserAccountName);
                    TShock.Users.SetUserGroup(loggeduser, foundgrp[0].Name);
                    args.Player.Group = foundgrp[0];
                    args.Player.SendMessage("Your Group Has Been Changed To " + foundgrp[0].Name, Color.Pink);
                    return;
                }
            }

            if (JPConfig.guestgroupname != "" && !foundplr[0].IsLoggedIn)
            {
                var foundguestgrp = FindGroup(JPConfig.guestgroupname);
                if (foundguestgrp.Count == 1)
                {
                    args.Player.Group = foundguestgrp[0];
                    args.Player.SendMessage("Your Group Has Temporarily Changed To " + foundguestgrp[0].Name, Color.HotPink);
                    args.Player.SendMessage("Use /register & /login to create a permanent account - Once Complete Type /" + JPConfig.jailcomm + " Again.", Color.HotPink);
                }
            }
        }

        private void imprison(CommandArgs args)
        {

            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /imprison [player]", Color.Red);
                return;
            }

            var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (foundplr.Count == 0)
            {
                args.Player.SendMessage("Invalid player!", Color.Red);
                return;
            }
            else if (foundplr.Count > 1)
            {
                args.Player.SendMessage(string.Format("More than one ({0}) player matched!", args.Parameters.Count), Color.Red);
                return;
            }
            else if (foundplr[0].Group.HasPermission("prison"))
            {
                args.Player.SendMessage(string.Format("You Cannot Use This Command On This Player!", args.Parameters.Count), Color.Red);
                return;
            }
            var plr = foundplr[0];
            var jpPly = Players[plr.Index];
            if (jpPly.prisonMode)
            {
                args.Player.SendMessage("Player Is Already In Prison", Color.Red);
                return;
            }
            string warpName = "prison";
            var warp = TShock.Warps.Find(warpName);
            if (warp != null)
            {
                if (plr.Teleport((int)warp.Position.X * 16, (int)(warp.Position.Y) * 16))
                {
                    plr.SendMessage(string.Format("{0} Warped you to the Prison! You Cannot Get Out Until An Admin Releases You", args.Player.Name), Color.Yellow);
                    args.Player.SendMessage(string.Format("You warped {0} to Prison!", plr.Name), Color.Yellow);
                    jpPly.prisonMode = !jpPly.prisonMode;
                }
            }
            else
            {
                args.Player.SendMessage("Prison Warp Was Not Made! Make One!", Color.Red);
            }
        }
        private void setfree(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /setfree [player]", Color.Red);
                return;
            }

            var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (foundplr.Count == 0)
            {
                args.Player.SendMessage("Invalid player!", Color.Red);
                return;
            }
            else if (foundplr.Count > 1)
            {
                args.Player.SendMessage(string.Format("More than one ({0}) player matched!", args.Parameters.Count), Color.Red);
                return;
            }
            else if (foundplr[0].Group.HasPermission("prison"))
            {
                args.Player.SendMessage(string.Format("You Cannot Use This Command On This Player!", args.Parameters.Count), Color.Red);
                return;
            }
            var plr = foundplr[0];
            var jpPly = Players[plr.Index];
            if (!jpPly.prisonMode)
            {
                args.Player.SendMessage("Player Is Already Free", Color.Red);
                return;
            }
            if (plr.Teleport(Main.spawnTileX * 16, Main.spawnTileY + 3 * 16))
            {
                plr.SendMessage(string.Format("{0} Warped You To Spawn From Prison! Now Behave!!!!!", args.Player.Name), Color.Green);
                args.Player.SendMessage(string.Format("You warped {0} to Spawn from Prison!", plr.Name), Color.Yellow);
                jpPly.prisonMode = !jpPly.prisonMode;
                jpPly.prisonModeSpam = true;
            }
        }
        private void jailreload(CommandArgs args)
        {
            SetupConfig();
            Log.Info("Jail Reload Initiated");
            args.Player.SendMessage("Jail Reload Initiated", Color.Green);
        }

        private void chests(CommandArgs args)
        {
            if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /allowchests <player> [amountoftime]", Color.Red);
                return;
            }

            var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (foundplr.Count == 0)
            {
                args.Player.SendMessage("Invalid player!", Color.Red);
                return;
            }
            else if (foundplr.Count > 1)
            {
                args.Player.SendMessage(string.Format("More than one ({0}) player matched!", args.Parameters.Count), Color.Red);
                return;
            }
            int chestamount2 = 0;
            args.Parameters.RemoveAt(0);
            var jpPly = Players[foundplr[0].Index];
            if (args.Parameters.Count > 0)
            {
                int.TryParse(args.Parameters[args.Parameters.Count - 1], out chestamount2);
                jpPly.chestAmount = chestamount2;
            }

            if (jpPly.chestMode)
            {
                if (jpPly.chestAmount == 0)
                    jpPly.chestAmount = 60;
                jpPly.chestMode = !jpPly.chestMode;
                foundplr[0].SendMessage("You Have Been Granted Access To The Chests By " + args.Player.Name + " For " + jpPly.chestAmount + " Seconds!", Color.Green);
                args.Player.SendMessage("You Have Granted Access For The Chests To " + foundplr[0].Name + " For " + jpPly.chestAmount + " Seconds!", Color.Green);
                return;
            }
            if (!jpPly.chestMode)
            {
                jpPly.chestMode = !jpPly.chestMode;
                foundplr[0].SendMessage("Your Access To The Chests Has Been Revoked By " + args.Player.Name, Color.Red);
                args.Player.SendMessage("You Have Revoked Access From The Chests From " + foundplr[0].Name, Color.Red);
                jpPly.chestModeSpam = true;
                return;
            }
        }

        private void idlecomm(CommandArgs args)
        {
            var jpPly = Players[args.Player.Index];
            TShock.Players[args.Player.Index].SendMessage("You Have Been AFK For: " + jpPly.idle, Color.Red);
        }

        private void sendafk(CommandArgs args)
        {
            var jpPly = Players[args.Player.Index];
            var warp = TShock.Warps.Find("afk");
            jpPly.backtileX = TShock.Players[args.Player.Index].TileX;
            jpPly.backtileY = TShock.Players[args.Player.Index].TileY;
            if (args.Player.Teleport((int)warp.Position.X * 16, (int)warp.Position.Y * 16 + 3))
            {
                args.Player.SendMessage("You Have Been Warped To The AFK Zone. Use the /back command to go back!", Color.Red);
                TShock.Utils.Broadcast(args.Player.Name + " Is Away From His/Her Keyboard And Has Been Warped To The AFK Zone!", Color.Red);
            }
        }

        private void sendback(CommandArgs args)
        {
            var jpPly = Players[args.Player.Index];
            if (jpPly.backtileX != 0)
            {
                if (args.Player.Teleport(jpPly.backtileX * 16, jpPly.backtileY * 16 + 3))
                {
                    args.Player.SendMessage("You Have Been Warped Back To Where You Were", Color.Green);
                    TShock.Utils.Broadcast(args.Player.Name + " Is Back From AFK! YAY!!!", Color.Green);
                    jpPly.backtileX = 0;
                    jpPly.backtileY = 0;
                }
            }
            else
                args.Player.SendMessage("Unable To Send You Back From AFK", Color.Green);
        }

        private static void setidletime(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /setafktime <time>", Color.Red);
                return;
            }
            JPConfig.afktime = int.Parse(args.Parameters[0]);
            args.Player.SendMessage("Idle Time Set To: " + JPConfig.afktime, Color.Red);
        }

        private static void setcmdtime(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /setJPConfig.cmdtime <time>", Color.Red);
                return;
            }
            JPConfig.cmdtime = int.Parse(args.Parameters[0]);
            args.Player.SendMessage("Command Time Set To: " + JPConfig.cmdtime, Color.Green);
        }

        private void killzone(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /killzone <player>", Color.Red);
                return;
            }

            var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
            var jpPly = Players[foundplr[0].Index];
            if (foundplr.Count == 0)
            {
                args.Player.SendMessage("Invalid player!", Color.Red);
                return;
            }
            else if (foundplr.Count > 1)
            {
                args.Player.SendMessage(string.Format("More than one ({0}) player matched!", args.Parameters.Count), Color.Red);
                return;
            }
            if (!jpPly.killMode)
            {
                jpPly.killMode = !jpPly.killMode;
                foundplr[0].SendMessage("You Have Been Granted Access To Kill Zones From " + args.Player.Name, Color.Green);
                args.Player.SendMessage("You Have Granted Access To Kill Zones For " + foundplr[0].Name, Color.Green);
                return;
            }
            if (jpPly.killMode)
            {
                jpPly.killMode = !jpPly.killMode;
                foundplr[0].SendMessage("Your Access To The Kill Zones Has Been Revoked By " + args.Player.Name, Color.Red);
                args.Player.SendMessage("You Have Revoked Access From Kill Zones For " + foundplr[0].Name, Color.Red);
                return;
            }
        }

        public static List<Group> FindGroup(string grp)
        {
            var found = new List<Group>();
            grp = grp.ToLower();
            foreach (Group group in TShock.Groups.groups)
            {
                if (group == null)
                    continue;

                string name = group.Name.ToLower();
                if (name.Equals(grp))
                    return new List<Group> { group };
                if (name.Contains(grp))
                    found.Add(group);
            }
            return found;
        }
    }
}