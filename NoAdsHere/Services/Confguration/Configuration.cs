﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using NLog;

namespace NoAdsHere.Services.Confguration
{
    public sealed class Config
    {
        private static readonly Logger _logger = LogManager.GetLogger("Config");

        private Config()
        {
        }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("command_activation_strings")]
        public IEnumerable<string> CommandStrings { get; set; } = new[]
        {
            "?nah"
        };

        [JsonProperty("command_on_mention")]
        public bool TriggerOnMention { get; set; } = true;

        [JsonProperty("channels")]
        public IEnumerable<ulong> ChannelWhitelist { get; set; } = new ulong[0];

        public class ConfigDatabase
        {
            [JsonProperty("host")]
            public string Host { get; set; } = "localhost";

            [JsonProperty("port")]
            public int Port { get; set; } = 27017;

            [JsonProperty("db")]
            public string Db { get; set; } = "admin";

            [JsonProperty("user")]
            public string Username { get; set; } = "user";

            [JsonProperty("password")]
            public string Password { get; set; } = "password";

            [JsonIgnore]
            public string ConnectionString => $"mongodb://{Username}:{Password}@{Host}:{Port}/{Db}";
        }

        [JsonProperty("db")]
        public ConfigDatabase Database { get; set; } = new ConfigDatabase();

        public static Config Load()
        {
            _logger.Info("Loading Configuration from config.json");
            if (File.Exists("config.json"))
            {
                var json = File.ReadAllText("config.json");
                return JsonConvert.DeserializeObject<Config>(json);
            }
            var config = new Config();
            config.Save();
            throw new InvalidOperationException("configuration file created; insert token and restart.");
        }

        public static Config LoadFrom(string path)
        {
            _logger.Info($"Loading Configuration from {path}");
            path = Path.Combine(path, "config.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<Config>(json);
            }
            else
                throw new FileNotFoundException("Config needs to be present!");
        }

        public void Save()
        {
            _logger.Info("Saving Configuration to config.json");
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText("config.json", json);
        }
    }
}