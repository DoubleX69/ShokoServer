﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories.Repos;
using Shoko.Server.Settings;


namespace Shoko.Server.Repositories
{
    public class Repo
    {
        static Repo _instance;
        public static Repo Instance => _instance;
        public static IProgress<InitProgress> ProgressMonitor { get; set; } = new DatabaseProgress();

        public static event EventHandler<InitProgress> ProgressEvent;

        // DECLARE THESE IN ORDER OF DEPENDENCY
        public JMMUserRepository JMMUser { get; private set; }
        public AuthTokensRepository AuthTokens { get; private set; }
        public CloudAccountRepository CloudAccount { get; private set; }
        public ImportFolderRepository ImportFolder { get; private set; }
        public AniDB_AnimeRepository AniDB_Anime { get; private set; }
        public AniDB_EpisodeRepository AniDB_Episode { get; private set; }
        public AniDB_FileRepository AniDB_File { get; private set; }
        public AniDB_Anime_TitleRepository AniDB_Anime_Title { get; private set; }
        public AniDB_Anime_TagRepository AniDB_Anime_Tag { get; private set; }
        public AniDB_TagRepository AniDB_Tag { get; private set; }
        public CustomTagRepository CustomTag { get; private set; }
        public CrossRef_CustomTagRepository CrossRef_CustomTag { get; private set; }
        public CrossRef_File_EpisodeRepository CrossRef_File_Episode { get; private set; }
        public CommandRequestRepository CommandRequest { get; private set; }
        public VideoLocal_PlaceRepository VideoLocal_Place { get; private set; }
        public VideoLocalRepository VideoLocal { get; private set; }
        public VideoLocal_UserRepository VideoLocal_User { get; private set; }
        public GroupFilterConditionRepository GroupFilterCondition { get; internal set; }
        public GroupFilterRepository GroupFilter { get; private set; }
        public AnimeEpisodeRepository AnimeEpisode { get; private set; }
        public AnimeEpisode_UserRepository AnimeEpisode_User { get; private set; }
        public AnimeSeriesRepository AnimeSeries { get; private set; }
        public AnimeSeries_UserRepository AnimeSeries_User { get; private set; }
        public AnimeGroupRepository AnimeGroup { get; private set; }
        public AnimeGroup_UserRepository AnimeGroup_User { get; private set; }
        public AniDB_VoteRepository AniDB_Vote { get; private set; }
        public TvDB_EpisodeRepository TvDB_Episode { get; private set; }
        public TvDB_SeriesRepository TvDB_Series { get; private set; }
        public CrossRef_AniDB_ProviderRepository CrossRef_AniDB_Provider { get; private set; }
        public TvDB_ImagePosterRepository TvDB_ImagePoster { get; private set; }
        public TvDB_ImageFanartRepository TvDB_ImageFanart { get; private set; }
        public TvDB_ImageWideBannerRepository TvDB_ImageWideBanner { get; private set; }


        public Trakt_ShowRepository Trakt_Show { get; private set; }
        public Trakt_SeasonRepository Trakt_Season { get; private set; }
        public Trakt_FriendRepository Trakt_Friend { get; private set; }
        public Trakt_EpisodeRepository Trakt_Episode { get; private set; }
        public ScheduledUpdateRepository ScheduledUpdate { get; private set; }
        public RenameScriptRepository RenameScript { get; private set; }
        public PlaylistRepository Playlist { get; private set; }
        public MovieDB_PosterRepository MovieDB_Poster { get; private set; }
        public MovieDB_FanartRepository MovieDB_Fanart { get; private set; }
        public MovieDB_MovieRepository MovieDb_Movie { get; private set; }
        public LanguageRepository Language { get; private set; }
        public IgnoreAnimeRepository IgnoreAnime { get; private set; }
        public FileNameHashRepository FileNameHash { get; private set; }
        public FileFfdshowPresetRepository FileFfdshowPreset { get; private set; }
        public DuplicateFileRepository DuplicateFile { get; private set; }

