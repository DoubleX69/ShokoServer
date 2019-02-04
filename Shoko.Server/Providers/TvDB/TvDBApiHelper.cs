﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.CommandQueue.Commands.Image;
using Shoko.Server.CommandQueue.Commands.Trakt;
using Shoko.Server.CommandQueue.Commands.TvDB;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using TvDbSharper;
using TvDbSharper.Dto;
using Language = TvDbSharper.Dto.Language;
using Shoko.Models.WebCache;

namespace Shoko.Server.Providers.TvDB
{
    public static class TvDBApiHelper
    {
        private static readonly ITvDbClient client = new TvDbClient();
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static string CurrentServerTime
        {
            get
            {
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local);
                TimeSpan span = DateTime.Now - epoch;
                return ((long)span.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }
        }

        private static async Task CheckAuthorizationAsync()
        {
            try
            {
                client.AcceptedLanguage = ServerSettings.Instance.TvDB.Language;
                if (string.IsNullOrEmpty(client.Authentication.Token))
                {
                    TvDBRateLimiter.Instance.EnsureRate();
                    await client.Authentication.AuthenticateAsync(Constants.TvDB.apiKey);
                    if (string.IsNullOrEmpty(client.Authentication.Token))
                        throw new TvDbServerException("Authentication Failed", 200);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, $"Error in TvDBAuth: {e}");
                throw;
            }
        }

        public static TvDB_Series GetSeriesInfoOnline(int seriesID, bool forceRefresh)
        {
            return GetSeriesInfoOnlineAsync(seriesID, forceRefresh).Result;
        }

        public static async Task<TvDB_Series> GetSeriesInfoOnlineAsync(int seriesID, bool forceRefresh)
        {
            try
            {
                TvDB_Series tvSeries = Repo.Instance.TvDB_Series.GetByTvDBID(seriesID);
                if (tvSeries != null && !forceRefresh)
                    return tvSeries;
                await CheckAuthorizationAsync();

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Series.GetAsync(seriesID);
                Series series = response.Data;
                using (var tupd = Repo.Instance.TvDB_Series.BeginAddOrUpdate(()=> Repo.Instance.TvDB_Series.GetByTvDBID(seriesID)))
                {
                    tupd.Entity.PopulateFromSeriesInfo(series);
                    tvSeries=tupd.Commit();
                }
                return tvSeries;
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetSeriesInfoOnlineAsync(seriesID, forceRefresh);
                    // suppress 404 and move on
                } else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return null;

                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in TvDBApiHelper.GetSeriesInfoOnline: {ex}");
            }

            return null;
        }

        public static List<TVDB_Series_Search_Response> SearchSeries(string criteria)
        {
            return Task.Run(async () => await SearchSeriesAsync(criteria)).Result;
        }

