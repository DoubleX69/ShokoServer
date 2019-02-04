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
using NLog;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Server.CommandQueue.Commands.AniDB;
using Shoko.Server.CommandQueue.Commands.Image;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Commands;
using Shoko.Server.Providers.AniDB.Raws;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
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
        private ushort serverPort = 0;
        private ushort clientPort = 0;

        private Timer logoutTimer;

        private Timer httpBanResetTimer;
        private Timer udpBanResetTimer;

        public DateTime? HttpBanTime { get; set; }
        public DateTime? UdpBanTime { get; set; }

        private bool _isHttpBanned;
        private bool _isUdpBanned;

        public TimeSpan UdpBanRetryTime => UdpBanTime.Value.AddMilliseconds(udpBanResetTimer.Interval).Subtract(DateTime.Now);

        public TimeSpan HttpBanRetryTime => HttpBanTime.Value.AddMilliseconds(httpBanResetTimer.Interval).Subtract(DateTime.Now);

        public bool IsHttpBanned
        {
            get => _isHttpBanned;
            set
            {
                _isHttpBanned = value;
                if (value)
                {
                    HttpBanTime = DateTime.Now;
                    ServerInfo.Instance.IsBanned = true;
                    ServerInfo.Instance.BanOrigin = @"HTTP";
                    ServerInfo.Instance.BanReason = HttpBanTime.ToString();
                    if (httpBanResetTimer.Enabled)
                    {
                        logger.Warn("HTTP ban timer was already running, ban time extending");
                        httpBanResetTimer.Stop(); //re-start implies stop
                    }
                    httpBanResetTimer.Start();
                    Analytics.PostEvent("AniDB", "Http Banned");
                }
                else
                {
                    if (httpBanResetTimer.Enabled)
                    {
                        httpBanResetTimer.Stop();
                        logger.Info("HTTP ban timer stopped. Resuming queue if not paused.");
                        // Skip if paused
                        /* TODO NEED TO REDO THIS, Queue supports for prequisites now.
                        if (!ShokoService.CmdProcessorGeneral.Paused)
                        {
                            // Needs to have something to do first
                            if (ShokoService.CmdProcessorGeneral.QueueCount > 0)
                            {
                                // Not really a new command, but this will start the queue if it's not running,
                                // with handling for problems
                                ShokoService.CmdProcessorGeneral.NotifyOfNewCommand();
                            }
                        }*/
                    }
                    if (!IsUdpBanned)
                    {
                        ServerInfo.Instance.IsBanned = false;
                        ServerInfo.Instance.BanOrigin = string.Empty;
                        ServerInfo.Instance.BanReason = string.Empty;
                    }
                }
            }
        }

        public bool IsUdpBanned
        {
            get => _isUdpBanned;
            set
            {
                _isUdpBanned = value;
                if (value)
                {
                    UdpBanTime = DateTime.Now;
                    ServerInfo.Instance.IsBanned = true;
                    ServerInfo.Instance.BanOrigin = @"UDP";
                    ServerInfo.Instance.BanReason = UdpBanTime.ToString();
                    if (udpBanResetTimer.Enabled)
                    {
                        logger.Warn("UDP ban timer was already running, ban time extending");
                        udpBanResetTimer.Stop(); // re-start implies stop
                    }
                    udpBanResetTimer.Start();
                    Analytics.PostEvent("AniDB", "Udp Banned");
                }
                else
                {
                    if (udpBanResetTimer.Enabled)
                    {
                        udpBanResetTimer.Stop();
                        logger.Info("UDP ban timer stopped. Resuming if not Paused");
                        /* TODO NEED TO REDO THIS, Queue supports for prequisites now.
        // Skip if paused
        if (!ShokoService.CmdProcessorGeneral.Paused)
        {
            // Needs to have something to do first
            if (ShokoService.CmdProcessorGeneral.QueueCount > 0)
            {
                // Not really a new command, but this will start the queue if it's not running,
                // with handling for problems
                ShokoService.CmdProcessorGeneral.NotifyOfNewCommand();
            }
        }*/
                    }
                    if (!IsHttpBanned)
                    {
                        ServerInfo.Instance.IsBanned = false;
                        ServerInfo.Instance.BanOrigin = string.Empty;
                        ServerInfo.Instance.BanReason = string.Empty;
                    }
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
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

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

        public void Init(string userName, string password, string serverName, ushort serverPort, ushort clientPort)
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
            httpBanResetTimer.AutoReset = false;
            httpBanResetTimer.Elapsed += HTTPBanResetTimerElapsed;
            httpBanResetTimer.Interval = TimeSpan.FromHours(12).TotalMilliseconds;

            udpBanResetTimer = new Timer();
            udpBanResetTimer.AutoReset = false;
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
            try
            {
                if (soUdp.Connected)
                {
                    soUdp.Shutdown(SocketShutdown.Both);
                    soUdp.Disconnect(false);
                }
            }
            catch (SocketException ex)
            {
                logger.Error(ex.ToString(), $"Failed to Shutdown and Disconnect the connection to AniDB: {0}");
            }
            finally
            {
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
                        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

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

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

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
            logger.Info("HTTP ban (12h) is over");
            IsHttpBanned = false;
        }

        private void UDPBanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            logger.Info("UDP ban (12h) is over");
            IsUdpBanned = false;
        }

        private void SetWaitingOnResponse(bool isWaiting)
        {
            WaitingOnResponse = isWaiting;
            ServerInfo.Instance.WaitingOnResponseAniDBUDP = isWaiting;

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

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

                    if (ServerSettings.Instance.AniDb.DownloadReleaseGroups)
                    {
                        CommandQueue.Queue.Instance.Add(new CmdAniDBGetReleaseGroup(getInfoCmd.fileInfo.GroupID, false));
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

        public void GetMyListFileStatus(int fileID)
        {
            if (!ServerSettings.Instance.AniDb.MyList_ReadWatched) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                AniDBCommand_GetMyListFileInfo cmdGetFileStatus = new AniDBCommand_GetMyListFileInfo();
                cmdGetFileStatus.Init(fileID);
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
                var aniFile = Repo.Instance.AniDB_File.GetByID(fileID);
                var vids = aniFile.EpisodeIDs.SelectMany(a => Repo.Instance.VideoLocal.GetByAniDBEpisodeID(a)).Where(a => a != null).ToList();
                foreach (var vid in vids)
                {
                    foreach (var user in Repo.Instance.JMMUser.GetAniDBUsers())
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
                    using (var upd = Repo.Instance.AniDB_MylistStats.BeginAddOrUpdate(() => Repo.Instance.AniDB_MylistStats.GetAll().FirstOrDefault()))
                    {
                        upd.Entity.Populate_RA(cmdGetMylistStats.MyListStats);
                        upd.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the list of updated AnimeIDs. This may use more than one call to get them all, and therefore a lot of time
        /// </summary>
        /// <param name="updatedAnimeIDs">The updated IDs</param>
        /// <param name="lastUpdateTime">The time that the last anime was updated</param>
        public void GetUpdated(ref List<int> updatedAnimeIDs, ref long lastUpdateTime)
        {
            updatedAnimeIDs = new List<int>();

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                AniDBCommand_GetUpdated cmdUpdated = new AniDBCommand_GetUpdated();
                cmdUpdated.Init(lastUpdateTime.ToString());
                SetWaitingOnResponse(true);
                enHelperActivityType ev = cmdUpdated.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);

                if (ev != enHelperActivityType.GotUpdated) return;

                int records = cmdUpdated.RecordCount;
                if (records <= 0) return;

                lastUpdateTime = long.Parse(cmdUpdated.LastUpdateTime);
                updatedAnimeIDs.AddRange(cmdUpdated.AnimeIDList);

                // we got them all
                if (records <= 200) return;

                // while loop it to be sure
                while (records > 200)
                {
                    // reinit with last update time provided
                    cmdUpdated = new AniDBCommand_GetUpdated();
                    cmdUpdated.Init(lastUpdateTime.ToString());
                    // get the rest (assuming RecordCount was <= 400
                    SetWaitingOnResponse(true);
                    ev = cmdUpdated.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                        new UnicodeEncoding(true, false));
                    SetWaitingOnResponse(false);

                    if (ev != enHelperActivityType.GotUpdated) return;

                    // update records with new count
                    records = cmdUpdated.RecordCount;
                    if (records <= 0) return;

                    lastUpdateTime = long.Parse(cmdUpdated.LastUpdateTime);

                    // if the first/last item overlap, then remove it so it isn't duplicated
                    if (cmdUpdated.AnimeIDList.Count > 0 && cmdUpdated.AnimeIDList[0] == updatedAnimeIDs.LastOrDefault())
                        cmdUpdated.AnimeIDList.RemoveAt(0);

                    updatedAnimeIDs.AddRange(cmdUpdated.AnimeIDList);
                }
            }
        }

        /// <summary>
        /// This is not for generic files (manually linked)
        /// </summary>
        /// <param name="animeID"></param>
        /// <param name="episodeNumber"></param>
        /// <param name="watched"></param>
        public void UpdateMyListFileStatus(IHash hash, bool watched, DateTime? watchedDate = null)
        {
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                if (watched && watchedDate == null) watchedDate = DateTime.Now;

                enHelperActivityType ev;
                // We have the ID, so update it
                if (hash.MyListID > 0)
                {
                    AniDBCommand_UpdateFile cmdUpdateFile = new AniDBCommand_UpdateFile();
                    cmdUpdateFile.Init(hash, watched, watchedDate);
                    SetWaitingOnResponse(true);
                    ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                        new UnicodeEncoding(true, false));
                    SetWaitingOnResponse(false);
                }
                else
                {
                    logger.Trace($"File has no MyListID, attempting to add: {hash.ED2KHash}");
                    // We don't have the MyListID, so we'll act like it's not there, and AniDB will tell us
                    ev = enHelperActivityType.NoSuchMyListFile;
                }

                if (ev == enHelperActivityType.NoSuchMyListFile)
                {
                    // Run synchronously, but still do all of the stuff with Trakt and whatnot
                    // We are skipping the watched state settings, as we are setting them here
                    new CmdAniDBAddFileToMyList(hash.ED2KHash, false).Run();
                }
            }
        }

        /// <summary>
        /// This is for generic files (manually linked)
        /// </summary>
        /// <param name="animeID"></param>
        /// <param name="episodeNumber"></param>
        /// <param name="watched"></param>
        public void UpdateMyListFileStatus(IHash hash, int animeID, int episodeNumber, bool watched, DateTime? watchedDate = null)
        {
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                if (watched && watchedDate == null) watchedDate = DateTime.Now;

                AniDBCommand_UpdateFile cmdUpdateFile = new AniDBCommand_UpdateFile();
                cmdUpdateFile.Init(hash, animeID, episodeNumber, watched, watchedDate);
                SetWaitingOnResponse(true);
                var ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);

                if (ev == enHelperActivityType.NoSuchMyListFile)
                {
                    // Run synchronously, but still do all of the stuff with Trakt and whatnot
                    // We are skipping the watched state settings, as we are setting them here
                    new CmdAniDBAddFileToMyList(hash.ED2KHash, false).Run();
                }
            }
        }

        public (int?, DateTime?) AddFileToMyList(IHash fileDataLocal, DateTime? watchedDate, ref AniDBFile_State? state)
        {
            // It's easier to compare a change if we return the original watch date instead of null, since null means unwatched
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return (null, watchedDate);

            if (!Login()) return (null, watchedDate);

            enHelperActivityType ev;
            AniDBCommand_AddFile cmdAddFile;

            lock (lockAniDBConnections)
            {
                cmdAddFile = new AniDBCommand_AddFile();
                cmdAddFile.Init(fileDataLocal, ServerSettings.Instance.AniDb.MyList_StorageState, watchedDate);
                SetWaitingOnResponse(true);
                ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            // if the user already has this file on
            if (ev == enHelperActivityType.FileAlreadyExists && cmdAddFile.FileData != null)
            {
                state = cmdAddFile.State;
                return (cmdAddFile.MyListID, cmdAddFile.WatchedDate);
            }

            if (cmdAddFile.MyListID > 0) return (cmdAddFile.MyListID, watchedDate);

            return (null, watchedDate);
        }

        public (int?, DateTime?) AddFileToMyList(int animeID, int episodeNumber, DateTime? watchedDate, ref AniDBFile_State? state)
        {
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return (null, watchedDate);
            // It's easier to compare a change if we return the original watch date instead of null, since null means unwatched
            if (!Login()) return (null, watchedDate);

            enHelperActivityType ev;
            AniDBCommand_AddFile cmdAddFile;

            lock (lockAniDBConnections)
            {
                cmdAddFile = new AniDBCommand_AddFile();
                string episodeData = "";
                if (episodeType == EpisodeType.Credits)
                    episodeData = "C";
                else if (episodeType == EpisodeType.Special)
                    episodeData = "S";
                else if (episodeType == EpisodeType.Trailer)
                    episodeData = "T";
                else if (episodeType == EpisodeType.Parody)
                    episodeData = "P";
                else if (episodeType == EpisodeType.Other)
                    episodeData = "O";
                episodeData += episodeNumber;
                cmdAddFile.Init(animeID, episodeData, ServerSettings.Instance.AniDb.MyList_StorageState, watchedDate);
                SetWaitingOnResponse(true);
                ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            // if the user already has this file on
            if (ev == enHelperActivityType.FileAlreadyExists && cmdAddFile.FileData != null)
            {
                state = cmdAddFile.State;
                return (cmdAddFile.MyListID, cmdAddFile.WatchedDate);
            }

            if (cmdAddFile.MyListID > 0) return (cmdAddFile.MyListID, watchedDate);

            return (null, watchedDate);
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
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return;

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
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return;

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
                anime = Repo.Instance.AniDB_Anime.GetByID(animeID);
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
                anime.PopulateAndSaveFromUDP(getAnimeCmd.AnimeInfo);
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
            return ev == enHelperActivityType.GotCharInfo && getCharCmd.CharInfo != null ? getCharCmd.CharInfo : null;
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
            using (var upd = Repo.Instance.AniDB_ReleaseGroup.BeginAddOrUpdate(groupID))
            {
                upd.Entity.Populate_RA(getCmd.Group);
                upd.Commit();
            }
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
            Repo.Instance.AniDB_GroupStatus.DeleteForAnime(animeID);

            // save the records
            foreach (Raw_AniDB_GroupStatus raw in getCmd.GrpStatusCollection.Groups)
            {
                using (var upd = Repo.Instance.AniDB_GroupStatus.BeginAdd())
                {
                    upd.Entity.Populate_RA(raw);
                    upd.Commit();
                }
            }

            if (getCmd.GrpStatusCollection.LatestEpisodeNumber > 0)
            {
                // update the anime with a record of the latest subbed episode
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByID(animeID);
                if (anime != null)
                {
                    using (var upd = Repo.Instance.AniDB_Anime.BeginAddOrUpdate(anime))
                    {
                        upd.Entity.LatestEpisodeNumber = getCmd.GrpStatusCollection.LatestEpisodeNumber;
                        anime = upd.Commit();
                    }

                    // check if we have this episode in the database
                    // if not get it now by updating the anime record
                    List<AniDB_Episode> eps = Repo.Instance.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(animeID,
                        getCmd.GrpStatusCollection.LatestEpisodeNumber);
                    if (eps.Count == 0)
                    {
                        CommandQueue.Queue.Instance.Add(new CmdAniDBGetAnimeHTTP(animeID, true, false, 0));
                    }
                    // update the missing episode stats on groups and children
                    SVR_AnimeSeries series = Repo.Instance.AnimeSeries.GetByAnimeID(animeID);
                    series?.QueueUpdateStats();
                    //series.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                }
                if (anime != null)
                {

                    // check if we have this episode in the database
                    // if not get it now by updating the anime record
                    List<AniDB_Episode> eps = Repo.Instance.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(animeID,
                        getCmd.GrpStatusCollection.LatestEpisodeNumber);
                    if (eps.Count == 0)
                    {
                        CommandQueue.Queue.Instance.Add(new CmdAniDBGetAnimeHTTP(animeID, true, false));
                    }
                    // update the missing episode stats on groups and children
                    SVR_AnimeSeries series = Repo.Instance.AnimeSeries.GetByAnimeID(animeID);
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
                using (var upd = Repo.Instance.AniDB_Review.BeginAddOrUpdate(reviewID))
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

                using (var upd = Repo.Instance.AniDB_Vote.BeginAddOrUpdate(() => Repo.Instance.AniDB_Vote.GetByEntityAndType(cmdVote.EntityID, voteType), () => new AniDB_Vote { EntityID = cmdVote.EntityID }))
                {
                    upd.Entity.VoteType = (int)cmdVote.VoteType;
                    upd.Entity.VoteValue = cmdVote.VoteValue;
                    upd.Commit();
                }
            }
        }

        public void VoteAnimeRevoke(int animeID, AniDBVoteType voteType)
        {
            VoteAnime(animeID, -1, voteType);
        }
        public SVR_AniDB_Anime GetAnimeInfoHTTP(int animeID, bool forceRefresh,
        bool downloadRelations, int relDepth = 0)
        {
            //if (!Login()) return null;

            var anime = Repo.Instance.AniDB_Anime.GetByID(animeID);
            var update = Repo.Instance.AniDB_AnimeUpdate.GetByAnimeID(animeID);
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
                return SaveResultsForAnimeXML(animeID, downloadRelations || ServerSettings.Instance.AutoGroupSeries, true, getAnimeCmd, relDepth);
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

            SVR_AniDB_Anime anime;
            using (var upd = Repo.Instance.AniDB_Anime.BeginAddOrUpdate(animeID))
            {
                if (!upd.Entity.PopulateFromHTTP(getAnimeCmd.Anime, getAnimeCmd.Episodes, getAnimeCmd.Titles,
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
                    CommandQueue.Queue.Instance.Add(new CmdImageDownloadAllAniDb(upd.Entity.AnimeID, false));
                }

                anime = upd.Commit();
            }
            // create AnimeEpisode records for all episodes in this anime only if we have a series
            SVR_AnimeSeries ser = Repo.Instance.AnimeSeries.GetByAnimeID(animeID);
            if (ser != null)
            {
                using (var upd = Repo.Instance.AnimeSeries.BeginAddOrUpdate(ser))
                {
                    upd.Entity.CreateAnimeEpisodes();
                    upd.Commit();
                }
            }
            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            return anime;
        }

        public bool ValidAniDBCredentials()
        {
            var settings = ServerSettings.Instance.AniDb;
            userName = settings.Username;
            password = settings.Password;
            serverName = settings.ServerAddress;
            serverPort = settings.ServerPort;
            clientPort = settings.ClientPort;

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(serverName)
                || serverPort == 0 || clientPort == 0)
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
