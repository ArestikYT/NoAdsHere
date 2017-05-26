﻿using System.Threading.Tasks;
using Discord;
using MongoDB.Bson;
using MongoDB.Driver;
using NoAdsHere.Database.Models.GuildSettings;
using NoAdsHere.Database.Models.Violator;
using System.Collections.Generic;

namespace NoAdsHere.Database
{
    public interface IIndexed
    {
        ObjectId Id { get; set; }
    }

    public interface IGuildIndexed : IIndexed
    {
        ulong GuildId { get; set; }
    }

    public static class DatabaseExtentions
    {
        public static IMongoCollection<T> GetCollection<T>(this MongoClient mongo, IDiscordClient client)
        {
            var dbname = client.CurrentUser.Username.Replace(" ", "");
            var db = mongo.GetDatabase(dbname);
            return db.GetCollection<T>(typeof(T).Name);
        }

        public static async Task<ReplaceOneResult> SaveAsync<T>(this IMongoCollection<T> collection, T entity) where T : IIndexed
        {
            return await collection.ReplaceOneAsync(i => i.Id == entity.Id, entity, new UpdateOptions { IsUpsert = true });
        }

        public static async Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, T entity) where T : IIndexed
        {
            return await collection.DeleteOneAsync(i => i.Id == entity.Id);
        }

        public static async Task<T> SingleByGuildAsync<T>(this IMongoCollection<T> collection, ulong guildId) where T : IGuildIndexed
        {
            var cursor = await collection.FindAsync(f => f.GuildId == guildId);
            var result = await cursor.SingleOrDefaultAsync();

            if (result != null)
                return result;
            return default(T);
        }

        public static async Task<List<T>> ListByGuildAsync<T>(this IMongoCollection<T> collection, ulong guildId) where T : IGuildIndexed
        {
            var cursor = await collection.FindAsync(f => f.GuildId == guildId);
            var result = await cursor.ToListAsync();

            return result;
        }
        
        public static async Task<Block> GetBlockAsync(this IMongoCollection<Block> collection, ulong guildId, BlockTypes type)
        {
            var cursor = await collection.FindAsync(f => f.GuildId == guildId && f.BlockType == type);
            var result = await cursor.SingleOrDefaultAsync();

            if (result != null)
                return result;
            await collection.InsertOneAsync(new Block(guildId, type));
            var cursor2 = await collection.FindAsync(f => f.GuildId == guildId && f.BlockType == type);
            var result2 = await cursor2.SingleOrDefaultAsync();
            return result2;
        }
        
        public static async Task<List<Ignore>> GetIgnoresAsync(this IMongoCollection<Ignore> collection, ulong guildId, IgnoreingTypes type)
        {
            IAsyncCursor<Ignore> cursor;
            if (type == IgnoreingTypes.Disabled)
                cursor = await collection.FindAsync(f => f.GuildId == guildId && (f.IgnoreingType == type));
            else
                cursor = await collection.FindAsync(f => f.GuildId == guildId && (f.IgnoreingType == type || f.IgnoreingType == IgnoreingTypes.All));
            var result = await cursor.ToListAsync();

            return result ?? new List<Ignore>(0);
        }

        public static async Task<Penalty> GetPenaltyAsync(this IMongoCollection<Penalty> collection, ulong guildId, int penaltyId)
        {
            var cursor = await collection.FindAsync(f => f.GuildId == guildId && f.PenaltyId == penaltyId);
            var result = await cursor.SingleOrDefaultAsync();
            return result;
        }
        
        public static async Task<List<Penalty>> GetPenaltiesAsync(this IMongoCollection<Penalty> collection, ulong guildId)
        {
            var cursor = await collection.FindAsync(f => f.GuildId == guildId);
            var result = await cursor.ToListAsync();
            return result;
        }

        public static async Task<Violator> GetUserAsync(this IMongoCollection<Violator> collection, IGuildUser user)
        {
            var cursor = await collection.FindAsync(f => f.GuildId == user.Guild.Id && f.UserId == user.Id);
            var result = await cursor.SingleOrDefaultAsync();

            if (result != null)
                return result;
            await collection.InsertOneAsync(new Violator(user.Guild.Id, user.Id));
            var cursor2 = await collection.FindAsync(f => f.GuildId == user.Guild.Id && f.UserId == user.Id);
            var result2 = await cursor2.SingleOrDefaultAsync();
            return result2;
        }
    }
}