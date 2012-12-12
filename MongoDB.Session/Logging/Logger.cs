using MongoDB.Driver;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web.SessionState;

namespace MongoDB.Session.Logging {
	internal class Logger {
		public delegate void LogEventDelegate(string id, string pageURL, string user, string action);
		public delegate void LogSessionObjectsDelegate(ISessionStateItemCollection item, string id, string pageURL, string user);

        private readonly MongoClient _client;
		private readonly string _databaseName = DefaultValues.DbName + ".Log";
		private readonly string _applicationName;

        private static class CollectionNames {
            public const string SessionObjectLog = "SessionObjectLog";
            public const string EventLog = "EventLog";
        }

        public Logger(string applicationName, NameValueCollection config) {
			this._applicationName = applicationName;

            var connectionString = DefaultValues.ConnectionString;
            if (config[ConfigKeys.ConnectionString] != null) {
                connectionString = config[ConfigKeys.ConnectionString];
            }

            this._client = new MongoClient(connectionString);
            
            if (config[ConfigKeys.DbName] != null) {
                this._databaseName = config[ConfigKeys.DbName] + ".Log";
            }
		}

        public void LogEvent(string sessionId, string url, string user, string eventDescription) {
            var asyncCall = new LogEventDelegate(AsyncLogEvent);
            asyncCall.BeginInvoke(sessionId, url, user, eventDescription, null, null);
        }

        public void LogSessionObjects(ISessionStateItemCollection item, string sessionId, string url, string user) {
            var asyncCall = new LogSessionObjectsDelegate(AsyncLogSessionObjects);
            asyncCall.BeginInvoke(item, sessionId, url, user, null, null);
        }
        
        private MongoCollection<SessionObject> GetSessionObjectLogCollection() {
            return this.GetCollection<SessionObject>(CollectionNames.SessionObjectLog);
		}

        private MongoCollection<Event> GetEventLogCollection() {
            return this.GetCollection<Event>(CollectionNames.EventLog);
		}

		private MongoCollection<T> GetCollection<T>(string collectionName) {
            var server = this._client.GetServer();
			var database = server.GetDatabase(this._databaseName);
            return database.GetCollection<T>(collectionName);
		}

        private byte[] Serialize(Object item) {
            using (var ms = new MemoryStream()) {
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, item);
                return ms.ToArray();
            }
        }

		private void AsyncLogEvent(string sessionId, string url, string user, string eventDescription) {
            var unixRef = new DateTime(1970, 1, 1, 0, 0, 0);
            var tick = (DateTime.UtcNow.Ticks - unixRef.Ticks) / 10000000;

            var sessionLogCollection = this.GetEventLogCollection();

            var sspEvent = new Event() {
                ApplicationName = this._applicationName,
                SessionId = sessionId,
                Url = url,
                EventDescription = eventDescription,
                User = user,
                CreatedDate = DateTime.UtcNow
            };

            sessionLogCollection.Insert(sspEvent);
        }

		private void AsyncLogSessionObjects(ISessionStateItemCollection item, string sessionId, string url, string user) {
			var sessionLogCollection = this.GetSessionObjectLogCollection();

			for (int i = 0; i < item.Count; i++) {
				if (item[i] != null) {
					var sessionKey = item.Keys[i];
					var objectSize = this.Serialize(item[i]).Length / 1024.0;

                    var logObject = new SessionObject() {
                        ApplicationName = this._applicationName,
                        SessionId = sessionId,
                        Url = url,
                        Key = sessionKey,
                        ObjectSize = objectSize,
                        User = user,
                        CreatedDate = DateTime.UtcNow
                    };

                    sessionLogCollection.Insert(logObject);
				}
			}
		}
	}
}
