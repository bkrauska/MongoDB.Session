using MongoDB.Bson.Serialization.Attributes;
using System;

namespace MongoDB.Session.Logging {
    internal class SessionObject {
        [BsonId(IdGenerator = typeof(MongoDB.Bson.Serialization.IdGenerators.CombGuidGenerator))]
        public Guid Id { get; set; }
        [BsonElement("app")]
        public string ApplicationName { get; set; }
        [BsonElement("s_id")]
        public string SessionId { get; set; }
        [BsonElement("u")]
        public string Url { get; set; }
        [BsonElement("k")]
        public string Key { get; set; }
        [BsonElement("s_kb")]
        public double ObjectSize { get; set; }
        [BsonElement("user")]
        public string User { get; set; }
        [BsonElement("c_date")]
        public DateTime CreatedDate { get; set; }
    }
}
