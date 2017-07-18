using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using NoAdsHere.Common;
using NoAdsHere.Services.Database;

namespace NoAdsHere.Database.Models.Guild
{
    public class Ignore : DatabaseService, IGuildIndexed
    {
        public Ignore(ulong guildId, IgnoreType ignoreType, ulong ignoredId, BlockType blockType)
        {
            GuildId = guildId;
            IgnoreType = ignoreType;
            IgnoredId = ignoredId;
            BlockType = blockType;
        }

        public ObjectId Id { get; set; }
        public ulong GuildId { get; set; }
        public IgnoreType IgnoreType { get; set; }
        public ulong IgnoredId { get; set; }
        public BlockType BlockType { get; set; }

        internal async Task<DeleteResult> DeleteAsync()
        {
            var collection = Db.GetCollection<Ignore>();
            return await collection.DeleteOneAsync(i => i.Id == Id);
        }

        internal async Task<ReplaceOneResult> UpdateAsync()
        {
            var collection = Db.GetCollection<Ignore>();
            return await collection.ReplaceOneAsync(i => i.Id == Id, this, new UpdateOptions { IsUpsert = true });
        }
    }
}