using HogWarp;
using HogWarp.Lib;
using HogWarp.Lib.Game;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HogWarpMods
{
    public class NPCBattleSync : IPluginBase
    {
        public string Name => "NPCBattleSync";
        public string Description => "Synchronizes NPC battles across players.";

        private Server? _server;
        private NPCManager? _npcManager;

        public void Initialize(Server server)
        {
            _server = server;

            string npcJsonFilePath = Path.Combine("plugins", "NPCBattleSync", "npcs.json");
            _npcManager = new NPCManager(_server, npcJsonFilePath);

            _server.Information("NPC Battle Sync Mod Initialized.");

            // Subscribe to server events
            _server.PlayerJoinEvent += OnPlayerJoin;
            _server.UpdateEvent += OnUpdate;
            _server.ChatEvent += OnChat;
        }

        private void OnPlayerJoin(Player player)
        {
            _server!.Information($"Player {player.Name} joined. Syncing combat state...");
            SyncCombatStateToPlayer(player);
        }

        private void OnChat(Player player, string message, ref bool cancel)
        {
            if (message == "/npcstatus")
            {
                _server!.Information($"Player {player.Name} requested NPC status.");
                foreach (var npc in _npcManager!.GetAllNPCs())
                {
                    player.SendMessage($"NPC {npc.Name} - Health: {npc.Health}, Alive: {npc.IsAlive}, Position: {npc.Position}");
                }
                cancel = true; // Prevent the command from being processed further
            }
            else if (message == "/dumpnpcs")
            {
                _server!.Information($"Player {player.Name} requested to dump NPC data.");
                _npcManager!.DumpNPCsToJson();
                player.SendMessage("NPC data has been dumped to npcs.json.");
                cancel = true;
            }
        }

        private void OnUpdate(float deltaSeconds)
        {
            foreach (var npc in _npcManager!.GetAllNPCs())
            {
                if (!npc.IsAlive)
                {
                    _server!.Information($"Reviving NPC {npc.Name} automatically...");
                    _npcManager.ReviveNPC(npc.Name);
                }
            }
        }

        private void SyncCombatStateToPlayer(Player player)
        {
            _server!.Information($"Syncing combat state to player {player.Name}...");
            foreach (var npc in _npcManager!.GetAllNPCs())
            {
                player.SendMessage($"NPC {npc.Name} - Health: {npc.Health}, Position: {npc.Position}");
                _server!.Information($"Sent NPC data to {player.Name}: {npc.Name}, Health: {npc.Health}");
            }
        }

        private void OnNPCDamaged(string npcName, float damage)
        {
            _server!.Information($"NPC {npcName} damaged by {damage}.");
            _npcManager!.DamageNPC(npcName, damage);
            SyncCombatState();
        }

        private void SyncCombatState()
        {
            _server!.Information("Syncing combat state across all players...");
            foreach (var npc in _npcManager!.GetAllNPCs())
            {
                foreach (var player in _server!.PlayerManager.Players)
                {
                    player.SendMessage($"NPC {npc.Name}: Health {npc.Health}, Alive: {npc.IsAlive}, Position: {npc.Position}");
                }
            }
        }
    }
    public class NPCManager
    {
        private readonly List<NPC> _npcs;
        private readonly Server _server;

        public NPCManager(Server server, string jsonFilePath)
        {
            _server = server;
            _npcs = LoadNPCsFromJson(jsonFilePath);
        }

        private List<NPC> LoadNPCsFromJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
            {
                _server.Warning($"NPC JSON file not found: {jsonFilePath}. Creating a default file.");

                // Create a default JSON file
                var defaultNPCs = new List<NPC>
                {
                    new NPC { Name = "Default Goblin", Health = 100, Position = "(0, 0, 0)", IsAlive = true }
                };

                SaveNPCsToJson(jsonFilePath, defaultNPCs);
                _server.Information("Default NPC JSON file created.");
                return defaultNPCs;
            }

            try
            {
                string json = File.ReadAllText(jsonFilePath);
                _server.Information("NPC JSON file loaded successfully.");
                return JsonConvert.DeserializeObject<List<NPC>>(json) ?? new List<NPC>();
            }
            catch (Exception ex)
            {
                _server.Error($"Failed to load NPCs from JSON: {ex.Message}");
                return new List<NPC>();
            }
        }

        public IEnumerable<NPC> GetAllNPCs()
        {
            return _npcs;
        }

        public void DumpNPCsToJson()
        {
            try
            {
                // Replace GetAllEntities with an actual method to fetch NPCs if available in the API
                var allNPCs = _npcs; // Assume _npcs holds all NPCs dynamically, replace if needed

                // Save to JSON file
                string npcJsonFilePath = Path.Combine("plugins", "NPCBattleSync", "npcs.json");
                SaveNPCsToJson(npcJsonFilePath, allNPCs);

                _server.Information($"NPC data successfully dumped to {npcJsonFilePath}.");
            }
            catch (Exception ex)
            {
                _server.Error($"Failed to dump NPC data: {ex.Message}");
            }
        }

        public void DamageNPC(string name, float damage)
        {
            var npc = GetNPCByName(name);
            if (npc != null && npc.IsAlive)
            {
                npc.Health -= damage;
                if (npc.Health <= 0)
                {
                    npc.Health = 0;
                    npc.IsAlive = false;
                }
                _server.Information($"NPC {npc.Name} now has {npc.Health} health and isAlive: {npc.IsAlive}");
            }
        }

        public void ReviveNPC(string name)
        {
            var npc = GetNPCByName(name);
            if (npc != null && !npc.IsAlive)
            {
                npc.Health = 100; // Reset health to default value
                npc.IsAlive = true;
                _server.Information($"NPC {npc.Name} has been revived with {npc.Health} health.");
            }
        }

        private NPC? GetNPCByName(string name)
        {
            foreach (var npc in _npcs)
            {
                if (npc.Name == name)
                {
                    return npc;
                }
            }

            return null;
        }

        public void SaveNPCsToJson(string filePath, List<NPC>? npcs = null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(npcs ?? _npcs, Formatting.Indented);
                File.WriteAllText(filePath, json);
                _server.Information($"NPC data saved to {filePath}");
            }
            catch (Exception ex)
            {
                _server.Error($"Failed to save NPC data to JSON: {ex.Message}");
            }
        }
    }

    public class NPC
    {
        public string Name { get; set; } = string.Empty;
        public float Health { get; set; }
        public string Position { get; set; } = string.Empty;
        public bool IsAlive { get; set; }
    }
}