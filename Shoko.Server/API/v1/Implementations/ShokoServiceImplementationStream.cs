﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Models.Interfaces;
using Shoko.Server.API.v1;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using HttpContext = Microsoft.AspNetCore.Http.HttpContext;
using Mime = MimeMapping.MimeUtility;
using Timer = System.Timers.Timer;

namespace Shoko.Server
{
    [ApiController, Route("/Stream"), ApiVersion("1.0", Deprecated = true)]
    public class ShokoServiceImplementationStream : Controller, IShokoServerStream, IHttpContextAccessor
    {
        static ShokoServiceImplementationStream()
        {
            ConnectionTimer.Elapsed += TimerElapsed;
        }

        public new HttpContext HttpContext { get; set; }

        //89% Should be enough to not touch matroska offsets and give us some margin
        private double WatchedThreshold = 0.89;

        public const string ServerVersion = "Shoko Stream Server 1.0";
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        ///  A list of open connections to the API
        /// </summary>
        private static readonly HashSet<string> OpenConnections = new HashSet<string>();
        /// <summary>
        /// blur the connection state to 5s, as most media players and calls are spread.
        /// This prevents flickering of the state for UI
        /// </summary>
        private static readonly Timer ConnectionTimer = new Timer(5000);
        
        private static void AddConnection(HttpContext ctx)
        {
            lock (OpenConnections)
            {
                OpenConnections.Add(ctx.Connection.Id);
                ServerState.Instance.ApiInUse = OpenConnections.Count > 0;
            }
        }
        
        private static void RemoveConnection(HttpContext ctx)
        {
            if (!ctx.Items.ContainsKey(ctx.Connection.Id)) return;
            lock (OpenConnections)
            {
                OpenConnections.Remove(ctx.Connection.Id);
            }
            ResetTimer();
        }

        private static void ResetTimer()
        {
            lock (ConnectionTimer)
            {
                ConnectionTimer.Stop();
                ConnectionTimer.Start();
            }
        }

