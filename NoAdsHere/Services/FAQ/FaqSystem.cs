﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using MongoDB.Driver;
using MoreLinq;
using NoAdsHere.Common;
using NoAdsHere.Database;
using NoAdsHere.Database.Models.FAQ;
using NoAdsHere.Services.Configuration;

namespace NoAdsHere.Services.FAQ
{
    public class FaqSystem
    {
        private readonly DiscordShardedClient _client;
        private readonly MongoClient _mongo;
        private readonly Config _config;

        public FaqSystem(DiscordShardedClient client, MongoClient mongo, Config config)
        {
            _client = client;
            _mongo = mongo;
            _config = config;
        }

        internal async Task<bool> AddGuildEntryAsync(ICommandContext context, string name, string response)
        {
            var lEntry = new GuildFaqEntry
            {
                GuildId = context.Guild.Id,
                CreatorId = context.User.Id,
                Name = name,
                Content = response,
                CreatedAt = DateTime.UtcNow,
                LastUsed = DateTime.MinValue,
                UseCount = 0
            };
            if (await GetGlobalFaqEntryAsync(name).ConfigureAwait(false) != null) return false;
            if (await GetGuildFaqEntryAsync(context.Guild.Id, name).ConfigureAwait(false) != null) return false;

            var collection = _mongo.GetCollection<GuildFaqEntry>(_client);
            await collection.InsertOneAsync(lEntry);
            return true;
        }

        internal async Task<bool> AddGlobalEntryAsync(ICommandContext context, string name, string response)
        {
            var gEntry = new GlobalFaqEntry
            {
                CreatorId = context.User.Id,
                Name = name,
                Content = response,
                CreatedAt = DateTime.UtcNow,
                LastUsed = DateTime.MinValue,
                UseCount = 0
            };
            if (await GetGlobalFaqEntryAsync(name).ConfigureAwait(false) != null) return false;

            var collection = _mongo.GetCollection<GlobalFaqEntry>(_client);
            await collection.InsertOneAsync(gEntry);
            return true;
        }

        internal async Task<DeleteResult> RemoveGuildEntryAsync(GuildFaqEntry entry)
            => await _mongo.GetCollection<GuildFaqEntry>(_client).DeleteAsync(entry);

        internal async Task<DeleteResult> RemoveGlobalEntryAsync(GlobalFaqEntry entry)
            => await _mongo.GetCollection<GlobalFaqEntry>(_client).DeleteAsync(entry);

        internal async Task<ReplaceOneResult> SaveGuildEntryAsync(GuildFaqEntry entry)
            => await _mongo.GetCollection<GuildFaqEntry>(_client).SaveAsync(entry);

        internal async Task<ReplaceOneResult> SaveGlobalEntryAsync(GlobalFaqEntry entry)
            => await _mongo.GetCollection<GlobalFaqEntry>(_client).SaveAsync(entry);

        internal async Task<List<GuildFaqEntry>> GetGuildEntriesAsync(ulong guildId)
            => await _mongo.GetCollection<GuildFaqEntry>(_client).GetGuildFaqsAsync(guildId);

        internal async Task<List<GlobalFaqEntry>> GetGlobalEntriesAsync()
            => await _mongo.GetCollection<GlobalFaqEntry>(_client).GetGlobalFaqsAsync();

        internal async Task<GuildFaqEntry> GetGuildFaqEntryAsync(ulong guildId, string name)
            => await _mongo.GetCollection<GuildFaqEntry>(_client).GetGuildFaqAsync(guildId, name);

        internal async Task<GlobalFaqEntry> GetGlobalFaqEntryAsync(string name)
            => await _mongo.GetCollection<GlobalFaqEntry>(_client).GetGlobalFaqAsync(name);

        internal async Task<Dictionary<GuildFaqEntry, int>> GetSimilarGuildEntries(ulong guildId, string name)
        {
            var guildEntries = await _mongo.GetCollection<GuildFaqEntry>(_client).GetGuildFaqsAsync(guildId);
            var entryDictionary = guildEntries.ToDictionary(guildEntry => guildEntry,
                guildEntry => LevenshteinDistance.Compute(name, guildEntry.Name));
            return entryDictionary.Where(pair => pair.Value <= _config.MaxLevenshteinDistance).OrderBy(pair => pair.Value)
                .ToDictionary();
        }

        internal async Task<Dictionary<GlobalFaqEntry, int>> GetSimilarGlobalEntries(string name)
        {
            var guildEntries = await _mongo.GetCollection<GlobalFaqEntry>(_client).GetGlobalFaqsAsync();
            var entryDictionary = guildEntries.ToDictionary(globalEntry => globalEntry,
                globalEntry => LevenshteinDistance.Compute(name, globalEntry.Name));
            return entryDictionary.Where(pair => pair.Value <= _config.MaxLevenshteinDistance).OrderBy(pair => pair.Value)
                .ToDictionary();
        }
    }
}