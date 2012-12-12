namespace MongoDB.Session {
    internal static class DefaultValues {
        public const double TimeOutInMinutes = 21;
        public const string ApplicationName = "AppName";
        public const string ConnectionString = "mongodb://localhost:27017/?safe=true";
        public const string DbName = "SessionState";
        public const string CollectionName = "Session";
        public const string Name = "MongoSessionStateStore";
        public const string Description = "MongoDB Session State Store provider";
    }
}
