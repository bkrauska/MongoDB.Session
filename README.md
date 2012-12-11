MongoDB.Session
===============

.Net MongoDB Custom Session State Provider

## Configuration
To use this session state provider add the following to your application's web.config.

    <system.web>
        <sessionState mode="Custom" customProvider="MongoDB.Session.SessionStateProvider" timeout="21">
            <providers>
                <add name="MongoDB.Session.SessionStateProvider" 
                     type="MongoDB.Session.SessionStateProvider" 
                     connectionString="mongodb://localhost:27017/?safe=true" 
                     dbName="SessionState" 
                     collectionName="Session" />
            </providers>
        </sessionState>
    </system.web>