        public static async Task<List<TVDB_Series_Search_Response>> SearchSeriesAsync(string criteria)
        {
            List<TVDB_Series_Search_Response> results = new List<TVDB_Series_Search_Response>();

            try
            {
                await CheckAuthorizationAsync();

                // Search for a series
                logger.Trace($"Search TvDB Series: {criteria}");

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Search.SearchSeriesByNameAsync(criteria);
                SeriesSearchResult[] series = response?.Data;
                if (series == null) return results;

                foreach (SeriesSearchResult item in series)
                {
                    TVDB_Series_Search_Response searchResult = new TVDB_Series_Search_Response();
                    searchResult.Populate(item);
                    results.Add(searchResult);
                }
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await SearchSeriesAsync(criteria);
                    // suppress 404 and move on
                } else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return results;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}\n        when searching for {criteria}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in SearchSeries: {ex}");
            }

            return results;
        }

        public static void LinkAniDBTvDB(int animeID, int tvDBID, bool additiveLink = false)
        {
            if (!additiveLink)
            {
                // remove all current links
                logger.Info($"Removing All TvDB Links for: {animeID}");
                RemoveAllAniDBTvDBLinks(animeID, false);
            }

            // check if we have this information locally
            // if not download it now
            TvDB_Series tvSeries = Repo.Instance.TvDB_Series.GetByTvDBID(tvDBID);

            if (tvSeries != null)
            {
                // download and update series info, episode info and episode images
                // will also download fanart, posters and wide banners
                CommandQueue.Queue.Instance.Add(new CmdTvDBUpdateSeries(tvDBID,false));
            }
            else
            {
                var unused = GetSeriesInfoOnlineAsync(tvDBID, true).Result;
            }

            using (var upd = Repo.Instance.CrossRef_AniDB_Provider.BeginAddOrUpdate(() => Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIdAndProvider(CrossRefType.TvDB, animeID, tvDBID.ToString())))
            {
                upd.Entity.CrossRefSource = CrossRefSource.User;
                upd.Entity.AnimeID = animeID;
                upd.Entity.CrossRefID = tvDBID.ToString();
                upd.Entity.CrossRefType = CrossRefType.TvDB;
                upd.Commit();
            }

            logger.Info(
                $"Adding TvDB Link: AniDB(ID:{animeID}) -> TvDB(ID:{tvDBID})");

            if (ServerSettings.Instance.TraktTv.Enabled && !string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
            {
                CommandQueue.Queue.Instance.Add(new CmdTraktSearchAnime(animeID, false));
            }
        }
        public static void LinkAniDBTvDBFromWebCache(WebCache_CrossRef_AniDB_Provider cache)
        {


            // check if we have this information locally
            // if not download it now
            int tvDBID = int.Parse(cache.CrossRefID);
            TvDB_Series tvSeries = Repo.Instance.TvDB_Series.GetByTvDBID(tvDBID);

            if (tvSeries == null)
                tvSeries = Task.Run(async () => await GetSeriesInfoOnlineAsync(tvDBID, true)).GetAwaiter().GetResult();
            using (var upd = Repo.Instance.CrossRef_AniDB_Provider.BeginAddOrUpdate(() => Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIdAndProvider(CrossRefType.TvDB, cache.AnimeID, cache.CrossRefID)))
            {
                upd.Entity.CrossRefSource = CrossRefSource.WebCache;
                upd.Entity.AnimeID = cache.AnimeID;
                upd.Entity.CrossRefID = cache.CrossRefID;
                upd.Entity.CrossRefType = CrossRefType.TvDB;
                upd.Entity.EpisodesOverrideData = cache.EpisodesOverrideData;
                upd.Entity.IsAdditive = cache.IsAdditive;
                upd.Commit();
            }
        }
        public static void RemoveAllAniDBTvDBLinks(int animeID, bool updateStats = true)
        {
            Repo.Instance.CrossRef_AniDB_Provider.FindAndDelete(() => Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIDAndType(animeID, CrossRefType.TvDB));
            if (updateStats) SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
        }

        public static List<TvDB_Language> GetLanguages()
        {
            return Task.Run(async () => await GetLanguagesAsync()).Result;
        }

        public static async Task<List<TvDB_Language>> GetLanguagesAsync()
        {
            List<TvDB_Language> languages = new List<TvDB_Language>();

            try
            {
                await CheckAuthorizationAsync();

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Languages.GetAllAsync();
                Language[] apiLanguages = response.Data;

                if (apiLanguages.Length <= 0)
                    return languages;

                foreach (Language item in apiLanguages)
                {
                    TvDB_Language lan = new TvDB_Language
                    {
                        Id = item.Id,
                        EnglishName = item.EnglishName,
                        Name = item.Name,
                        Abbreviation = item.Abbreviation
                    };
                    languages.Add(lan);
                }
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetLanguagesAsync();
                    // suppress 404 and move on
                } else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return languages;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in TVDBHelper.GetSeriesBannersOnline: {ex}");
            }

            return languages;
        }

        public static void DownloadAutomaticImages(int seriesID, bool forceDownload)
        {
            ImagesSummary summary = GetSeriesImagesCounts(seriesID);
            if (summary == null) return;
            if (summary.Fanart > 0) DownloadAutomaticImages(GetFanartOnline(seriesID), seriesID, forceDownload);
            if (summary.Poster > 0 || summary.Season > 0)
                DownloadAutomaticImages(GetPosterOnline(seriesID), seriesID, forceDownload);
            if (summary.Seasonwide > 0 || summary.Series > 0)
                DownloadAutomaticImages(GetBannerOnline(seriesID), seriesID, forceDownload);
        }

        static ImagesSummary GetSeriesImagesCounts(int seriesID)
        {
            return Task.Run(async () => await GetSeriesImagesCountsAsync(seriesID)).Result;
        }

        static async Task<ImagesSummary> GetSeriesImagesCountsAsync(int seriesID)
        {
            try
            {
                await CheckAuthorizationAsync();

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Series.GetImagesSummaryAsync(seriesID);
                return response.Data;
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetSeriesImagesCountsAsync(seriesID);
                    // suppress 404 and move on
                } else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return null;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            return null;
        }

        static async Task<Image[]> GetSeriesImagesAsync(int seriesID, KeyType type)
        {
            await CheckAuthorizationAsync();

            ImagesQuery query = new ImagesQuery
            {
                KeyType = type
            };
            TvDBRateLimiter.Instance.EnsureRate();
            try
            {
                var response = await client.Series.GetImagesAsync(seriesID, query);
                return response.Data;
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetSeriesImagesAsync(seriesID, type);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return new Image[] { };
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch
            {
                // ignore
            }
            return new Image[] { };
        }

        public static List<TvDB_ImageFanart> GetFanartOnline(int seriesID)
        {
            return Task.Run(async () => await GetFanartOnlineAsync(seriesID)).Result;
        }

       public static async Task<List<TvDB_ImageFanart>> GetFanartOnlineAsync(int seriesID)
        {
            List<int> validIDs = new List<int>();
            List<TvDB_ImageFanart> tvImages = new List<TvDB_ImageFanart>();
            try
            {
                Image[] images = await GetSeriesImagesAsync(seriesID, KeyType.Fanart);

                int count = 0;
                foreach (Image image in images)
                {
                    int id = image.Id ?? 0;
                    if (id == 0) continue;

                    if (count >= ServerSettings.Instance.TvDB.AutoFanartAmount) break;
                    TvDB_ImageFanart img;
                    using (var repo = Repo.Instance.TvDB_ImageFanart.BeginAddOrUpdate(() => Repo.Instance.TvDB_ImageFanart.GetByTvDBID(id)))
                    {
                        if (repo.Original == null)
                            repo.Entity.Enabled = 1;
                        repo.Entity.Populate(seriesID, image);
                        repo.Entity.Language = client.AcceptedLanguage;
                        img = repo.Commit();

                    }
                    tvImages.Add(img);
                    validIDs.Add(id);
                    count++;
                }

                // delete any images from the database which are no longer valid
                Repo.Instance.TvDB_ImageFanart.FindAndDelete(() =>
                {
                    List<TvDB_ImageFanart> todelete = new List<TvDB_ImageFanart>();
                    foreach (TvDB_ImageFanart img in Repo.Instance.TvDB_ImageFanart.GetBySeriesID(seriesID))
                    {
                        if (!validIDs.Contains(img.Id))
                            todelete.Add(img);
                    }

                    return todelete;
                });
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetFanartOnlineAsync(seriesID);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return tvImages;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in TVDBApiHelper.GetSeriesFanartOnlineAsync: {ex}");
            }

            return tvImages;
        }


        public static List<TvDB_ImagePoster> GetPosterOnline(int seriesID)
        {
            return Task.Run(async () => await GetPosterOnlineAsync(seriesID)).Result;
        }

        public static async Task<List<TvDB_ImagePoster>> GetPosterOnlineAsync(int seriesID)
        {
            List<int> validIDs = new List<int>();
            List<TvDB_ImagePoster> tvImages = new List<TvDB_ImagePoster>();

            try
            {
                Image[] posters = await GetSeriesImagesAsync(seriesID, KeyType.Poster);
                Image[] season = await GetSeriesImagesAsync(seriesID, KeyType.Season);

                Image[] images = posters.Concat(season).ToArray();

                int count = 0;
                foreach (Image image in images)
                {
                    int id = image.Id ?? 0;
                    if (id == 0) continue;

                    if (count >= ServerSettings.Instance.TvDB.AutoPostersAmount) break;
                    TvDB_ImagePoster img;
                    using (var repo = Repo.Instance.TvDB_ImagePoster.BeginAddOrUpdate(() => Repo.Instance.TvDB_ImagePoster.GetByTvDBID(id)))
                    {
                        if (repo.Original == null)
                            repo.Entity.Enabled = 1;
                        repo.Entity.Populate(seriesID, image);
                        repo.Entity.Language = client.AcceptedLanguage;
                        img = repo.Commit();
                    }
                    validIDs.Add(id);
                    tvImages.Add(img);
                    count++;
                }

                // delete any images from the database which are no longer valid
                Repo.Instance.TvDB_ImagePoster.FindAndDelete(() =>
                {
                    List<TvDB_ImagePoster> todelete = new List<TvDB_ImagePoster>();
                    foreach (TvDB_ImagePoster img in Repo.Instance.TvDB_ImagePoster.GetBySeriesID(seriesID))
                    {
                        if (!validIDs.Contains(img.Id))
                            todelete.Add(img);
                    }

                    return todelete;
                });
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetPosterOnlineAsync(seriesID);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return tvImages;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in TVDBApiHelper.GetPosterOnlineAsync: {ex}");
            }

            return tvImages;
        }

        public static List<TvDB_ImageWideBanner> GetBannerOnline(int seriesID)
        {
            return Task.Run(async () => await GetBannerOnlineAsync(seriesID)).Result;
        }

        public static async Task<List<TvDB_ImageWideBanner>> GetBannerOnlineAsync(int seriesID)
        {
            List<int> validIDs = new List<int>();
            List<TvDB_ImageWideBanner> tvImages = new List<TvDB_ImageWideBanner>();

            try
            {
                Image[] season = await GetSeriesImagesAsync(seriesID, KeyType.Seasonwide);
                Image[] series = await GetSeriesImagesAsync(seriesID, KeyType.Series);

                Image[] images = season.Concat(series).ToArray();

                int count = 0;
                foreach (Image image in images)
                {
                    int id = image.Id ?? 0;
                    if (id == 0) continue;

                    if (count >= ServerSettings.Instance.TvDB.AutoWideBannersAmount) break;
                    TvDB_ImageWideBanner img;
                    using (var repo = Repo.Instance.TvDB_ImageWideBanner.BeginAddOrUpdate(() => Repo.Instance.TvDB_ImageWideBanner.GetByTvDBID(id)))
                    {
                        if (repo.Original == null)
                            repo.Entity.Enabled = 1;
                        repo.Entity.Populate(seriesID, image);
                        repo.Entity.Language = client.AcceptedLanguage;
                        img = repo.Commit();
                    }
                    validIDs.Add(id);
                    tvImages.Add(img);
                    count++;
                }

                // delete any images from the database which are no longer valid
                Repo.Instance.TvDB_ImageWideBanner.FindAndDelete(() =>
                {
                    List<TvDB_ImageWideBanner> todelete = new List<TvDB_ImageWideBanner>();
                    foreach (TvDB_ImageWideBanner img in Repo.Instance.TvDB_ImageWideBanner.GetBySeriesID(seriesID))
                    {
                        if (!validIDs.Contains(img.Id))
                            todelete.Add(img);
                    }

                    return todelete;
                });

            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetBannerOnlineAsync(seriesID);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return tvImages;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in TVDBApiHelper.GetPosterOnlineAsync: {ex}");
            }

            return tvImages;
        }

        public static void DownloadAutomaticImages(List<TvDB_ImageFanart> images, int seriesID, bool forceDownload)
        {
            // find out how many images we already have locally
            int imageCount = Repo.Instance.TvDB_ImageFanart.GetBySeriesID(seriesID).Count(fanart =>
                !string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()));

            foreach (TvDB_ImageFanart img in images)
                if (ServerSettings.Instance.TvDB.AutoFanart && imageCount < ServerSettings.Instance.TvDB.AutoFanartAmount &&
                    !string.IsNullOrEmpty(img.GetFullImagePath()))
                {
                    bool fileExists = File.Exists(img.GetFullImagePath());
                    if (fileExists && !forceDownload) continue;
                    CommandQueue.Queue.Instance.Add(new CmdImageDownload(img.TvDB_ImageFanartID,
                        ImageEntityType.TvDB_FanArt, forceDownload));
                    imageCount++;
                }
                else
                {
                    //The TvDB_AutoFanartAmount point to download less images than its available
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (string.IsNullOrEmpty(img.GetFullImagePath()) || !File.Exists(img.GetFullImagePath()))
                        Repo.Instance.TvDB_ImageFanart.Delete(img);
                }
        }

        public static void DownloadAutomaticImages(List<TvDB_ImagePoster> images, int seriesID, bool forceDownload)
        {
            // find out how many images we already have locally
            int imageCount = Repo.Instance.TvDB_ImagePoster.GetBySeriesID(seriesID).Count(fanart =>
                !string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()));

            foreach (TvDB_ImagePoster img in images)
                if (ServerSettings.Instance.TvDB.AutoPosters && imageCount < ServerSettings.Instance.TvDB.AutoPostersAmount &&
                    !string.IsNullOrEmpty(img.GetFullImagePath()))
                {
                    bool fileExists = File.Exists(img.GetFullImagePath());
                    if (fileExists && !forceDownload) continue;
                    CommandQueue.Queue.Instance.Add(new CmdImageDownload(img.TvDB_ImagePosterID,
                        ImageEntityType.TvDB_Cover, forceDownload));
                    imageCount++;
                }
                else
                {
                    //The TvDB_AutoFanartAmount point to download less images than its available
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (string.IsNullOrEmpty(img.GetFullImagePath()) || !File.Exists(img.GetFullImagePath()))
                        Repo.Instance.TvDB_ImagePoster.Delete(img);
                }
        }

        public static void DownloadAutomaticImages(List<TvDB_ImageWideBanner> images, int seriesID, bool forceDownload)
        {
            // find out how many images we already have locally
            int imageCount = Repo.Instance.TvDB_ImageWideBanner.GetBySeriesID(seriesID).Count(banner =>
                !string.IsNullOrEmpty(banner.GetFullImagePath()) && File.Exists(banner.GetFullImagePath()));

            foreach (TvDB_ImageWideBanner img in images)
                if (ServerSettings.Instance.TvDB.AutoWideBanners && imageCount < ServerSettings.Instance.TvDB.AutoWideBannersAmount &&
                    !string.IsNullOrEmpty(img.GetFullImagePath()))
                {
                    bool fileExists = File.Exists(img.GetFullImagePath());
                    if (fileExists && !forceDownload) continue;
                    CommandQueue.Queue.Instance.Add(new CmdImageDownload(img.TvDB_ImageWideBannerID,
                        ImageEntityType.TvDB_Banner, forceDownload));
                    imageCount++;
                }
                else
                {
                    //The TvDB_AutoFanartAmount point to download less images than its available
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (string.IsNullOrEmpty(img.GetFullImagePath()) || !File.Exists(img.GetFullImagePath()))
                        Repo.Instance.TvDB_ImageWideBanner.Delete(img);
                }
        }

        public static List<BasicEpisode> GetEpisodesOnline(int seriesID)
        {
            return Task.Run(async () => await GetEpisodesOnlineAsync(seriesID)).Result;
        }

        static async Task<List<BasicEpisode>> GetEpisodesOnlineAsync(int seriesID)
        {
            List<BasicEpisode> apiEpisodes = new List<BasicEpisode>();
            try
            {
                await CheckAuthorizationAsync();

                var tasks = new List<Task<TvDbResponse<BasicEpisode[]>>>();
                TvDBRateLimiter.Instance.EnsureRate();
                var firstResponse = await client.Series.GetEpisodesAsync(seriesID, 1);

                for (int i = 2; i <= firstResponse.Links.Last; i++)
                {
                    TvDBRateLimiter.Instance.EnsureRate();
                    tasks.Add(client.Series.GetEpisodesAsync(seriesID, i));
                }

                var results = await Task.WhenAll(tasks);

                apiEpisodes = firstResponse.Data.Concat(results.SelectMany(x => x.Data)).ToList();
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetEpisodesOnlineAsync(seriesID);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return apiEpisodes;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in TvDBApiHelper.GetEpisodesOnlineAsync: {ex}");
            }

            return apiEpisodes;
        }

        public static TvDB_Episode UpdateEpisode(int episodeID, bool downloadImages, bool forceRefresh)
        {
            return QueueEpisodeImageDownloadAsync(episodeID, downloadImages, forceRefresh).Result;
        }

        static async Task<EpisodeRecord> GetEpisodeDetailsAsync(int episodeID)
        {
            try
            {
                await CheckAuthorizationAsync();

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Episodes.GetAsync(episodeID);
                return response.Data;
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetEpisodeDetailsAsync(episodeID);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return null;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in TvDBApiHelper.GetEpisodeDetailsAsync: {ex}");
            }

            return null;
        }

        public static async Task<TvDB_Episode> QueueEpisodeImageDownloadAsync(int tvDBEpisodeID, bool downloadImages, bool forceRefresh)
        {
            try
            {
                TvDB_Episode ep = Repo.Instance.TvDB_Episode.GetByTvDBID(tvDBEpisodeID);
                if (ep == null || forceRefresh)
                {
                    EpisodeRecord episode = await GetEpisodeDetailsAsync(tvDBEpisodeID);
                    if (episode == null)
                        return null;

                    using (var eup = Repo.Instance.TvDB_Episode.BeginAddOrUpdate(()=> Repo.Instance.TvDB_Episode.GetByTvDBID(tvDBEpisodeID)))
                    {
                        eup.Entity.Populate(episode);
                        ep=eup.Commit();
                    }
                }

                if (downloadImages)
                    if (!string.IsNullOrEmpty(ep.Filename))
                    {
                        bool fileExists = File.Exists(ep.GetFullImagePath());
                        if (!fileExists || forceRefresh)
                        {

                            CommandQueue.Queue.Instance.Add(new CmdImageDownload(ep.TvDB_EpisodeID,
                                    ImageEntityType.TvDB_Episode, forceRefresh));
                        }
                    }
                return ep;
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                    {
                        return await QueueEpisodeImageDownloadAsync(tvDBEpisodeID, downloadImages, forceRefresh);
                    }
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return null;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in TVDBHelper.GetEpisodes: {ex}");
            }
            return null;
        }

        public static void UpdateSeriesInfoAndImages(int seriesID, bool forceRefresh, bool downloadImages)
        {
            try
            {
                // update the series info
                TvDB_Series tvSeries = GetSeriesInfoOnline(seriesID, forceRefresh);
                if (tvSeries == null) return;

                if (downloadImages)
                    DownloadAutomaticImages(seriesID, forceRefresh);

                List<BasicEpisode> episodeItems = GetEpisodesOnline(seriesID);
                logger.Trace($"Found {episodeItems.Count} Episode nodes");

                List<int> existingEpIds = new List<int>();
                foreach (BasicEpisode item in episodeItems)
                {
                    if (!existingEpIds.Contains(item.Id))
                        existingEpIds.Add(item.Id);

                    string infoString = $"{tvSeries.SeriesName} - Episode {item.AbsoluteNumber?.ToString() ?? "X"}";
                    CommandQueue.Queue.Instance.Add(new CmdTvDBUpdateEpisode(item.Id, infoString, downloadImages, forceRefresh));
                }

                // get all the existing tvdb episodes, to see if any have been deleted
    
                Repo.Instance.TvDB_Episode.FindAndDelete(() =>
                {
                    List<TvDB_Episode> todelete = new List<TvDB_Episode>();
                    foreach (TvDB_Episode oldEp in Repo.Instance.TvDB_Episode.GetBySeriesID(seriesID))
                    {
                        if (!existingEpIds.Contains(oldEp.Id))
                            todelete.Add(oldEp);
                    }

                    return todelete;
                });



                // Not updating stats as it will happen with the episodes
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in TVDBHelper.GetEpisodes: {ex}");
            }
        }

        public static void LinkAniDBTvDBEpisode(int aniDBID, int tvDBID)
        {
            AniDB_Episode ep = Repo.Instance.AniDB_Episode.GetByEpisodeID(aniDBID);
            TvDB_Episode tvep = Repo.Instance.TvDB_Episode.GetByID(tvDBID);
            using (var upd = Repo.Instance.CrossRef_AniDB_Provider.BeginAddOrUpdate(() => Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIdAndProvider(CrossRefType.TvDB, ep.AnimeID, tvep.SeriesID.ToString())))
            {
                upd.Entity.EpisodesListOverride.AddOrUpdate(aniDBID, tvDBID.ToString(),tvep.SeasonNumber,ep.EpisodeNumber,ep.GetEpisodeTypeEnum(), MatchRating.UserVerified);
                if (upd.Entity.EpisodesListOverride.NeedPersitance)
                    upd.Commit();
            }

            using (var upd = Repo.Instance.AnimeEpisode.BeginAddOrUpdate(() => Repo.Instance.AnimeEpisode.GetByAniDBEpisodeID(aniDBID)))
            {
                SVR_AniDB_Anime.UpdateStatsByAnimeID(ep.AnimeID);
                upd.Commit();
            }

            logger.Trace($"Changed tvdb episode association: {aniDBID}");
        }

        // Removes all TVDB information from a series, bringing it back to a blank state.
        public static void RemoveLinkAniDBTvDB(int animeID, int tvDBID)
        {
            Repo.Instance.CrossRef_AniDB_Provider.FindAndDelete(() => Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIdAndProvider(CrossRefType.TvDB, animeID, tvDBID.ToString()));
            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
        }

        public static void ScanForMatches()
        {
            IReadOnlyList<SVR_AnimeSeries> allSeries = Repo.Instance.CrossRef_AniDB_Provider.GetSeriesWithoutLinks(CrossRefType.TvDB);
            
            foreach (SVR_AnimeSeries ser in allSeries)
            {
                CommandQueue.Queue.Instance.Add(new CmdTvDBSearchAnime(ser.AniDB_ID, false));
            }
        }

        public static void UpdateAllInfo(bool force)
        {
            CommandQueue.Queue.Instance.AddRange(Repo.Instance.CrossRef_AniDB_Provider.GetByType(CrossRefType.TvDB).Select(a => int.Parse(a.CrossRefID)).Distinct().Select(a => new CmdTvDBUpdateSeries(a, force)));
        }

        public static List<int> GetUpdatedSeriesList(long serverTime)
        {
            return GetUpdatedSeriesListAsync(serverTime).Result;
        }

        public static async Task<List<int>> GetUpdatedSeriesListAsync(long lasttimeseconds)
        {
            List<int> seriesList = new List<int>();
            try
            {
                // Unix timestamp is seconds past epoch
                DateTime lastUpdateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                lastUpdateTime = lastUpdateTime.AddSeconds(lasttimeseconds).ToLocalTime();

                // api limits this to a week at a time, so split it up
                List<(DateTime, DateTime)> spans = new List<(DateTime, DateTime)>();
                if (lastUpdateTime.AddDays(7) < DateTime.Now)
                {
                    DateTime time = lastUpdateTime;
                    while (time < DateTime.Now)
                    {
                        var nextTime = time.AddDays(7);
                        if (nextTime > DateTime.Now) nextTime = DateTime.Now;
                        spans.Add((time, nextTime));
                        time = time.AddDays(7);
                    }
                }
                else
                {
                    spans.Add((lastUpdateTime, DateTime.Now));
                }

                int i = 1;
                int count = spans.Count;
                foreach (var span in spans)
                {
                    TvDBRateLimiter.Instance.EnsureRate();
                    // this may take a while if you don't keep shoko running, so log info
                    logger.Info($"Getting updates from TvDB, part {i} of {count}");
                    i++;
                    var response = await client.Updates.GetAsync(span.Item1, span.Item2);

                    Update[] updates = response?.Data;
                    if (updates == null) continue;

                    seriesList.AddRange(updates.Where(item => item != null).Select(item => item.Id));
                }

                return seriesList;
            }
            catch (TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetUpdatedSeriesListAsync(lasttimeseconds);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return seriesList;
                logger.Error(exception,
                    $"TvDB returned an error code: {exception.StatusCode}\n        {exception.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in GetUpdatedSeriesList: {ex}");
            }
            return seriesList;
        }

        // ReSharper disable once RedundantAssignment
        public static string IncrementalTvDBUpdate(ref List<int> tvDBIDs, ref bool tvDBOnline)
        {
            // check if we have record of doing an automated update for the TvDB previously
            // if we have then we have kept a record of the server time and can do a delta update
            // otherwise we need to do a full update and keep a record of the time

            List<int> allTvDBIDs = new List<int>();
            tvDBIDs = tvDBIDs ?? new List<int>();
            tvDBOnline = true;

            try
            {
                // record the tvdb server time when we started
                // we record the time now instead of after we finish, to include any possible misses
                string currentTvDBServerTime = CurrentServerTime;
                if (currentTvDBServerTime.Length == 0)
                {
                    tvDBOnline = false;
                    return currentTvDBServerTime;
                }

                foreach (SVR_AnimeSeries ser in Repo.Instance.AnimeSeries.GetAll())
                {
                    List<SVR_CrossRef_AniDB_Provider> xrefs = ser.GetCrossRefTvDB();
                    if (xrefs == null) continue;

                    foreach (SVR_CrossRef_AniDB_Provider xref in xrefs)
                        if (!allTvDBIDs.Contains(Int32.Parse(xref.CrossRefID))) allTvDBIDs.Add(int.Parse(xref.CrossRefID));
                }

                // get the time we last did a TvDB update
                // if this is the first time it will be null
                // update the anidb info ever 24 hours

                ScheduledUpdate sched = Repo.Instance.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);

                string lastServerTime = string.Empty;
                if (sched != null)
                {
                    TimeSpan ts = DateTime.Now - sched.LastUpdate;
                    logger.Trace($"Last tvdb info update was {ts.TotalHours} hours ago");
                    if (!string.IsNullOrEmpty(sched.UpdateDetails))
                        lastServerTime = sched.UpdateDetails;

                    // the UpdateDetails field for this type will actually contain the last server time from
                    // TheTvDB that a full update was performed
                }


                // get a list of updates from TvDB since that time
                if (lastServerTime.Length > 0)
                {
                    if (!long.TryParse(lastServerTime, out long lasttimeseconds)) lasttimeseconds = -1;
                    if (lasttimeseconds < 0)
                    {
                        tvDBIDs = allTvDBIDs;
                        return CurrentServerTime;
                    }
                    List<int> seriesList = GetUpdatedSeriesList(lasttimeseconds);
                    logger.Trace($"{seriesList.Count} series have been updated since last download");
                    logger.Trace($"{allTvDBIDs.Count} TvDB series locally");

                    foreach (int id in seriesList)
                        if (allTvDBIDs.Contains(id)) tvDBIDs.Add(id);
                    logger.Trace($"{tvDBIDs.Count} TvDB local series have been updated since last download");
                }
                else
                {
                    // use the full list
                    tvDBIDs = allTvDBIDs;
                }

                return CurrentServerTime;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"IncrementalTvDBUpdate: {ex}");
                return string.Empty;
            }
        }
    }
}
