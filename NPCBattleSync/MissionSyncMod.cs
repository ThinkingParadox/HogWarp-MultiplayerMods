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
    public class MissionSyncMod : IPluginBase
    {
        public string Name => "MissionSyncMod";
        public string Description => "Synchronizes mission progress across players.";

        private Server? _server;
        private readonly Dictionary<string, string> _sharedMissionState = new Dictionary<string, string>();

        public void Initialize(Server server)
        {
            _server = server;

            _server.Information("Mission Sync Mod Initialized.");

            // Subscribe to server events
            _server.PlayerJoinEvent += OnPlayerJoin;
        }

        private void OnPlayerJoin(Player player)
        {
            _server!.Information($"Player {player.Name} joined. Syncing missions...");
            SyncMissionsToPlayer(player);
        }

        private void SyncMissionsToPlayer(Player player)
        {
            foreach (var mission in _sharedMissionState)
            {
                player.SendMessage($"Mission: {mission.Key}, State: {mission.Value}");
                _server!.Information($"Synced mission {mission.Key} with state {mission.Value} to {player.Name}");
            }
        }

        public void UpdateMissionState(string missionName, string newState)
        {
            _server!.Information($"Updating mission state: {missionName} -> {newState}");

            if (_sharedMissionState.ContainsKey(missionName))
            {
                _sharedMissionState[missionName] = newState;
            }
            else
            {
                _sharedMissionState.Add(missionName, newState);
            }

            foreach (var player in _server.PlayerManager.Players)
            {
                player.SendMessage($"Mission Updated: {missionName} is now {newState}");
                _server!.Information($"Mission update sent to player {player.Name}: {missionName} -> {newState}");
            }
        }

        public void SaveMissionStatesToJson(string filePath, Dictionary<string, string>? states = null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(states ?? _sharedMissionState, Formatting.Indented);
                File.WriteAllText(filePath, json);
                _server!.Information($"Mission states saved to {filePath}");
            }
            catch (Exception ex)
            {
                _server!.Error($"Failed to save mission states to JSON: {ex.Message}");
            }
        }

        public void LoadMissionStatesFromJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _server!.Warning($"Mission JSON file not found: {filePath}. Creating a default file.");

                // Create a default mission state
                var defaultMissionState = new Dictionary<string, string>
                {
                    { "Default Mission", "Not Started" }
                };

                SaveMissionStatesToJson(filePath, defaultMissionState);
                _server!.Information("Default mission JSON file created.");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var missionStates = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (missionStates != null)
                {
                    foreach (var mission in missionStates)
                    {
                        _sharedMissionState[mission.Key] = mission.Value;
                    }
                }

                _server!.Information("Mission states loaded from JSON.");
            }
            catch (Exception ex)
            {
                _server!.Error($"Failed to load mission states from JSON: {ex.Message}");
            }
        }
    }
}