        public CrossRef_Subtitles_AniDB_FileRepository CrossRef_Subtitles_AniDB_File { get; private set; }

        public CrossRef_Languages_AniDB_FileRepository CrossRef_Languages_AniDB_File { get; private set; }

        public BookmarkedAnimeRepository BookmarkedAnime { get; private set; }
        public AniDB_SeiyuuRepository AniDB_Seiyuu { get; private set; }
        public AniDB_ReviewRepository AniDB_Review { get; private set; }
        public AniDB_ReleaseGroupRepository AniDB_ReleaseGroup { get; private set; }

        public AniDB_RecommendationRepository AniDB_Recommendation { get; private set; }
        public AniDB_MylistStatsRepository AniDB_MylistStats { get; private set; }            
        public AniDB_GroupStatusRepository AniDB_GroupStatus { get; private set; }
        public AniDB_CharacterRepository AniDB_Character { get; private set; }

        public AniDB_Character_SeiyuuRepository AniDB_Character_Seiyuu { get; private set; }

        public AniDB_Anime_SimilarRepository AniDB_Anime_Similar { get; private set; }

        public AniDB_Anime_ReviewRepository AniDB_Anime_Review { get; private set; }

        public AniDB_Anime_RelationRepository AniDB_Anime_Relation { get; private set; }

        public AniDB_Anime_DefaultImageRepository AniDB_Anime_DefaultImage { get; private set; }

        public AniDB_Anime_CharacterRepository AniDB_Anime_Character { get; private set; }

        public ScanRepository Scan { get; private set; }
        public ScanFileRepository ScanFile { get; private set; }
        public AniDB_Episode_TitleRepository AniDB_Episode_Title { get; internal set; }
        public AnimeStaffRepository AnimeStaff { get; internal set; }
        public AnimeCharacterRepository AnimeCharacter { get; internal set; }
        public AniDB_AnimeUpdateRepository AniDB_AnimeUpdate { get; internal set; }





        /************** Might need to be DEPRECATED **************/

        public CrossRef_Anime_StaffRepository CrossRef_Anime_Staff { get; internal set; }


        //AdHoc Repo
        public AdhocRepository Adhoc { get; private set; }
        internal ShokoContextProvider Provider { get; set; }

        private List<IRepository> _repos;

        private TU Register<TU, T>(DbSet<T> table) where T : class where TU : IRepository<T>, new()
        {
            TU repo = new TU();
            repo.SetContext(Provider,table);
            repo.SwitchCache(CachedRepos.Contains(table.GetName()));
            _repos.Add(repo);
            return repo;
        }

        public HashSet<string> CachedRepos = new HashSet<string>();
        public HashSet<string> DefaultCached = new HashSet<string>()
        {
            nameof(AniDB_Anime_DefaultImage), nameof(AniDB_Anime_Tag), nameof(AniDB_Anime_Title),
            nameof(AniDB_Anime), nameof(AniDB_Episode_Title), nameof(AniDB_Episode),
            nameof(AniDB_File), nameof(AniDB_Tag), nameof(AniDB_Vote), nameof(AnimeCharacter),
            nameof(AnimeEpisode_User), nameof(AnimeEpisode), nameof(AnimeGroup_User),
            nameof(AnimeGroup), nameof(AnimeSeries_User), nameof(AnimeSeries), nameof(AnimeStaff),
            nameof(AuthTokens), nameof(CloudAccount), nameof(CrossRef_AniDB_Provider),
            nameof(CrossRef_Anime_Staff),
            nameof(CrossRef_CustomTag), nameof(CrossRef_File_Episode), nameof(CustomTag),
            nameof(GroupFilter), nameof(ImportFolder), nameof(JMMUser), nameof(TvDB_Episode),
            nameof(TvDB_ImageFanart), nameof(TvDB_ImagePoster), nameof(TvDB_ImageWideBanner),
            nameof(TvDB_Series), nameof(VideoLocal_Place), nameof(VideoLocal_User), nameof(VideoLocal),
        }; //TODO Set Default

