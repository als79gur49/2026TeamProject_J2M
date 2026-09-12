using System;
using System.Collections.Generic;

namespace Game.Platform.Steam.Tests.EditMode
{
    internal sealed class FakeSteamNativeApi : ISteamNativeApi
    {
        internal bool PacksizeCompatible { get; set; } = true;
        internal bool InitializeResult { get; set; } = true;
        internal uint AppId { get; set; } = 480;
        internal bool SteamIdValid { get; set; } = true;
        internal bool LoggedOn { get; set; } = true;
        internal Exception PacksizeException { get; set; }
        internal Exception InitializeException { get; set; }
        internal Exception AppIdException { get; set; }
        internal Exception SteamIdValidException { get; set; }
        internal Exception LoggedOnException { get; set; }
        internal Exception CallbackException { get; set; }
        internal Exception ShutdownException { get; set; }
        internal Action CallbackAction { get; set; }
        internal List<string> CallOrder { get; set; }

        internal int PacksizeCount { get; private set; }
        internal int InitializeCount { get; private set; }
        internal int CallbackCount { get; private set; }
        internal int ShutdownCount { get; private set; }
        internal int AppIdCount { get; private set; }
        internal int IdentityCount { get; private set; }
        internal int LoggedOnCount { get; private set; }

        public bool IsPacksizeCompatible()
        {
            PacksizeCount++;
            if (PacksizeException != null)
            {
                throw PacksizeException;
            }

            return PacksizeCompatible;
        }

        public bool Initialize()
        {
            InitializeCount++;
            if (InitializeException != null)
            {
                throw InitializeException;
            }

            return InitializeResult;
        }

        public void RunCallbacks()
        {
            CallbackCount++;
            CallOrder?.Add("run-callbacks");
            if (CallbackException != null)
            {
                throw CallbackException;
            }

            CallbackAction?.Invoke();
        }

        public void Shutdown()
        {
            ShutdownCount++;
            CallOrder?.Add("native-shutdown");
            if (ShutdownException != null)
            {
                throw ShutdownException;
            }
        }

        public uint GetAppId()
        {
            AppIdCount++;
            if (AppIdException != null)
            {
                throw AppIdException;
            }

            return AppId;
        }

        public bool IsSteamIdValid()
        {
            IdentityCount++;
            if (SteamIdValidException != null)
            {
                throw SteamIdValidException;
            }

            return SteamIdValid;
        }

        public bool IsLoggedOn()
        {
            LoggedOnCount++;
            if (LoggedOnException != null)
            {
                throw LoggedOnException;
            }

            return LoggedOn;
        }
    }
}
