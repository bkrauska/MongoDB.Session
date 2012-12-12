using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Session.Logging;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;

namespace MongoDB.Session {
    public class SessionStateProvider : SessionStateStoreProviderBase {
        private double _timeoutInMinutes = DefaultValues.TimeOutInMinutes;
        private string _applicationName = DefaultValues.ApplicationName;
        private bool _logSessionObjects = false;
        private bool _logEvents = false;

        private MongoSessionHelper _mongo = null;
        private Logger _logger = null;

        public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config) {
            if (config == null) {
                throw new ArgumentNullException("config");
            }

            if (!string.IsNullOrWhiteSpace(name)) {
                name = DefaultValues.Name;
            }

            if (string.IsNullOrWhiteSpace(config[ConfigKeys.Description])) {
                config.Remove(ConfigKeys.Description);
                config.Add(ConfigKeys.Description, DefaultValues.Description);
            }

            base.Initialize(name, config);

            if (ConfigurationManager.AppSettings[ConfigKeys.ApplicationName] != null) {
                this._applicationName = ConfigurationManager.AppSettings[ConfigKeys.ApplicationName];
            }

            var cfg = WebConfigurationManager.OpenWebConfiguration(System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath);
            var sessionStateConfig = (SessionStateSection)cfg.GetSection("system.web/sessionState");

            this._timeoutInMinutes = sessionStateConfig.Timeout.TotalMinutes;

            this._mongo = new MongoSessionHelper(this._applicationName, config);

            if (config[ConfigKeys.LogSessionItems] != null && config[ConfigKeys.LogSessionItems].ToUpper() == "TRUE") {
                this._logSessionObjects = true;
            }

            if (config[ConfigKeys.LogEvents] != null && config[ConfigKeys.LogEvents].ToUpper() == "TRUE") {
                this._logEvents = true;
            }

            if (this._logSessionObjects || this._logEvents) {
                this._logger = new Logger(this._applicationName, config);
            }
        }

        public override void InitializeRequest(System.Web.HttpContext context) {

        }

        public override void EndRequest(System.Web.HttpContext context) {

        }

        public override void Dispose() {

        }