        public void Init(ShokoContextProvider provider, HashSet<string> cachedRepos)
        {
            ShokoContext db = provider.GetContext();
            db.Database.Migrate();
            //db.Database.EnsureCreated();
            _instance = this;

            _repos = new List<IRepository>();
            if (cachedRepos != null)
                CachedRepos = cachedRepos;
            Provider = provider;

            JMMUser = Register<JMMUserRepository, SVR_JMMUser>(db.JMMUsers);
            AuthTokens = Register<AuthTokensRepository, AuthTokens>(db.AuthTokens);
            CloudAccount = Register<CloudAccountRepository, SVR_CloudAccount>(db.CloudAccounts);
            ImportFolder = Register<ImportFolderRepository, SVR_ImportFolder>(db.ImportFolders);
            AniDB_Anime = Register<AniDB_AnimeRepository, SVR_AniDB_Anime>(db.AniDB_Animes);
            AniDB_Episode = Register<AniDB_EpisodeRepository, AniDB_Episode>(db.AniDB_Episodes);
            AniDB_File = Register<AniDB_FileRepository, SVR_AniDB_File>(db.AniDB_Files);
            AniDB_Anime_Title = Register<AniDB_Anime_TitleRepository, AniDB_Anime_Title>(db.AniDB_Anime_Titles);
            AniDB_Anime_Tag = Register<AniDB_Anime_TagRepository, AniDB_Anime_Tag>(db.AniDB_Anime_Tags);
            AniDB_Tag = Register<AniDB_TagRepository, AniDB_Tag>(db.AniDB_Tags);
            AniDB_Episode_Title = Register<AniDB_Episode_TitleRepository, AniDB_Episode_Title>(db.AniDB_Episode_Title);
            CustomTag = Register<CustomTagRepository, CustomTag>(db.CustomTags);
            CrossRef_CustomTag = Register<CrossRef_CustomTagRepository, CrossRef_CustomTag>(db.CrossRef_CustomTags);
            CrossRef_File_Episode = Register<CrossRef_File_EpisodeRepository, CrossRef_File_Episode>(db.CrossRef_File_Episodes);
            CommandRequest = Register<CommandRequestRepository, CommandRequest>(db.CommandRequests);
            VideoLocal_Place = Register<VideoLocal_PlaceRepository, SVR_VideoLocal_Place>(db.VideoLocal_Places);
            VideoLocal = Register<VideoLocalRepository, SVR_VideoLocal>(db.VideoLocals);
            VideoLocal_User = Register<VideoLocal_UserRepository, VideoLocal_User>(db.VideoLocal_Users);
            GroupFilterCondition = Register<GroupFilterConditionRepository, GroupFilterCondition>(db.GroupFilterConditions);
            GroupFilter = Register<GroupFilterRepository, SVR_GroupFilter>(db.GroupFilters);
            AnimeEpisode = Register<AnimeEpisodeRepository, SVR_AnimeEpisode>(db.AnimeEpisodes);
            AnimeEpisode_User = Register<AnimeEpisode_UserRepository, SVR_AnimeEpisode_User>(db.AnimeEpisode_Users);
            AnimeSeries = Register<AnimeSeriesRepository, SVR_AnimeSeries>(db.AnimeSeries);
            AnimeSeries_User = Register<AnimeSeries_UserRepository, SVR_AnimeSeries_User>(db.AnimeSeries_Users );
            AnimeGroup = Register<AnimeGroupRepository, SVR_AnimeGroup>(db.AnimeGroups);
            AnimeGroup_User = Register<AnimeGroup_UserRepository, SVR_AnimeGroup_User>(db.AnimeGroup_Users);
            AniDB_Vote = Register<AniDB_VoteRepository, AniDB_Vote>(db.AniDB_Votes);
            TvDB_Episode = Register<TvDB_EpisodeRepository, TvDB_Episode>(db.TvDB_Episodes);
            TvDB_Series = Register<TvDB_SeriesRepository, TvDB_Series>(db.TvDB_Series);
            CrossRef_AniDB_Provider = Register<CrossRef_AniDB_ProviderRepository, SVR_CrossRef_AniDB_Provider>(db.CrossRef_AniDB_Provider);
            TvDB_ImagePoster = Register<TvDB_ImagePosterRepository, TvDB_ImagePoster>(db.TvDB_ImagePosters);
            TvDB_ImageFanart = Register<TvDB_ImageFanartRepository, TvDB_ImageFanart>(db.TvDB_ImageFanarts);
            TvDB_ImageWideBanner = Register<TvDB_ImageWideBannerRepository, TvDB_ImageWideBanner>(db.TvDB_ImageWideBanners);


            Trakt_Show = Register<Trakt_ShowRepository, Trakt_Show>(db.Trakt_Shows);
            Trakt_Season = Register<Trakt_SeasonRepository, Trakt_Season>(db.Trakt_Seasons);
            Trakt_Friend = Register<Trakt_FriendRepository, Trakt_Friend>(db.Trakt_Friends);
            Trakt_Episode = Register<Trakt_EpisodeRepository, Trakt_Episode>(db.Trakt_Episodes);
            ScheduledUpdate = Register<ScheduledUpdateRepository, ScheduledUpdate>(db.ScheduledUpdates);
            RenameScript = Register<RenameScriptRepository, RenameScript>(db.RenameScripts);
            Playlist = Register<PlaylistRepository, Playlist>(db.Playlists);
            MovieDB_Poster = Register<MovieDB_PosterRepository, MovieDB_Poster>(db.MovieDB_Posters);
            MovieDB_Fanart = Register<MovieDB_FanartRepository, MovieDB_Fanart>(db.MovieDB_Fanarts);
            MovieDb_Movie = Register<MovieDB_MovieRepository, MovieDB_Movie>(db.MovieDB_Movies);
            Language = Register<LanguageRepository, Language>(db.Languages);
            IgnoreAnime = Register<IgnoreAnimeRepository, IgnoreAnime>(db.IgnoreAnimes);
            FileNameHash = Register<FileNameHashRepository, FileNameHash>(db.FileNameHashes);
            FileFfdshowPreset = Register<FileFfdshowPresetRepository, FileFfdshowPreset>(db.FileFfdshowPresets);
            DuplicateFile = Register<DuplicateFileRepository, DuplicateFile>(db.DuplicateFiles);
            CrossRef_Subtitles_AniDB_File = Register<CrossRef_Subtitles_AniDB_FileRepository, CrossRef_Subtitles_AniDB_File>(db.CrossRef_Subtitles_AniDB_Files);
            CrossRef_Languages_AniDB_File = Register<CrossRef_Languages_AniDB_FileRepository, CrossRef_Languages_AniDB_File>(db.CrossRef_Languages_AniDB_Files);
            BookmarkedAnime = Register<BookmarkedAnimeRepository, BookmarkedAnime>(db.BookmarkedAnimes);
            AniDB_Seiyuu = Register<AniDB_SeiyuuRepository, AniDB_Seiyuu>(db.AniDB_Seiyuus);
            AniDB_Review = Register<AniDB_ReviewRepository, AniDB_Review>(db.AniDB_Reviews);
            AniDB_ReleaseGroup = Register<AniDB_ReleaseGroupRepository, AniDB_ReleaseGroup>(db.AniDB_ReleaseGroups);
            AniDB_Recommendation = Register<AniDB_RecommendationRepository, AniDB_Recommendation>(db.AniDB_Recommendations);
            AniDB_MylistStats = Register<AniDB_MylistStatsRepository, AniDB_MylistStats>(db.AniDB_MylistStats);
            AniDB_GroupStatus = Register<AniDB_GroupStatusRepository, AniDB_GroupStatus>(db.AniDB_GroupStatus);
            AniDB_Character = Register<AniDB_CharacterRepository, AniDB_Character>(db.AniDB_Characters);

            AniDB_Character_Seiyuu = Register<AniDB_Character_SeiyuuRepository, AniDB_Character_Seiyuu>(db.AniDB_Character_Seiyuus);

            AniDB_Anime_Similar = Register<AniDB_Anime_SimilarRepository, AniDB_Anime_Similar>(db.AniDB_Anime_Similars);

            AniDB_Anime_Review = Register<AniDB_Anime_ReviewRepository, AniDB_Anime_Review>(db.AniDB_Anime_Reviews);

            AniDB_Anime_Relation = Register<AniDB_Anime_RelationRepository, AniDB_Anime_Relation>(db.AniDB_Anime_Relations);

            AniDB_Anime_DefaultImage = Register<AniDB_Anime_DefaultImageRepository, AniDB_Anime_DefaultImage>(db.AniDB_Anime_DefaultImages);

            AniDB_Anime_Character = Register<AniDB_Anime_CharacterRepository, AniDB_Anime_Character>(db.AniDB_Anime_Characters);

            Scan = Register<ScanRepository, Scan>(db.Scans);
            ScanFile = Register<ScanFileRepository, ScanFile>(db.ScanFiles);
            AnimeStaff = Register<AnimeStaffRepository, AnimeStaff>(db.AnimeStaff);
            AnimeCharacter = Register<AnimeCharacterRepository, AnimeCharacter>(db.AnimeCharacter);
            AniDB_AnimeUpdate = Register<AniDB_AnimeUpdateRepository, AniDB_AnimeUpdate>(db.AniDB_AnimeUpdate);


            /************** Might need to be DEPRECATED **************/
            CrossRef_Anime_Staff = Register<CrossRef_Anime_StaffRepository, CrossRef_Anime_Staff>(db.CrossRef_Anime_Staff);
            Adhoc = new AdhocRepository();
        }
        