        private static void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            lock (OpenConnections)
            {
                ServerState.Instance.ApiInUse = OpenConnections.Count > 0;
            }
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            AddConnection(context.HttpContext);
            base.OnActionExecuting(context);
        }
        
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            base.OnActionExecuted(context);
            RemoveConnection(context.HttpContext);
        }

        [HttpGet("{videolocalid}/{userId?}/{autowatch?}/{fakename?}")]
        [ProducesResponseType(typeof(FileStreamResult),200), ProducesResponseType(typeof(FileStreamResult),206), ProducesResponseType(404)]
        public Stream StreamVideo(int videolocalid, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveVideoLocal(videolocalid, userId, autowatch);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
            {
                return new StreamWithResponse(r.Status, r.StatusDescription);
            }
            return StreamFromIFile(r, autowatch);
        }

        [HttpGet("Filename/{base64filename}/{userId?}/{autowatch?}/{fakename?}")]
        [ProducesResponseType(typeof(FileStreamResult),200), ProducesResponseType(typeof(FileStreamResult),206), ProducesResponseType(404)]
        public Stream StreamVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveFilename(base64filename, userId, autowatch);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
            {
                return new StreamWithResponse(r.Status, r.StatusDescription);
            }
            return StreamFromIFile(r, autowatch);
        }

        private Stream StreamFromIFile(InfoResult r, bool? autowatch)
        {
            try
            {
                var request = HttpContext.Request;

                FileSystemResult<Stream> fr = r.File.OpenRead();
                if (fr == null || !fr.IsOk)
                {
                    return new StreamWithResponse(HttpStatusCode.BadRequest,
                        "Unable to open file '" + r.File.FullName + "': " + fr?.Error);
                }
                Stream org = fr.Result;
                long totalsize = org.Length;
                long start = 0;
                long end = totalsize - 1;

                string rangevalue = request.Headers["Range"].FirstOrDefault() ??
                                    request.Headers["range"].FirstOrDefault();
                rangevalue = rangevalue?.Replace("bytes=", string.Empty);
                bool range = !string.IsNullOrEmpty(rangevalue);

                if (range)
                {
                    // range: bytes=split[0]-split[1]
                    string[] split = rangevalue.Split('-');
                    if (split.Length == 2)
                    {
                        // bytes=-split[1] - tail of specified length
                        if (string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                        {
                            long e = long.Parse(split[1]);
                            start = totalsize - e;
                            end = totalsize - 1;
                        }
                        // bytes=split[0] - split[0] to end of file
                        else if (!string.IsNullOrEmpty(split[0]) && string.IsNullOrEmpty(split[1]))
                        {
                            start = long.Parse(split[0]);
                            end = totalsize - 1;
                        }
                        // bytes=split[0]-split[1] - specified beginning and end
                        else if (!string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                        {
                            start = long.Parse(split[0]);
                            end = long.Parse(split[1]);
                            if (start > totalsize - 1)
                                start = totalsize - 1;
                            if (end > totalsize - 1)
                                end = totalsize - 1;
                        }
                    }
                }
                var outstream = new SubStream(org, start, end - start + 1);
                var resp = new StreamWithResponse {ContentType = r.Mime};
                resp.Headers.Add("Server", ServerVersion);
                resp.Headers.Add("Connection", "keep-alive");
                resp.Headers.Add("Accept-Ranges", "bytes");
                resp.Headers.Add("Content-Range", "bytes " + start + "-" + end + "/" + totalsize);
                resp.ContentLength = end - start + 1;

                resp.ResponseStatus = range ? HttpStatusCode.PartialContent : HttpStatusCode.OK;

                if (r.User != null && autowatch.HasValue && autowatch.Value && r.VideoLocal != null)
                {
                    outstream.CrossPosition = (long) (totalsize * WatchedThreshold);
                    outstream.CrossPositionCrossed +=
                        (a) =>
                        {
                            Task.Factory.StartNew(() => { r.VideoLocal.ToggleWatchedStatus(true, r.User.JMMUserID); },
                                new CancellationToken(),
                                TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        };
                }
                resp.Stream = outstream;
                return resp;
            }
            catch (Exception e)
            {
                logger.Error("An error occurred while serving a file: " + e);
                var resp = new StreamWithResponse();
                resp.ResponseStatus = HttpStatusCode.InternalServerError;
                resp.ResponseDescription = e.Message;
                return resp;
            }
        }

        [HttpHead("{videolocalid}/{userId?}/{autowatch?}/{fakename?}")]
        public Stream InfoVideo(int videolocalid, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveVideoLocal(videolocalid, userId, autowatch);
            StreamWithResponse s = new StreamWithResponse(r.Status, r.StatusDescription);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
                return s;
            s.Headers.Add("Server", ServerVersion);
            s.Headers.Add("Accept-Ranges", "bytes");
            s.Headers.Add("Content-Range", "bytes 0-" + (r.File.Size - 1) + "/" + r.File.Size);
            s.ContentType = r.Mime;
            s.ContentLength = r.File.Size;
            return s;
        }

        [HttpHead("Filename/{base64filename}/{userId?}/{autowatch?}/{fakename?}")]
        public Stream InfoVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveFilename(base64filename, userId, autowatch);
            StreamWithResponse s = new StreamWithResponse(r.Status, r.StatusDescription);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
                return s;
            s.Headers.Add("Server", ServerVersion);
            s.Headers.Add("Accept-Ranges", "bytes");
            s.Headers.Add("Content-Range", "bytes 0-" + (r.File.Size - 1) + "/" + r.File.Size);
            s.ContentType = r.Mime;
            s.ContentLength = r.File.Size;
            return s;
        }

        class InfoResult
        {
            public IFile File { get; set; }
            public SVR_VideoLocal VideoLocal { get; set; }
            public SVR_JMMUser User { get; set; }
            public HttpStatusCode Status { get; set; }
            public string StatusDescription { get; set; }
            public string Mime { get; set; }
        }

        private InfoResult ResolveVideoLocal(int videolocalid, int? userId, bool? autowatch)
        {
            InfoResult r = new InfoResult();
            SVR_VideoLocal loc = RepoFactory.VideoLocal.GetByID(videolocalid);
            if (loc == null)
            {
                r.Status = HttpStatusCode.BadRequest;
                r.StatusDescription = "Video Not Found";
                return r;
            }
            r.VideoLocal = loc;
            r.File = loc.GetBestFileLink();
            return FinishResolve(r, userId, autowatch);
        }

        public static string Base64DecodeUrl(string base64EncodedData)
        {
            var base64EncodedBytes =
                System.Convert.FromBase64String(base64EncodedData.Replace("-", "+")
                    .Replace("_", "/")
                    .Replace(",", "="));
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        private InfoResult FinishResolve(InfoResult r, int? userId, bool? autowatch)
        {
            if (r.File == null)
            {
                r.Status = HttpStatusCode.NotFound;
                r.StatusDescription = "Video Not Found";
                return r;
            }
            if (userId.HasValue && autowatch.HasValue && userId.Value != 0)
            {
                r.User = RepoFactory.JMMUser.GetByID(userId.Value);
                if (r.User == null)
                {
                    r.Status = HttpStatusCode.NotFound;
                    r.StatusDescription = "User Not Found";
                    return r;
                }
            }
            r.Mime = r.File.ContentType;
            if (string.IsNullOrEmpty(r.Mime) || r.Mime.Equals("application/octet-stream",
                    StringComparison.InvariantCultureIgnoreCase))
                r.Mime = Mime.GetMimeMapping(r.File.FullName);
            r.Status = HttpStatusCode.OK;
            return r;
        }

        private InfoResult ResolveFilename(string filenamebase64, int? userId, bool? autowatch)
        {
            InfoResult r = new InfoResult();
            string fullname = Base64DecodeUrl(filenamebase64);
            r.VideoLocal = null;
            r.File = SVR_VideoLocal.ResolveFile(fullname);
            return FinishResolve(r, userId, autowatch);
        }
    }
}