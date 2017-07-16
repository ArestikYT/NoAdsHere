﻿using System;
using MongoDB.Bson;
using NoAdsHere.Common;
using System.Threading.Tasks;
using MongoDB.Driver;
using NoAdsHere.Services.Database;

namespace NoAdsHere.Database.Models.GuildSettings
{
    public class AllowString : DatabaseService, IIndexed
    {
        public AllowString(ulong guildId, IgnoreType ignoreType, ulong ignoredId, string allowedString)
        {
            GuildId = guildId;
            IgnoreType = ignoreType;
            IgnoredId = ignoredId;
            AllowedString = allowedString ?? throw new ArgumentNullException(nameof(allowedString));
        }

        public ObjectId Id { get; set; }
        public ulong GuildId { get; set; }
        public IgnoreType IgnoreType { get; set; }
        public ulong IgnoredId { get; set; }
        public string AllowedString { get; set; }

        internal async Task<DeleteResult> DeleteAsync()
        {
            var collection = _db.GetCollection<AllowString>();
            return await collection.DeleteOneAsync(i => i.Id == Id);
        }

        internal async Task<ReplaceOneResult> UpdateAsync()
        {
            var collection = _db.GetCollection<AllowString>();
            return await collection.ReplaceOneAsync(i => i.Id == Id, this, new UpdateOptions { IsUpsert = true });
        }
    }
}