        public void SetCache(HashSet<string> cachedRepos)
        {
            CachedRepos = cachedRepos != null ? cachedRepos : new HashSet<string>();
            _repos.ForEach(r=>r.SwitchCache(CachedRepos.Contains(r.Name)));
        }

        internal ShokoContextProvider GetProvider() 
        {
            string connStr;
            switch(ServerSettings.Instance.Database.Type)
            {
                case DatabaseTypes.MySql:
                    connStr = $"Server={ServerSettings.Instance.Database.Hostname};Database={ServerSettings.Instance.Database.Schema};User ID={ServerSettings.Instance.Database.Username};Password={ServerSettings.Instance.Database.Password};Default Command Timeout=3600";
                    break;
                case DatabaseTypes.SqlServer:
                    connStr = $"Server={ServerSettings.Instance.Database.Hostname};Database={ServerSettings.Instance.Database.Schema};UID={ServerSettings.Instance.Database.Username};PWD={ServerSettings.Instance.Database.Password};";
                    break;
                case DatabaseTypes.Sqlite:
                default:
                    if (!Directory.Exists(ServerSettings.Instance.Database.MySqliteDirectory)) Directory.CreateDirectory(ServerSettings.Instance.Database.MySqliteDirectory);
                    connStr = $"data source={Path.Combine(ServerSettings.Instance.Database.MySqliteDirectory, "JMMServer.db3")}"; //";useutf16encoding=True";
                    break;
            }
            return new ShokoContextProvider(ServerSettings.Instance.Database.Type, connStr);
        }

        internal bool Start()
        {
            Init(GetProvider(), DefaultCached);

            return true;
        }

        internal bool Migrate()
        {
            //run any migrations.


            return true;
        }

        internal bool DoInit(IProgress<InitProgress> progress = null, int batchSize = 20)
        {
            _repos.ForEach(a => a.PreInit(progress, batchSize));
            _repos.ForEach(a => a.PostInit(progress, batchSize));
            return true;
        }


        public class DatabaseProgress : IProgress<InitProgress>
        {
            public void Report(InitProgress value)
            {
                ProgressEvent?.Invoke(this, value);
            }
        }
    }
}