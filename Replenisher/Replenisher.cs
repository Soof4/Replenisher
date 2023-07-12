using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace Replenisher {
    [ApiVersion(2, 1)]
    public class Replenisher : TerrariaPlugin {
        public override Version Version => new Version("1.2.1");
        public override string Name => "Replenisher";
        public override string Author => "omni";
        public override string Description => "Replenish your world's resources!";

        private static readonly int TIMEOUT = 10000;

        private Config config;

        private DateTime lastTime = DateTime.UtcNow;
        
        public Replenisher(Main game) : base(game) {
        }

        public override void Initialize() {
            Commands.ChatCommands.Add(new Command("tshock.world.causeevents", Replen, "replen"));
            Commands.ChatCommands.Add(new Command("tshock.world.causeevents", ConfigReload, "replenreload"));
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);

            if (!ReadConfig())
                TShock.Log.ConsoleError("Error in config file. This will probably cause the plugin to crash if not resolved.");
        }
        private void OnUpdate(EventArgs e) {
            if ((DateTime.UtcNow - lastTime).TotalMinutes < config.AutoRefillTimerInMinutes) {
                return;
            }

            lastTime = DateTime.UtcNow;

            if (config.ReplenChests) {
                TShock.Log.ConsoleInfo("Auto generating chests...");
                PrivateReplenisher(GenType.chests, config.ChestAmount);
            }

            if (config.ReplenLifeCrystals) {
                TShock.Log.ConsoleInfo("Auto generating life crystals...");
                PrivateReplenisher(GenType.lifecrystals, config.LifeCrystalAmount);
            }

            if (config.ReplenOres) {
                TShock.Log.ConsoleInfo("Auto generating ores...");
                var obj = new Terraria.ID.TileID();
                ushort oretype;
                try {
                    foreach (string s in config.OreToReplen) {
                        oretype = (ushort)obj.GetType().GetField(s.FirstCharToUpper()).GetValue(obj);
                        PrivateReplenisher(GenType.ore, config.OreAmount, oretype);
                    }
                }
                catch (ArgumentException) { }
            }
            if (config.ReplenPots) {
                TShock.Log.ConsoleInfo("Auto generating pots...");
                PrivateReplenisher(GenType.pots, config.PotsAmount);
            }
            if (config.ReplenTrees) {
                TShock.Log.ConsoleInfo("Auto generating trees...");
                PrivateReplenisher(GenType.trees, config.TreesAmount);
            }

        }

        private void CreateConfig() {
            string filepath = Path.Combine(TShock.SavePath, "ReplenisherConfig.json");
            try {
                using (var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Write)) {
                    using (var sr = new StreamWriter(stream)) {
                        config = new Config();
                        var configString = JsonConvert.SerializeObject(config, Formatting.Indented);
                        sr.Write(configString);
                    }
                    stream.Close();
                }
            }
            catch (Exception ex) {
                TShock.Log.ConsoleError(ex.Message);
                config = new Config();
            }
        }
        private bool ReadConfig() {
            string filepath = Path.Combine(TShock.SavePath, "ReplenisherConfig.json");
            try {
                if (File.Exists(filepath)) {
                    using (var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        using (var sr = new StreamReader(stream)) {
                            var configString = sr.ReadToEnd();
                            config = JsonConvert.DeserializeObject<Config>(configString);
                        }
                        stream.Close();
                    }
                    return true;
                }
                else {
                    TShock.Log.ConsoleError("Replenisher config not found. Creating new one...");
                    CreateConfig();
                    return true;
                }
            }
            catch (JsonSerializationException ex) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in Replenisher config file. Please try to let the config file be generated automatically and manually import your settings. If that doesn't work, feel free to post about it.");
                Console.WriteLine(ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
            catch (Exception ex) {
                TShock.Log.ConsoleError(ex.Message);
            }
            return false;
        }
        private void ConfigReload(CommandArgs args) {
            if (ReadConfig())
                args.Player.SendSuccessMessage("Replenisher config reloaded.");
            else
                args.Player.SendErrorMessage("Error reading config. Check log for details.");
            return;
        }

        private int PrivateReplenisher(GenType type, int amount, ushort oretype = 0, CommandArgs args = null) {
            int counter = 0;
            bool success;
            for (int i = 0; i < TIMEOUT; i++) {
                success = false;
                int xRandBase = WorldGen.genRand.Next(1, Main.maxTilesX);
                int y = 0;
                switch (type) {
                    case GenType.ore:
                        y = WorldGen.genRand.Next((int)(Main.worldSurface) - 12, Main.maxTilesY);
                        if (TShock.Regions.InAreaRegion(xRandBase, y).Any() && !config.GenerateInProtectedAreas) {
                            success = false;
                            break;
                        }
                        if (oretype == Terraria.ID.TileID.Hellstone) {
                            WorldGen.OreRunner(xRandBase, WorldGen.genRand.Next((int)(Main.maxTilesY) - 200, Main.maxTilesY), 2.0, amount, oretype);
                        }
                        else {
                            WorldGen.OreRunner(xRandBase, y, 2.0, amount, oretype);
                        } 
                        success = true;
                        break;
                    case GenType.chests:
                        if (amount == 0) {    // uproot chests
                            int tmpEmpty = 0, empty = 0;
                            for (int x = 0; x < Main.maxChests; x++) {
                                if (Main.chest[x] != null) {
                                    tmpEmpty++;
                                    bool found = false;
                                    foreach (Item itm in Main.chest[x].item) {
                                        if (itm.netID != 0) {
                                            found = true;
                                        }
                                    }

                                    if (found == false) {
                                        empty++;
                                        WorldGen.KillTile(Main.chest[x].x, Main.chest[x].y, false, false, false);
                                        Main.chest[x] = null;

                                    }
                                }
                            }
                            args.Player.SendSuccessMessage("Uprooted {0} empty out of {1} chests.", empty, tmpEmpty);
                            return counter;
                        }
                        y = WorldGen.genRand.Next((int)((Main.worldSurface)*0.90), Main.maxTilesY);
                        if (TShock.Regions.InAreaRegion(xRandBase, y).Any() && !config.GenerateInProtectedAreas) {
                            success = false;
                            break;
                        }

                        try { success = WorldGen.AddBuriedChest(xRandBase, y); }
                        catch (Exception){ success = false; }

                        break;
                    case GenType.pots:
                        y = WorldGen.genRand.Next((int)(Main.worldSurface) - 12, Main.maxTilesY);
                        if (TShock.Regions.InAreaRegion(xRandBase, y).Any() && !config.GenerateInProtectedAreas) {
                            success = false;
                            break;
                        }
                        success = WorldGen.PlacePot(xRandBase, y);
                        break;
                    case GenType.lifecrystals:
                        y = WorldGen.genRand.Next((int)(Main.worldSurface) - 12, Main.maxTilesY);
                        if (TShock.Regions.InAreaRegion(xRandBase, y).Any() && !config.GenerateInProtectedAreas) {
                            success = false;
                            break;
                        }
                        success = WorldGen.AddLifeCrystal(xRandBase, y);
                        break;
                    case GenType.altars:
                        y = WorldGen.genRand.Next((int)(Main.worldSurface) - 12, Main.maxTilesY);
                        if (TShock.Regions.InAreaRegion(xRandBase, y).Any() && !config.GenerateInProtectedAreas) {
                            success = false;
                            break;
                        }
                        WorldGen.Place3x2(xRandBase, y, 26);
                        success = Main.tile[xRandBase, y].type == 26;
                        break;
                    case GenType.trees:
                        WorldGen.AddTrees();
                        success = true;
                        break;
                    case GenType.floatingisland:
                        y = WorldGen.genRand.Next((int)(Main.worldSurface*0.22), (int)(Main.worldSurface*0.35));
                        if (TShock.Regions.InAreaRegion(xRandBase, y).Any() && !config.GenerateInProtectedAreas) {
                            success = false;
                            break;
                        }
                        WorldGen.FloatingIsland(xRandBase, y);
                        success = true;
                        break;
                    case GenType.pyramids:
                        y = WorldGen.genRand.Next((int)(Main.worldSurface * 0.9), (int)(Main.worldSurface * 1.2));
                        if (Main.tile[xRandBase, y].type != 53 || xRandBase > Main.maxTilesX - WorldGen.oceanDistance || xRandBase < WorldGen.oceanDistance ) {
                            success = false;
                            break;
                        } 
                        success = WorldGen.Pyramid(xRandBase, y);
                        break;
                }
                if (success) {
                    counter++;
                    if (counter >= amount)
                        return counter;
                }
            }
            return counter;
        }
        public void Replen(CommandArgs args) {
            GenType type = GenType.ore;
            int amount = -1;
            ushort oretype = 0;
            int counter = 0;

            if (args.Parameters.Count >= 2 && Enum.TryParse<GenType>(args.Parameters[0], true, out type) && int.TryParse(args.Parameters[1], out amount)) {
                if (type == GenType.ore) {
                    if (args.Parameters.Count < 3) {
                        args.Player.SendErrorMessage("Please enter a valid ore type.");
                        return;
                    }
                    var obj = new Terraria.ID.TileID();
                    try { oretype = (ushort)obj.GetType().GetField(args.Parameters[2].FirstCharToUpper()).GetValue(obj); }
                    catch (ArgumentException) { args.Player.SendErrorMessage("Please enter a valid ore type."); }
                }
                else if (type == GenType.trees) {
                    if (args.Parameters.Count >= 3)
                        args.Player.SendInfoMessage("CAUTION: The number entered is not the number of trees total. It refers to the number of batches of trees to generate.");
                }
                if ((counter = PrivateReplenisher(type, amount, oretype, args)) == amount) {
                    args.Player.SendInfoMessage(type.ToString().FirstCharToUpper() + " generated successfully.");
                    return;
                }
                args.Player.SendErrorMessage($"Failed to generate all the {type}. Generated {counter} {type}.");
            }
            else
                args.Player.SendErrorMessage("Incorrect usage. Correct usage: /replen <ore|chests|pots|lifecrystals|altars|trees|floatingisland|pyramids> <amount> (oretype)\r\nNote: when generating trees, the amount is in batches not specific trees.");
        }
    }
    public enum GenType {
        ore,
        chests,
        pots,
        lifecrystals,
        altars,
        trees,
        pyramids,
        floatingisland,
    }
    public class Config {
        [JsonConstructor]
        public Config() {
            OreToReplen = new List<string>();
        }
        public bool GenerateInProtectedAreas, AutomaticallRefill;
        public int AutoRefillTimerInMinutes = 30;
        public bool ReplenOres;
        public List<string> OreToReplen = new List<string> { "Copper", "Iron" };
        public int OreAmount;
        public bool ReplenChests;
        public int ChestAmount;
        public bool ReplenPots;
        public int PotsAmount;
        public bool ReplenLifeCrystals;
        public int LifeCrystalAmount;
        public bool ReplenTrees;
        public int TreesAmount;
    }
}