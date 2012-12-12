using MongoDB.Bson.Serialization.Attributes;
using System;

namespace MongoDB.Session {
    internal class SessionObject {
        [BsonId]
        public string BsonID { get { return GetBsonId(this.ApplicationName, this.SessionID); } set { } }
        [BsonElement("s_id")]
        public string SessionID { get; set; }
        [BsonElement("app")]
        public string ApplicationName { get; set; }
        [BsonElement("user")]
        public string Username { get; set; }
        [BsonElement("data")]
        public string SessionData { get; set; }
        [BsonElement("t")]
        public int Timeout { get; set; }
        [BsonElement("f")]
        public int Flags { get; set; }
        [BsonElement("c_date")]
        public DateTime CreatedDate { get; set; }
        [BsonElement("e_date")]
        public DateTime ExpiresDate { get; set; }
        [BsonElement("l")]
        public bool IsLocked { get; set; }
        [BsonElement("l_id")]
        public int LockID { get; set; }
        [BsonElement("l_date")]
        public DateTime LockedDate { get; set; }

        public static string GetBsonId(string applicationName, string sessionID) {
            return string.Format("{0}.{1}", applicationName, sessionID);
        }
    }
}