        public override SessionStateStoreData GetItemExclusive(System.Web.HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions) {
            return GetItem(false, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItem(System.Web.HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions) {
            return GetItem(true, context, id, out locked, out lockAge, out lockId, out actions);
        }

        private SessionStateStoreData GetItem(bool exclusive, System.Web.HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions) {
            actions = 0;
            locked = false;
            lockId = 0;
            lockAge = TimeSpan.Zero;

            var collection = this._mongo.GetCollection();

            if (exclusive && !this._mongo.LockSession(collection, id)) {
                var previouslyLockedSession = this._mongo.GetMongoSessionObject(collection, id);

                if (previouslyLockedSession == null) {
                    lockId = previouslyLockedSession.LockID;
                    lockAge = DateTime.UtcNow - previouslyLockedSession.LockedDate;
                }

                this.LogEvent(id, context.Request.RawUrl, GetUsername(context.User), "Unable to obtain lock - " + lockAge);

                return null;
            }

            var sessionObject = this._mongo.GetMongoSessionObject(collection, id);

            if (sessionObject == null) {
                this.LogEvent(id, context.Request.RawUrl, GetUsername(context.User), "No session");

                return null;
            }

            if (sessionObject.ExpiresDate < DateTime.UtcNow) {
                this._mongo.ClearMongoSessionObject(collection, id);

                this.LogEvent(id, context.Request.RawUrl, GetUsername(context.User), "Session has expired");

                return null;
            }

            locked = sessionObject.IsLocked;
            if (locked) {
                lockAge = DateTime.UtcNow - sessionObject.LockedDate;
                lockId = sessionObject.LockID;

                this.LogEvent(id, context.Request.RawUrl, GetUsername(context.User), "Obtained lock on session - " + lockId);
            }

            actions = (SessionStateActions)sessionObject.Flags;
            sessionObject.Flags = 0;

            collection.Save(sessionObject);

            if (actions == SessionStateActions.InitializeItem) {
                return CreateNewStoreData(context, (int)this._timeoutInMinutes);
            }

            return Deserialize(context, sessionObject.SessionData, sessionObject.Timeout);
        }

        public override void SetAndReleaseItemExclusive(System.Web.HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem) {
            var sessItems = Serialize((SessionStateItemCollection)item.Items);

            var collection = this._mongo.GetCollection();
            this._mongo.EnsureTTLIndex(collection, this._timeoutInMinutes);

            this.LogSessionObjects(item.Items, id, context.Request.RawUrl, GetUsername(context.User));

            if (newItem) {
                var newSession = new SessionObject() {
                    SessionID = id,
                    ApplicationName = this._applicationName,
                    Username = this.GetUsername(context.User),
                    SessionData = sessItems,
                    Timeout = (int)this._timeoutInMinutes,
                    Flags = 0,
                    CreatedDate = DateTime.UtcNow,
                    ExpiresDate = DateTime.UtcNow.AddMinutes(this._timeoutInMinutes),
                    IsLocked = false,
                    LockID = (int)lockId,
                    LockedDate = DateTime.UtcNow
                };

                collection.Save(newSession);

                this.LogEvent(id, context.Request.RawUrl, GetUsername(context.User), "Insert new session");
            }
            else {
                var query = Query.And(
                    Query<SessionObject>.EQ(x => x.BsonID, SessionObject.GetBsonId(this._applicationName, id)),
                    Query<SessionObject>.EQ(y => y.LockID, lockId)
                );
                var update = Update<SessionObject>
                    .Set(x => x.SessionData, sessItems)
                    .Set(y => y.IsLocked, false)
                    .Set(z => z.Username, this.GetUsername(context.User));

                collection.Update(query, update);

                this.LogEvent(id, context.Request.RawUrl, GetUsername(context.User), "Set items and unlock session - " + lockId);
            }
        }

        public override void ReleaseItemExclusive(System.Web.HttpContext context, string id, object lockId) {
            var collection = this._mongo.GetCollection();
            this._mongo.UnlockMongoSessionObject(collection, id, (int)lockId);

            this.LogEvent(id, context.Request.RawUrl, GetUsername(context.User), "Release session item exclusive - " + lockId);
        }

        public override void RemoveItem(System.Web.HttpContext context, string id, object lockId, SessionStateStoreData item) {
            var collection = this._mongo.GetCollection();
            this._mongo.ClearMongoSessionObject(collection, id);

            this.LogEvent(id, context.Request.RawUrl, GetUsername(context.User), "Remove session by lock id - " + lockId);
        }

        public override void CreateUninitializedItem(System.Web.HttpContext context, string id, int timeout) {
            var collection = this._mongo.GetCollection();
            this._mongo.EnsureTTLIndex(collection, this._timeoutInMinutes);

            var newSession = new SessionObject() {
                SessionID = id,
                ApplicationName = this._applicationName,
                Username = this.GetUsername(context.User),
                SessionData = string.Empty,
                Timeout = (int)this._timeoutInMinutes,
                Flags = 0,
                CreatedDate = DateTime.UtcNow,
                ExpiresDate = DateTime.UtcNow.AddMinutes(this._timeoutInMinutes),
                IsLocked = false,
                LockID = (int)0,
                LockedDate = DateTime.UtcNow
            };

            collection.Save(newSession);

            this.LogEvent(id, context.Request.RawUrl, GetUsername(context.User), "Create uninitialized session");
        }

        public override SessionStateStoreData CreateNewStoreData(System.Web.HttpContext context, int timeout) {
            return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback) {
            return false;
        }

        public override void ResetItemTimeout(System.Web.HttpContext context, string id) {
            var collection = this._mongo.GetCollection();

            this._mongo.ResetTimeoutMongoSessionObject(collection, id, this._timeoutInMinutes);
        }

        private string Serialize(SessionStateItemCollection items) {
            using (var ms = new MemoryStream()) {
                using (var writer = new BinaryWriter(ms)) {
                    if (items != null) {
                        items.Serialize(writer);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private SessionStateStoreData Deserialize(HttpContext context, string serializedItems, int timeout) {
            using (var ms = new MemoryStream(Convert.FromBase64String(serializedItems))) {
                var sessionItems = new SessionStateItemCollection();

                if (ms.Length > 0) {
                    using (var reader = new BinaryReader(ms)) {
                        sessionItems = SessionStateItemCollection.Deserialize(reader);
                    }
                }

                return new SessionStateStoreData(sessionItems, SessionStateUtility.GetSessionStaticObjects(context), timeout);
            }
        }

        private void LogSessionObjects(ISessionStateItemCollection sessionCollection, string sessionID, string url, string username) {
            if (this._logSessionObjects && this._logger != null) {
                this._logger.LogSessionObjects(sessionCollection, sessionID, url, username);
            }
        }

        private void LogEvent(string sessionID, string url, string username, string eventDescription) {
            if (this._logEvents && this._logger != null) {
                this._logger.LogEvent(sessionID, url, username, eventDescription);   
            }
        }

        private string GetUsername(System.Security.Principal.IPrincipal user) {
            if (user == null || user.Identity == null) {
                return string.Empty;
            }

            return user.Identity.Name;
        }
    }
}