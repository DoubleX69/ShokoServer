﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using AniDBAPI;
using AniDBAPI.Commands;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Providers.AniDB
{
    public class AniDBHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // we use this lock to make don't try and access AniDB too much (UDP and HTTP)
        private readonly object lockAniDBConnections = new object();

        private IPEndPoint localIpEndPoint;
        private IPEndPoint remoteIpEndPoint;
        private Socket soUdp;
        private string curSessionID = string.Empty;

        private string userName = string.Empty;
        private string password = string.Empty;
        private string serverName = string.Empty;
        private string serverPort = string.Empty;
        private string clientPort = string.Empty;

        private Timer logoutTimer;

        private Timer httpBanResetTimer;
        private Timer udpBanResetTimer;

        public DateTime? HttpBanTime { get; set; }
        public DateTime? UdpBanTime { get; set; }

        private bool _isHttpBanned;
        private bool _isUdpBanned;

        public bool IsHttpBanned
        {
            get => _isHttpBanned;
            set
            {
                _isHttpBanned = value;
                HttpBanTime = DateTime.Now;
                if (value)
                {
                    ServerInfo.Instance.IsBanned = true;
                    ServerInfo.Instance.BanOrigin = @"HTTP";
                    ServerInfo.Instance.BanReason = HttpBanTime.ToString();
                    httpBanResetTimer.Start();
                }
                else if (!IsUdpBanned)
                {
                    ServerInfo.Instance.IsBanned = false;
                    ServerInfo.Instance.BanOrigin = string.Empty;
                    ServerInfo.Instance.BanReason = string.Empty;
                }
            }
        }

        public bool IsUdpBanned
        {
            get => _isUdpBanned;
            set
            {
                _isUdpBanned = value;
                UdpBanTime = DateTime.Now;
                if (value)
                {
                    ServerInfo.Instance.IsBanned = true;
                    ServerInfo.Instance.BanOrigin = @"UDP";
                    ServerInfo.Instance.BanReason = UdpBanTime.ToString();
                    udpBanResetTimer.Start();
                }
                else if (!IsHttpBanned)
                {
                    ServerInfo.Instance.IsBanned = false;
                    ServerInfo.Instance.BanOrigin = string.Empty;
                    ServerInfo.Instance.BanReason = string.Empty;
                }
            }
        }

        private bool isInvalidSession;

        public bool IsInvalidSession
        {
            get => isInvalidSession;

            set
            {
                isInvalidSession = value;
                ServerInfo.Instance.IsInvalidSession = isInvalidSession;
            }
        }

        private bool isLoggedOn;

        public bool IsLoggedOn
        {
            get => isLoggedOn;
            set => isLoggedOn = value;
        }

        public bool WaitingOnResponse { get; set; }

        public DateTime? WaitingOnResponseTime { get; set; }

        public int? ExtendPauseSecs { get; set; }

        public bool IsNetworkAvailable { private set; get; }

        public string ExtendPauseReason { get; set; } = string.Empty;

        public static event EventHandler LoginFailed;

        public void ExtendPause(int secsToPause, string pauseReason)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            ExtendPauseSecs = secsToPause;
            ExtendPauseReason = pauseReason;
            ServerInfo.Instance.ExtendedPauseString = string.Format(Resources.AniDB_Paused,
                secsToPause,
                pauseReason);
            ServerInfo.Instance.HasExtendedPause = true;
        }

        public void ResetExtendPause()
        {
            ExtendPauseSecs = null;
            ExtendPauseReason = string.Empty;
            ServerInfo.Instance.ExtendedPauseString = string.Empty;
            ServerInfo.Instance.HasExtendedPause = false;
        }

        public void Init(string userName, string password, string serverName, string serverPort, string clientPort)
        {
            soUdp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            this.userName = userName;
            this.password = password;
            this.serverName = serverName;
            this.serverPort = serverPort;
            this.clientPort = clientPort;

            isLoggedOn = false;

            if (!BindToLocalPort()) IsNetworkAvailable = false;
            if (!BindToRemotePort()) IsNetworkAvailable = false;

            logoutTimer = new Timer();
            logoutTimer.Elapsed += LogoutTimer_Elapsed;
            logoutTimer.Interval = 5000; // Set the Interval to 5 seconds.
            logoutTimer.Enabled = true;
            logoutTimer.AutoReset = true;

            logger.Info("starting logout timer...");
            logoutTimer.Start();

            httpBanResetTimer = new Timer();
            httpBanResetTimer.Elapsed += HTTPBanResetTimerElapsed;
            httpBanResetTimer.Interval = TimeSpan.FromHours(12).TotalMilliseconds;

            udpBanResetTimer = new Timer();
            udpBanResetTimer.Elapsed += UDPBanResetTimerElapsed;
            udpBanResetTimer.Interval = TimeSpan.FromHours(12).TotalMilliseconds;
        }

        public void Dispose()
        {
            logger.Info("ANIDBLIB DISPOSING...");

            CloseConnections();
        }

        public void CloseConnections()
        {
            logoutTimer?.Stop();
            logoutTimer = null;
            if (soUdp == null) return;
            try{
                soUdp.Shutdown(SocketShutdown.Both);
                if (soUdp.Connected) {
                    soUdp.Disconnect(false);
                }
            }
            catch (SocketException ex) {
                logger.Error(ex.ToString(), $"Failed to Shutdown and Disconnect the connection to AniDB: {0}");
            }
            finally {
                logger.Info("CLOSING ANIDB CONNECTION...");
                soUdp.Close();
                logger.Info("CLOSED ANIDB CONNECTION");
                soUdp = null;
            }
        }

        void LogoutTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeSpan tsAniDBUDPTemp = DateTime.Now - ShokoService.LastAniDBUDPMessage;
            if (ExtendPauseSecs.HasValue && tsAniDBUDPTemp.TotalSeconds >= ExtendPauseSecs.Value)
                ResetExtendPause();

            if (!isLoggedOn) return;

            // don't ping when anidb is taking a long time to respond
            if (WaitingOnResponse)
            {
                try
                {
                    if (WaitingOnResponseTime.HasValue)
                    {
                        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                        TimeSpan ts = DateTime.Now - WaitingOnResponseTime.Value;
                        ServerInfo.Instance.WaitingOnResponseAniDBUDPString =
                            string.Format(Resources.AniDB_ResponseWaitSeconds,
                                ts.TotalSeconds);
                    }
                }
                catch
                {
                    //IGNORE
                }
                return;
            }

            lock (lockAniDBConnections)
            {
                TimeSpan tsAniDBNonPing = DateTime.Now - ShokoService.LastAniDBMessageNonPing;
                TimeSpan tsPing = DateTime.Now - ShokoService.LastAniDBPing;
                TimeSpan tsAniDBUDP = DateTime.Now - ShokoService.LastAniDBUDPMessage;

                // if we haven't sent a command for 45 seconds, send a ping just to keep the connection alive
                if (tsAniDBUDP.TotalSeconds >= Constants.PingFrequency &&
                    tsPing.TotalSeconds >= Constants.PingFrequency &&
                    !IsUdpBanned && !ExtendPauseSecs.HasValue)
                {
                    AniDBCommand_Ping ping = new AniDBCommand_Ping();
                    ping.Init();
                    ping.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                }

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                string msg = string.Format(Resources.AniDB_LastMessage,
                    tsAniDBUDP.TotalSeconds);

                if (tsAniDBNonPing.TotalSeconds > Constants.ForceLogoutPeriod) // after 10 minutes
                {
                    ForceLogout();
                }
            }
        }

        private void HTTPBanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            IsHttpBanned = false;
        }

        private void UDPBanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            IsUdpBanned = false;
        }

        private void SetWaitingOnResponse(bool isWaiting)
        {
            WaitingOnResponse = isWaiting;
            ServerInfo.Instance.WaitingOnResponseAniDBUDP = isWaiting;

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            if (isWaiting)
                ServerInfo.Instance.WaitingOnResponseAniDBUDPString =
                    Resources.AniDB_ResponseWait;
            else
                ServerInfo.Instance.WaitingOnResponseAniDBUDPString = Resources.Command_Idle;

            if (isWaiting)
                WaitingOnResponseTime = DateTime.Now;
            else
                WaitingOnResponseTime = null;
        }

        public bool Login()
        {
            // check if we are already logged in
            if (isLoggedOn) return true;

            if (!ValidAniDBCredentials()) return false;

            AniDBCommand_Login login = new AniDBCommand_Login();
            login.Init(userName, password);

            string msg = login.commandText.Replace(userName, "******");
            msg = msg.Replace(password, "******");
            logger.Trace("udp command: {0}", msg);
            SetWaitingOnResponse(true);
            enHelperActivityType ev = login.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                new UnicodeEncoding(true, false));
            SetWaitingOnResponse(false);

            if (login.errorOccurred)
                logger.Trace("error in login: {0}", login.errorMessage);
            //else
            //  logger.Info("socketResponse: {0}", login.socketResponse);

            Thread.Sleep(2200);

            switch (ev)
            {
                case enHelperActivityType.LoginFailed:
                    logger.Error("AniDB Login Failed: invalid credentials");
                    LoginFailed?.Invoke(this, null);
                    break;
                case enHelperActivityType.LoggedIn:
                    curSessionID = login.SessionID;
                    isLoggedOn = true;
                    IsInvalidSession = false;
                    return true;
                default:
                    logger.Error($"AniDB Login Failed: error connecting to AniDB: {login.errorMessage}");
                    break;
            }

            return false;
        }

        public void ForceLogout()
        {
            if (isLoggedOn)
            {
                AniDBCommand_Logout logout = new AniDBCommand_Logout();
                logout.Init();
                //logger.Info("udp command: {0}", logout.commandText);
                SetWaitingOnResponse(true);
                logout.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                //logger.Info("socketResponse: {0}", logout.socketResponse);
                isLoggedOn = false;
            }
        }

        public Raw_AniDB_File GetFileInfo(IHash vidLocal)
        {
            if (!Login()) return null;

            enHelperActivityType ev = enHelperActivityType.NoSuchFile;
            AniDBCommand_GetFileInfo getInfoCmd = null;

            lock (lockAniDBConnections)
            {
                getInfoCmd = new AniDBCommand_GetFileInfo();
                getInfoCmd.Init(vidLocal, true);
                SetWaitingOnResponse(true);
                ev = getInfoCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev == enHelperActivityType.GotFileInfo && getInfoCmd.fileInfo != null)
            {
                try
                {
                    logger.Trace("ProcessResult_GetFileInfo: {0}", getInfoCmd.fileInfo);

                    if (ServerSettings.AniDB_DownloadReleaseGroups)
                    {
                        CommandRequest_GetReleaseGroup cmdRelgrp =
                            new CommandRequest_GetReleaseGroup(getInfoCmd.fileInfo.GroupID, false);
                        cmdRelgrp.Save();
                    }


                    return getInfoCmd.fileInfo;
                }
                catch (Exception ex)
                {
                    logger.Error(ex.ToString());
                    return null;
                }
            }

            return null;
        }

        public void GetMyListFileStatus(int aniDBFileID)
        {
            if (!ServerSettings.AniDB_MyList_ReadWatched) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                AniDBCommand_GetMyListFileInfo cmdGetFileStatus = new AniDBCommand_GetMyListFileInfo();
                cmdGetFileStatus.Init(aniDBFileID);
                SetWaitingOnResponse(true);
                enHelperActivityType ev = cmdGetFileStatus.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                switch (ev)
                {
                        case enHelperActivityType.Banned_555:
                            logger.Error("Recieved ban on trying to get MyList stats for file");
                            return;
                        // Ignore no info in MyList for file
                        case enHelperActivityType.NoSuchMyListFile: return;
                        case enHelperActivityType.LoginRequired:
                            logger.Error("Not logged in to AniDB");
                            return;
                }
                if (cmdGetFileStatus.MyListFile?.WatchedDate == null) return;
                var aniFile = Repo.AniDB_File.GetByFileID(aniDBFileID);
                var vids = aniFile.EpisodeIDs.SelectMany(a => Repo.VideoLocal.GetByAniDBEpisodeID(a)).Where(a => a != null).ToList();
                foreach (var vid in vids)
                {
                    foreach (var user in Repo.JMMUser.GetAniDBUsers())
                    {
                        vid.ToggleWatchedStatus(true, false, cmdGetFileStatus.MyListFile.WatchedDate, true,
                            user.JMMUserID, false, true);
                    }
                }
            }
        }

        public void UpdateMyListStats()
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                AniDBCommand_GetMyListStats cmdGetMylistStats = new AniDBCommand_GetMyListStats();
                cmdGetMylistStats.Init();
                SetWaitingOnResponse(true);
                enHelperActivityType ev = cmdGetMylistStats.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                if (ev == enHelperActivityType.GotMyListStats && cmdGetMylistStats.MyListStats != null)
                {
                    using (var upd = Repo.AniDB_MylistStats.BeginAddOrUpdate(() => Repo.AniDB_MylistStats.GetAll().FirstOrDefault()))
                    {
                        upd.Entity.Populate_RA(cmdGetMylistStats.MyListStats);
                        upd.Commit();
                    }
                }
            }
        }

        public void GetUpdated(ref List<int> updatedAnimeIDs, ref long startTime)
        {
            //startTime = 0;
            updatedAnimeIDs = new List<int>();

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                AniDBCommand_GetUpdated cmdUpdated = new AniDBCommand_GetUpdated();
                cmdUpdated.Init(startTime.ToString());
                SetWaitingOnResponse(true);
                enHelperActivityType ev = cmdUpdated.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);

                if (ev == enHelperActivityType.GotUpdated && cmdUpdated.RecordCount > 0)
                {
                    startTime = long.Parse(cmdUpdated.StartTime);
                    updatedAnimeIDs = cmdUpdated.AnimeIDList;
                }
            }
        }

        /// <summary>
        /// This is for generic files (manually linked)
        /// </summary>
        /// <param name="animeID"></param>
        /// <param name="episodeNumber"></param>
        /// <param name="watched"></param>
        public void UpdateMyListFileStatus(IHash hash, bool watched, DateTime? watchedDate = null)
        {
            if (!ServerSettings.AniDB_MyList_AddFiles) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                AniDBCommand_UpdateFile cmdUpdateFile = new AniDBCommand_UpdateFile();
                if (watched && watchedDate == null) watchedDate = DateTime.Now;

                enHelperActivityType ev;
                if (hash.MyListID > 0)
                {
                    cmdUpdateFile.Init(hash, watched, watchedDate);
                    SetWaitingOnResponse(true);
                    ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                        new UnicodeEncoding(true, false));
                    SetWaitingOnResponse(false);
                }
                else
                {
                    logger.Trace($"File has no MyListID, attempting to add: {hash.ED2KHash}");
                    ev = enHelperActivityType.NoSuchMyListFile;
                }

                if (ev == enHelperActivityType.NoSuchMyListFile)
                {
                    // Run sychronously, but still do all of the stuff with watched state settings
                    CommandRequest_AddFileToMyList addcmd = new CommandRequest_AddFileToMyList(hash.ED2KHash);
                    // Initialize private parts
                    addcmd.LoadFromDBCommand(addcmd.ToDatabaseObject());
                    addcmd.ProcessCommand();
                }
            }
        }

        public (int?, bool?) AddFileToMyList(IHash fileDataLocal, ref DateTime? watchedDate, ref AniDBFile_State? state)
        {
            if (!ServerSettings.AniDB_MyList_AddFiles) return (null, false);

            if (!Login()) return (null, false);

            enHelperActivityType ev;
            AniDBCommand_AddFile cmdAddFile;

            lock (lockAniDBConnections)
            {
                cmdAddFile = new AniDBCommand_AddFile();
                cmdAddFile.Init(fileDataLocal, ServerSettings.AniDB_MyList_StorageState);
                SetWaitingOnResponse(true);
                ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            // if the user already has this file on
            if (ev == enHelperActivityType.FileAlreadyExists && cmdAddFile.FileData != null)
            {
                watchedDate = cmdAddFile.WatchedDate;
                state = cmdAddFile.State;
                return (cmdAddFile.MyListID, cmdAddFile.ReturnIsWatched);
            }

            if (cmdAddFile.MyListID > 0) return (cmdAddFile.MyListID, false);

            return (null, false);
        }

        public (int?, bool?) AddFileToMyList(int animeID, int episodeNumber, ref DateTime? watchedDate)
        {
            if (!Login()) return (null, null);

            enHelperActivityType ev;
            AniDBCommand_AddFile cmdAddFile;

            lock (lockAniDBConnections)
            {
                cmdAddFile = new AniDBCommand_AddFile();
                cmdAddFile.Init(animeID, episodeNumber, ServerSettings.AniDB_MyList_StorageState);
                SetWaitingOnResponse(true);
                ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            // if the user already has this file on
            if (ev == enHelperActivityType.FileAlreadyExists && cmdAddFile.FileData != null && ServerSettings.AniDB_MyList_ReadWatched)
            {
                watchedDate = cmdAddFile.WatchedDate;
                return (cmdAddFile.MyListID, cmdAddFile.ReturnIsWatched);
            }
            if (ServerSettings.AniDB_MyList_ReadUnwatched) return (cmdAddFile.MyListID, false);

            if (cmdAddFile.MyListID > 0)
                return (cmdAddFile.MyListID, null);

            return (null, null);
        }

        internal void MarkFileAsRemote(int myListID)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdMarkFileExternal = new AniDBCommand_MarkFileAsRemote();
                cmdMarkFileExternal.Init(myListID);
                SetWaitingOnResponse(true);
                cmdMarkFileExternal.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        internal void MarkFileAsOnDisk(IHash hash)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdMarkFileDisk = new AniDBCommand_MarkFileAsDisk();
                cmdMarkFileDisk.Init(hash.MyListID);
                SetWaitingOnResponse(true);
                cmdMarkFileDisk.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        internal void MarkFileAsOnDisk(int myListID)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdMarkFileDisk = new AniDBCommand_MarkFileAsDisk();
                cmdMarkFileDisk.Init(myListID);
                SetWaitingOnResponse(true);
                cmdMarkFileDisk.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void MarkFileAsUnknown(IHash hash)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdMarkFileUnknown = new AniDBCommand_MarkFileAsUnknown();
                cmdMarkFileUnknown.Init(hash.MyListID);
                SetWaitingOnResponse(true);
                cmdMarkFileUnknown.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void MarkFileAsUnknown(int myListID)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdMarkFileUnknown = new AniDBCommand_MarkFileAsUnknown();
                cmdMarkFileUnknown.Init(myListID);
                SetWaitingOnResponse(true);
                cmdMarkFileUnknown.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void MarkFileAsDeleted(IHash hash)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdDelFile = new AniDBCommand_MarkFileAsDeleted();
                cmdDelFile.Init(hash.MyListID);
                SetWaitingOnResponse(true);
                cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void MarkFileAsDeleted(int myListID)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdDelFile = new AniDBCommand_MarkFileAsDeleted();
                cmdDelFile.Init(myListID);
                SetWaitingOnResponse(true);
                cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void DeleteFileFromMyList(string hash, long fileSize)
        {
            if (!ServerSettings.AniDB_MyList_AddFiles) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdDelFile = new AniDBCommand_DeleteFile();
                cmdDelFile.Init(hash, fileSize);
                SetWaitingOnResponse(true);
                cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void DeleteFileFromMyList(int fileID)
        {
            if (!ServerSettings.AniDB_MyList_AddFiles) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdDelFile = new AniDBCommand_DeleteFile();
                cmdDelFile.Init(fileID);
                SetWaitingOnResponse(true);
                cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public SVR_AniDB_Anime GetAnimeInfoUDP(int animeID, bool forceRefresh)
        {
            SVR_AniDB_Anime anime = null;

            bool skip = true;
            if (forceRefresh)
                skip = false;
            else
            {
                anime = Repo.AniDB_Anime.GetByID(animeID);
                if (anime == null) skip = false;
            }

            //TODO: Skip AND null Anime? or Skip OR null Anime?
            if (skip)
                return anime;

            if (!Login()) return null;

            enHelperActivityType ev = enHelperActivityType.NoSuchAnime;
            AniDBCommand_GetAnimeInfo getAnimeCmd = null;

            lock (lockAniDBConnections)
            {
                getAnimeCmd = new AniDBCommand_GetAnimeInfo();
                getAnimeCmd.Init(animeID, forceRefresh);
                SetWaitingOnResponse(true);
                ev = getAnimeCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev == enHelperActivityType.GotAnimeInfo && getAnimeCmd.AnimeInfo != null)
            {
                // check for an existing record so we don't over write the description
                anime=SVR_AniDB_Anime.PopulateAndSaveFromUDP(getAnimeCmd.AnimeInfo);
            }

            return anime;
        }

        public Raw_AniDB_Character GetCharacterInfoUDP(int charID)
        {
            if (!Login()) return null;

            enHelperActivityType ev = enHelperActivityType.NoSuchChar;
            AniDBCommand_GetCharacterInfo getCharCmd = null;
            lock (lockAniDBConnections)
            {
                getCharCmd = new AniDBCommand_GetCharacterInfo();
                getCharCmd.Init(charID, true);
                SetWaitingOnResponse(true);
                ev = getCharCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
            if (ev == enHelperActivityType.GotCharInfo && getCharCmd.CharInfo != null)
            {
                return getCharCmd.CharInfo;
            }

            return null;
        }

        public void GetReleaseGroupUDP(int groupID)
        {
            if (!Login()) return;

            enHelperActivityType ev;
            AniDBCommand_GetGroup getCmd;
            lock (lockAniDBConnections)
            {
                getCmd = new AniDBCommand_GetGroup();
                getCmd.Init(groupID);
                SetWaitingOnResponse(true);
                ev = getCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev != enHelperActivityType.GotGroup || getCmd.Group == null) return;
            var relGroup = Repo.AniDB_ReleaseGroup.GetByID(groupID) ?? new AniDB_ReleaseGroup();

            relGroup.Populate_RA(getCmd.Group);
            Repo.AniDB_ReleaseGroup.Save(relGroup);
        }

        public GroupStatusCollection GetReleaseGroupStatusUDP(int animeID)
        {
            if (!Login()) return null;

            enHelperActivityType ev;
            AniDBCommand_GetGroupStatus getCmd;
            lock (lockAniDBConnections)
            {
                getCmd = new AniDBCommand_GetGroupStatus();
                getCmd.Init(animeID);
                SetWaitingOnResponse(true);
                ev = getCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev != enHelperActivityType.GotGroupStatus || getCmd.GrpStatusCollection == null)
                return getCmd.GrpStatusCollection;

            // delete existing records
            Repo.AniDB_GroupStatus.DeleteForAnime(animeID);

            // save the records
            foreach (Raw_AniDB_GroupStatus raw in getCmd.GrpStatusCollection.Groups)
            {
                AniDB_GroupStatus grpstat = new AniDB_GroupStatus();
                grpstat.Populate_RA(raw);
                Repo.AniDB_GroupStatus.Save(grpstat);
            }

            if (getCmd.GrpStatusCollection.LatestEpisodeNumber > 0)
            {
                // update the anime with a record of the latest subbed episode
                SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByID(animeID);
                if (anime != null)
                {
                    anime.LatestEpisodeNumber = getCmd.GrpStatusCollection.LatestEpisodeNumber;
                    Repo.AniDB_Anime.Save(anime);

                    // check if we have this episode in the database
                    // if not get it now by updating the anime record
                    List<AniDB_Episode> eps = Repo.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(animeID,
                        getCmd.GrpStatusCollection.LatestEpisodeNumber);
                    if (eps.Count == 0)
                    {
                        CommandRequest_GetAnimeHTTP cr_anime =
                            new CommandRequest_GetAnimeHTTP(animeID, true, false, 0);
                        cr_anime.Save();
                    }
                    // update the missing episode stats on groups and children
                    SVR_AnimeSeries series = Repo.AnimeSeries.GetByAnimeID(animeID);
                    series?.QueueUpdateStats();
                    //series.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                }
                if (anime != null)
                {

                    // check if we have this episode in the database
                    // if not get it now by updating the anime record
                    List<AniDB_Episode> eps = Repo.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(animeID,
                        getCmd.GrpStatusCollection.LatestEpisodeNumber);
                    if (eps.Count == 0)
                    {
                        CommandRequest_GetAnimeHTTP cr_anime =
                            new CommandRequest_GetAnimeHTTP(animeID, true, false);
                        cr_anime.Save();
                    }
                    // update the missing episode stats on groups and children
                    SVR_AnimeSeries series = Repo.AnimeSeries.GetByAnimeID(animeID);
                    series?.QueueUpdateStats();
                    //series.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                }
            }

            return getCmd.GrpStatusCollection;
        }

        public CalendarCollection GetCalendarUDP()
        {
            if (!Login()) return null;

            enHelperActivityType ev = enHelperActivityType.CalendarEmpty;
            AniDBCommand_GetCalendar cmd = null;
            lock (lockAniDBConnections)
            {
                cmd = new AniDBCommand_GetCalendar();
                cmd.Init();
                SetWaitingOnResponse(true);
                ev = cmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev == enHelperActivityType.GotCalendar && cmd.Calendars != null)
                return cmd.Calendars;

            return null;
        }

        public AniDB_Review GetReviewUDP(int reviewID)
        {
            if (!Login()) return null;

            enHelperActivityType ev = enHelperActivityType.NoSuchReview;
            AniDBCommand_GetReview cmd = null;

            lock (lockAniDBConnections)
            {
                cmd = new AniDBCommand_GetReview();
                cmd.Init(reviewID);
                SetWaitingOnResponse(true);
                ev = cmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev == enHelperActivityType.GotReview && cmd.ReviewInfo != null)
            {
                using (var upd = Repo.AniDB_Review.BeginAddOrUpdate(()=>Repo.AniDB_Review.GetByID(reviewID)))
                {
                    upd.Entity.Populate_RA(cmd.ReviewInfo);
                    return upd.Commit();
                }
            }

            return null;
        }

        public void VoteAnime(int animeID, decimal voteValue, AniDBVoteType voteType)
        {
            if (!Login()) return;


            lock (lockAniDBConnections)
            {
                var cmdVote = new AniDBCommand_Vote();
                cmdVote.Init(animeID, voteValue, voteType);
                SetWaitingOnResponse(true);
                var ev = cmdVote.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                if (ev != enHelperActivityType.Voted && ev != enHelperActivityType.VoteUpdated) return;
                AniDB_Vote thisVote = Repo.AniDB_Vote.GetByEntityAndType(cmdVote.EntityID, voteType) ?? new AniDB_Vote
                {
                    EntityID = cmdVote.EntityID
                };

                thisVote.VoteType = (int) cmdVote.VoteType;
                thisVote.VoteValue = cmdVote.VoteValue;
                Repo.AniDB_Vote.Save(thisVote);
            }
        }

        public void VoteAnimeRevoke(int animeID, AniDBVoteType voteType)
        {
            VoteAnime(animeID, -1, voteType);
        }


        public SVR_AniDB_Anime GetAnimeInfoHTTP(int animeID, bool forceRefresh = false, bool downloadRelations = true, int relDepth = 0)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetAnimeInfoHTTP(session, animeID, forceRefresh, downloadRelations, relDepth);
            }
        }

        public SVR_AniDB_Anime GetAnimeInfoHTTP(ISession session, int animeID, bool forceRefresh,
            bool downloadRelations, int relDepth = 0)
        {
            //if (!Login()) return null;

            ISessionWrapper sessionWrapper = session.Wrap();

            var anime = Repo.AniDB_Anime.GetByID(sessionWrapper, animeID);
            var update = Repo.AniDB_AnimeUpdate.GetByAnimeID(animeID);
            bool skip = true;
            bool animeRecentlyUpdated = false;
            if (anime != null && update != null)
            {
                TimeSpan ts = DateTime.Now - update.UpdatedAt;
                if (ts.TotalHours < 4) animeRecentlyUpdated = true;
            }
            if (!animeRecentlyUpdated)
            {
                if (forceRefresh)
                    skip = false;
                else if (anime == null) skip = false;
            }

            AniDBHTTPCommand_GetFullAnime getAnimeCmd;
            lock (lockAniDBConnections)
            {
                getAnimeCmd = new AniDBHTTPCommand_GetFullAnime();
                getAnimeCmd.Init(animeID, false, !skip, animeRecentlyUpdated);
                var result = getAnimeCmd.Process();
                if (result == enHelperActivityType.Banned_555 || result == enHelperActivityType.NoSuchAnime)
                {
                    logger.Error($"Failed get anime info for {animeID}. AniDB ban or No Such Anime returned");
                    return null;
                }
            }


            if (getAnimeCmd.Anime != null)
            {
                return SaveResultsForAnimeXML(session, animeID, downloadRelations || ServerSettings.AutoGroupSeries, true, getAnimeCmd, relDepth);
                //this endpoint is not working, so comenting...
/*
                if (forceRefresh)
                {
                    CommandRequest_Azure_SendAnimeFull cmdAzure = new CommandRequest_Azure_SendAnimeFull(anime.AnimeID);
                    cmdAzure.Save(session);
                }*/
            }

            logger.Error($"Failed get anime info for {animeID}. Anime was null");
            return null;
        }

        public SVR_AniDB_Anime SaveResultsForAnimeXML(int animeID, bool downloadRelations,
            bool validateImages,
            AniDBHTTPCommand_GetFullAnime getAnimeCmd, int relDepth)
        {
            

            logger.Trace("cmdResult.Anime: {0}", getAnimeCmd.Anime);

            var anime = Repo.AniDB_Anime.GetByID(animeID) ?? new SVR_AniDB_Anime();
            if (!anime.PopulateAndSaveFromHTTP(getAnimeCmd.Anime, getAnimeCmd.Episodes, getAnimeCmd.Titles,
                getAnimeCmd.Categories, getAnimeCmd.Tags,
                getAnimeCmd.Characters, getAnimeCmd.Resources, getAnimeCmd.Relations, getAnimeCmd.SimilarAnime, getAnimeCmd.Recommendations,
                downloadRelations, relDepth))
            {
                logger.Error($"Failed populate anime info for {animeID}");
                return null;
            }

            // All images from AniDB are downloaded in this
            if (validateImages)
            {
                var cmd = new CommandRequest_DownloadAniDBImages(anime.AnimeID, false);
                cmd.Save();
            }

            // create AnimeEpisode records for all episodes in this anime only if we have a series
            SVR_AnimeSeries ser = Repo.AnimeSeries.GetByAnimeID(animeID);
            Repo.AniDB_Anime.Save(anime);
            if (ser != null)
            {
                ser.CreateAnimeEpisodes();
                Repo.AnimeSeries.Save(ser, true, false);
            }
            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            return anime;
        }

        public bool ValidAniDBCredentials()
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(serverName)
                || string.IsNullOrEmpty(serverPort) || string.IsNullOrEmpty(clientPort))
            {
                //OnAniDBStatusEvent(new AniDBStatusEventArgs(enHelperActivityType.OtherError, "ERROR: Please enter valid AniDB credentials via Configuration first"));
                return false;
            }

            return true;
        }

        private bool BindToLocalPort()
        {
            // only do once
            //if (localIpEndPoint != null) return false;
            localIpEndPoint = null;

            // Dont send Expect 100 requests. These requests arnt always supported by remote internet devices, in which case can cause failure.
            ServicePointManager.Expect100Continue = false;

            try
            {
                IPHostEntry localHostEntry = Dns.GetHostEntry(Dns.GetHostName());


                logger.Info("-------- Local IP Addresses --------");
                localIpEndPoint = new IPEndPoint(IPAddress.Any, Convert.ToInt32(clientPort));
                logger.Info("-------- End Local IP Addresses --------");

                soUdp.Bind(localIpEndPoint);
                soUdp.ReceiveTimeout = 30000; // 30 seconds

                logger.Info("BindToLocalPort: Bound to local address: {0} - Port: {1} ({2})",
                    localIpEndPoint,
                    clientPort,
                    localIpEndPoint.AddressFamily);

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not bind to local port: {ex}");
                return false;
            }
        }

        private bool BindToRemotePort()
        {
            // only do once
            remoteIpEndPoint = null;
            //if (remoteIpEndPoint != null) return true;

            try
            {
                IPHostEntry remoteHostEntry = Dns.GetHostEntry(serverName);
                remoteIpEndPoint = new IPEndPoint(remoteHostEntry.AddressList[0], Convert.ToInt32(serverPort));

                logger.Info("BindToRemotePort: Bound to remote address: " + remoteIpEndPoint.Address +
                            " : " +
                            remoteIpEndPoint.Port);

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not bind to remote port: {ex}");
                return false;
            }
        }
    }
}