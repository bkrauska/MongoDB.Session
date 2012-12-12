using MongoDB.Driver;
using MongoDB.Driver.Builders;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDB.Session {
    internal class MongoSessionHelper {
        private MongoClient _client;

        private readonly string _applicationName;
        private readonly string _databaseName = DefaultValues.DbName;
        private readonly string _collectionName = DefaultValues.CollectionName;

        public MongoSessionHelper(string applicationName, NameValueCollection config) {
            this._applicationName = applicationName;

            var connectionString = DefaultValues.ConnectionString;
            if (config[ConfigKeys.ConnectionString] != null) {
                connectionString = config[ConfigKeys.ConnectionString];
            }

            if (config[ConfigKeys.DbName] != null) {
                this._databaseName = config[ConfigKeys.DbName];
            }

            if (config[ConfigKeys.CollectionName] != null) {
                this._collectionName = config[ConfigKeys.CollectionName];
            }

            this._client = new MongoClient(connectionString);
        }

        public MongoCollection<SessionObject> GetCollection() {
            var server = this._client.GetServer();
            var database = server.GetDatabase(this._databaseName);
            return database.GetCollection<SessionObject>(_collectionName);
        }

        public bool LockSession(MongoCollection<SessionObject> collection, string sessionID) {
            var query = Query.And(
                Query<SessionObject>.EQ(x => x.BsonID, SessionObject.GetBsonId(this._applicationName, sessionID)),
                Query<SessionObject>.EQ(y => y.IsLocked, false),
                Query<SessionObject>.GT(z => z.ExpiresDate, DateTime.UtcNow)
            );
            var update = Update<SessionObject>
                .Set(x => x.IsLocked, true)
                .Set(y => y.LockedDate, DateTime.UtcNow)
                .Inc(z => z.LockID, 1);

            var result = collection.Update(query, update);
            return result.DocumentsAffected > 0;
        }

        public void UnlockMongoSessionObject(MongoCollection<SessionObject> collection, string sessionID, int lockID) {
            var query = Query.And(
                Query<SessionObject>.EQ(x => x.BsonID, SessionObject.GetBsonId(this._applicationName, sessionID)),
                Query<SessionObject>.EQ(y => y.LockID, lockID)
            );
            var update = Update<SessionObject>
                .Set(x => x.IsLocked, false);
            collection.Update(query, update);
        }

        public void ClearMongoSessionObject(MongoCollection<SessionObject> collection, string sessionID) {
            var query = Query<SessionObject>.EQ(x => x.BsonID, SessionObject.GetBsonId(this._applicationName, sessionID));
            var update = Update<SessionObject>
                .Set(x => x.IsLocked, false)
                .Set(y => y.SessionData, string.Empty);
            collection.Update(query, update);
        }

        public void ResetTimeoutMongoSessionObject(MongoCollection<SessionObject> collection, string sessionID, double timeoutInMinutes) {
            var query = Query<SessionObject>.EQ(x => x.BsonID, SessionObject.GetBsonId(this._applicationName, sessionID));
            var update = Update<SessionObject>.Set(x => x.ExpiresDate, DateTime.UtcNow.AddMinutes(timeoutInMinutes));
            collection.Update(query, update);
        }

        public SessionObject GetMongoSessionObject(MongoCollection<SessionObject> collection, string sessionID) {
            var query = Query<SessionObject>.EQ(x => x.BsonID, SessionObject.GetBsonId(this._applicationName, sessionID));
            return collection.Find(query).FirstOrDefault();
        }

        public void EnsureTTLIndex(MongoCollection<SessionObject> collection, double timeoutInMinutes) {
            var options = IndexOptions.SetTimeToLive(TimeSpan.FromMinutes(timeoutInMinutes));
            collection.EnsureIndex(new IndexKeysBuilder().Ascending("Expires"), options);
        }
    }
}
