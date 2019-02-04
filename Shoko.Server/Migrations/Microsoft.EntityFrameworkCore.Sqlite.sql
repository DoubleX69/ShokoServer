PRAGMA foreign_keys=OFF;
CREATE TABLE IF NOT EXISTS AniDB_Anime_Category ( AniDB_Anime_CategoryID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, CategoryID int NOT NULL, Weighting int NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Anime_Character ( AniDB_Anime_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, CharID int NOT NULL, CharType text NOT NULL, EpisodeListRaw text NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Anime_Relation ( AniDB_Anime_RelationID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, RelatedAnimeID int NOT NULL, RelationType text NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Anime_Review ( AniDB_Anime_ReviewID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, ReviewID int NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Anime_Similar ( AniDB_Anime_SimilarID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, SimilarAnimeID int NOT NULL, Approval int NOT NULL, Total int NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Anime_Tag ( AniDB_Anime_TagID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, TagID int NOT NULL, Approval int NOT NULL , Weight int NULL);
CREATE TABLE IF NOT EXISTS AniDB_Anime_Title ( AniDB_Anime_TitleID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, TitleType text NOT NULL, Language text NOT NULL, Title text NULL );
CREATE TABLE IF NOT EXISTS AniDB_Category ( AniDB_CategoryID INTEGER PRIMARY KEY AUTOINCREMENT, CategoryID int NOT NULL, ParentID int NOT NULL, IsHentai int NOT NULL, CategoryName text NOT NULL, CategoryDescription text NOT NULL  );
CREATE TABLE IF NOT EXISTS AniDB_Character ( AniDB_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, CharID int NOT NULL, CharName text NOT NULL, PicName text NOT NULL, CharKanjiName text NOT NULL, CharDescription text NOT NULL, CreatorListRaw text NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Character_Seiyuu ( AniDB_Character_SeiyuuID INTEGER PRIMARY KEY AUTOINCREMENT, CharID int NOT NULL, SeiyuuID int NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Seiyuu ( AniDB_SeiyuuID INTEGER PRIMARY KEY AUTOINCREMENT, SeiyuuID int NOT NULL, SeiyuuName text NOT NULL, PicName text NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_File ( AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, FileID int NOT NULL, Hash text NOT NULL, AnimeID int NOT NULL, GroupID int NOT NULL, File_Source text NOT NULL, File_AudioCodec text NOT NULL, File_VideoCodec text NOT NULL, File_VideoResolution text NOT NULL, File_FileExtension text NOT NULL, File_LengthSeconds int NOT NULL, File_Description text NOT NULL, File_ReleaseDate int NOT NULL, Anime_GroupName text NOT NULL, Anime_GroupNameShort text NOT NULL, Episode_Rating int NOT NULL, Episode_Votes int NOT NULL, DateTimeUpdated timestamp NOT NULL, IsWatched int NOT NULL, WatchedDate timestamp NULL, CRC text NOT NULL, MD5 text NOT NULL, SHA1 text NOT NULL, FileName text NOT NULL, FileSize INTEGER NOT NULL , FileVersion int NULL, IsCensored int NULL, IsDeprecated int NULL, InternalVersion int NULL, IsChaptered INT NOT NULL DEFAULT -1);
CREATE TABLE IF NOT EXISTS AniDB_GroupStatus ( AniDB_GroupStatusID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, GroupID int NOT NULL, GroupName text NOT NULL, CompletionState int NOT NULL, LastEpisodeNumber int NOT NULL, Rating int NOT NULL, Votes int NOT NULL, EpisodeRange text NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_ReleaseGroup ( AniDB_ReleaseGroupID INTEGER PRIMARY KEY AUTOINCREMENT, GroupID int NOT NULL, Rating int NOT NULL, Votes int NOT NULL, AnimeCount int NOT NULL, FileCount int NOT NULL, GroupName text NOT NULL, GroupNameShort text NOT NULL, IRCChannel text NOT NULL, IRCServer text NOT NULL, URL text NOT NULL, Picname text NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Review ( AniDB_ReviewID INTEGER PRIMARY KEY AUTOINCREMENT, ReviewID int NOT NULL, AuthorID int NOT NULL, RatingAnimation int NOT NULL, RatingSound int NOT NULL, RatingStory int NOT NULL, RatingCharacter int NOT NULL, RatingValue int NOT NULL, RatingEnjoyment int NOT NULL, ReviewText text NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Tag ( AniDB_TagID INTEGER PRIMARY KEY AUTOINCREMENT, TagID int NOT NULL, Spoiler int NOT NULL, LocalSpoiler int NOT NULL, GlobalSpoiler int NOT NULL, TagName text NOT NULL, TagCount int NOT NULL, TagDescription text NOT NULL );
CREATE TABLE IF NOT EXISTS AnimeEpisode( AnimeEpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeSeriesID int NOT NULL, AniDB_EpisodeID int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeCreated timestamp NOT NULL , PlexContractVersion int NOT NULL DEFAULT 0, PlexContractBlob BLOB NULL, PlexContractSize int NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS AnimeGroup ( AnimeGroupID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeGroupParentID int NULL, GroupName text NOT NULL, Description text NULL, IsManuallyNamed int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeCreated timestamp NOT NULL, SortName text NOT NULL, MissingEpisodeCount int NOT NULL, MissingEpisodeCountGroups int NOT NULL, OverrideDescription int NOT NULL, EpisodeAddedDate timestamp NULL , DefaultAnimeSeriesID int NULL, ContractVersion int NOT NULL DEFAULT 0, LatestEpisodeAirDate timestamp NULL, ContractBlob BLOB NULL, ContractSize int NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS AnimeSeries ( AnimeSeriesID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeGroupID int NOT NULL, AniDB_ID int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeCreated timestamp NOT NULL, DefaultAudioLanguage text NULL, DefaultSubtitleLanguage text NULL, MissingEpisodeCount int NOT NULL, MissingEpisodeCountGroups int NOT NULL, LatestLocalEpisodeNumber int NOT NULL, EpisodeAddedDate timestamp NULL , SeriesNameOverride text, DefaultFolder text NULL, ContractVersion int NOT NULL DEFAULT 0, LatestEpisodeAirDate timestamp NULL, ContractBlob BLOB NULL, ContractSize int NOT NULL DEFAULT 0, AirsOn TEXT NULL);
CREATE TABLE IF NOT EXISTS CommandRequest ( CommandRequestID INTEGER PRIMARY KEY AUTOINCREMENT, Priority int NOT NULL, CommandType int NOT NULL, CommandID text NOT NULL, CommandDetails text NOT NULL, DateTimeUpdated timestamp NOT NULL );
CREATE TABLE IF NOT EXISTS CrossRef_AniDB_Other( CrossRef_AniDB_OtherID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, CrossRefID text NOT NULL, CrossRefSource int NOT NULL, CrossRefType int NOT NULL );
CREATE TABLE IF NOT EXISTS CrossRef_File_Episode ( CrossRef_File_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, Hash text NULL, FileName text NOT NULL, FileSize INTEGER NOT NULL, CrossRefSource int NOT NULL, AnimeID int NOT NULL, EpisodeID int NOT NULL, Percentage int NOT NULL, EpisodeOrder int NOT NULL );
CREATE TABLE IF NOT EXISTS CrossRef_Languages_AniDB_File ( CrossRef_Languages_AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, FileID int NOT NULL, LanguageID int NOT NULL );
CREATE TABLE IF NOT EXISTS CrossRef_Subtitles_AniDB_File ( CrossRef_Subtitles_AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, FileID int NOT NULL, LanguageID int NOT NULL );
CREATE TABLE IF NOT EXISTS FileNameHash ( FileNameHashID INTEGER PRIMARY KEY AUTOINCREMENT, FileName text NOT NULL, FileSize INTEGER NOT NULL, Hash text NOT NULL, DateTimeUpdated timestamp NOT NULL );
CREATE TABLE IF NOT EXISTS Language ( LanguageID INTEGER PRIMARY KEY AUTOINCREMENT, LanguageName text NOT NULL );
CREATE TABLE IF NOT EXISTS ImportFolder ( ImportFolderID INTEGER PRIMARY KEY AUTOINCREMENT, ImportFolderType int NOT NULL, ImportFolderName text NOT NULL, ImportFolderLocation text NOT NULL, IsDropSource int NOT NULL, IsDropDestination int NOT NULL , IsWatched int NOT NULL DEFAULT 1, CloudID int NULL);
CREATE TABLE IF NOT EXISTS ScheduledUpdate( ScheduledUpdateID INTEGER PRIMARY KEY AUTOINCREMENT,  UpdateType int NOT NULL, LastUpdate timestamp NOT NULL, UpdateDetails text NOT NULL );
CREATE TABLE IF NOT EXISTS DuplicateFile ( DuplicateFileID INTEGER PRIMARY KEY AUTOINCREMENT, FilePathFile1 text NOT NULL, FilePathFile2 text NOT NULL, ImportFolderIDFile1 int NOT NULL, ImportFolderIDFile2 int NOT NULL, Hash text NOT NULL, DateTimeUpdated timestamp NOT NULL );
CREATE TABLE IF NOT EXISTS GroupFilter( GroupFilterID INTEGER PRIMARY KEY AUTOINCREMENT, GroupFilterName text NOT NULL, ApplyToSeries int NOT NULL, BaseCondition int NOT NULL, SortingCriteria text , Locked int NULL, FilterType int NOT NULL DEFAULT 1, GroupsIdsVersion int NOT NULL DEFAULT 0, GroupsIdsString text NULL, GroupConditionsVersion int NOT NULL DEFAULT 0, GroupConditions text NULL, ParentGroupFilterID int NULL, InvisibleInClients int NOT NULL DEFAULT 0, SeriesIdsVersion int NOT NULL DEFAULT 0, SeriesIdsString text NULL);
INSERT OR IGNORE INTO GroupFilter VALUES(1,'Continue Watching (SYSTEM)',0,1,'4;2',1,2,3,'{}',1,'[{"GroupFilterConditionID":0,"GroupFilterID":1,"ConditionType":28,"ConditionOperator":1,"ConditionParameter":""},{"GroupFilterConditionID":0,"GroupFilterID":1,"ConditionType":3,"ConditionOperator":1,"ConditionParameter":""}]',NULL,0,2,'{}');
INSERT OR IGNORE INTO GroupFilter VALUES(2,'All',0,1,'5;1',1,4,3,'{}',1,'[]',NULL,0,2,'{}');
INSERT OR IGNORE INTO GroupFilter VALUES(3,'Tags',0,1,'13;1',1,24,3,'{}',1,'[]',NULL,0,2,'{}');
INSERT OR IGNORE INTO GroupFilter VALUES(4,'Years',0,1,'13;1',1,40,3,'{}',1,'[]',NULL,0,2,'{}');
INSERT OR IGNORE INTO GroupFilter VALUES(5,'Seasons',0,1,'13;1',1,72,3,'{}',1,'[]',NULL,0,2,'{}');
CREATE TABLE IF NOT EXISTS GroupFilterCondition( GroupFilterConditionID INTEGER PRIMARY KEY AUTOINCREMENT, GroupFilterID int NOT NULL, ConditionType int NOT NULL, ConditionOperator int NOT NULL, ConditionParameter text NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Vote ( AniDB_VoteID INTEGER PRIMARY KEY AUTOINCREMENT, EntityID int NOT NULL, VoteValue int NOT NULL, VoteType int NOT NULL );
CREATE TABLE IF NOT EXISTS TvDB_ImageFanart ( TvDB_ImageFanartID INTEGER PRIMARY KEY AUTOINCREMENT, Id integer NOT NULL, SeriesID integer NOT NULL, BannerPath text, BannerType text, BannerType2 text, Colors text, Language text, ThumbnailPath text, VignettePath text, Enabled integer NOT NULL, Chosen INTEGER NULL);
CREATE TABLE IF NOT EXISTS TvDB_ImageWideBanner ( TvDB_ImageWideBannerID INTEGER PRIMARY KEY AUTOINCREMENT, Id integer NOT NULL, SeriesID integer NOT NULL, BannerPath text, BannerType text, BannerType2 text, Language text, Enabled integer NOT NULL, SeasonNumber integer);
CREATE TABLE IF NOT EXISTS TvDB_ImagePoster ( TvDB_ImagePosterID INTEGER PRIMARY KEY AUTOINCREMENT, Id integer NOT NULL, SeriesID integer NOT NULL, BannerPath text, BannerType text, BannerType2 text, Language text, Enabled integer NOT NULL, SeasonNumber integer);
CREATE TABLE IF NOT EXISTS TvDB_Series( TvDB_SeriesID INTEGER PRIMARY KEY AUTOINCREMENT, SeriesID integer NOT NULL, Overview text, SeriesName text, Status text, Banner text, Fanart text, Poster text, Lastupdated text, Rating INT NULL);
CREATE TABLE IF NOT EXISTS AniDB_Anime_DefaultImage ( AniDB_Anime_DefaultImageID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, ImageParentID int NOT NULL, ImageParentType int NOT NULL, ImageType int NOT NULL );
CREATE TABLE IF NOT EXISTS MovieDB_Movie( MovieDB_MovieID INTEGER PRIMARY KEY AUTOINCREMENT, MovieId int NOT NULL, MovieName text, OriginalName text, Overview text , Rating INT NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS MovieDB_Poster( MovieDB_PosterID INTEGER PRIMARY KEY AUTOINCREMENT, ImageID text, MovieId int NOT NULL, ImageType text, ImageSize text,  URL text,  ImageWidth int NOT NULL,  ImageHeight int NOT NULL,  Enabled int NOT NULL );
CREATE TABLE IF NOT EXISTS MovieDB_Fanart( MovieDB_FanartID INTEGER PRIMARY KEY AUTOINCREMENT, ImageID text, MovieId int NOT NULL, ImageType text, ImageSize text,  URL text,  ImageWidth int NOT NULL,  ImageHeight int NOT NULL,  Enabled int NOT NULL );
CREATE TABLE IF NOT EXISTS JMMUser( JMMUserID INTEGER PRIMARY KEY AUTOINCREMENT, Username text, Password text, IsAdmin int NOT NULL, IsAniDBUser int NOT NULL, IsTraktUser int NOT NULL, HideCategories text , CanEditServerSettings int NULL, PlexUsers text NULL, PlexToken text NULL);
INSERT OR IGNORE INTO JMMUser VALUES(1,'Default','',1,1,1,'',1,NULL,NULL);
INSERT OR IGNORE INTO JMMUser VALUES(2,'Family Friendly','',1,1,1,'ecchi,nudity,sex,sexual abuse,horror,erotic game,incest,18 restricted',1,NULL,NULL);
CREATE TABLE IF NOT EXISTS Trakt_Episode( Trakt_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, Trakt_ShowID int NOT NULL, Season int NOT NULL, EpisodeNumber int NOT NULL, Title text, URL text, Overview text, EpisodeImage text , TraktID int NULL);
CREATE TABLE IF NOT EXISTS Trakt_Show( Trakt_ShowID INTEGER PRIMARY KEY AUTOINCREMENT, TraktID text, Title text, Year text, URL text, Overview text, TvDB_ID int NULL );
CREATE TABLE IF NOT EXISTS Trakt_Season( Trakt_SeasonID INTEGER PRIMARY KEY AUTOINCREMENT, Trakt_ShowID int NOT NULL, Season int NOT NULL, URL text );
CREATE TABLE IF NOT EXISTS CrossRef_AniDB_Trakt( CrossRef_AniDB_TraktID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, TraktID text, TraktSeasonNumber int NOT NULL, CrossRefSource int NOT NULL );
CREATE TABLE IF NOT EXISTS AnimeEpisode_User( AnimeEpisode_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeEpisodeID int NOT NULL, AnimeSeriesID int NOT NULL, WatchedDate timestamp NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL , ContractVersion int NOT NULL DEFAULT 0, ContractBlob BLOB NULL, ContractSize int NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS AnimeSeries_User( AnimeSeries_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeSeriesID int NOT NULL, UnwatchedEpisodeCount int NOT NULL, WatchedEpisodeCount int NOT NULL, WatchedDate timestamp NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL , PlexContractVersion int NOT NULL DEFAULT 0, PlexContractBlob BLOB NULL, PlexContractSize int NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS AnimeGroup_User( AnimeGroup_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeGroupID int NOT NULL, IsFave int NOT NULL, UnwatchedEpisodeCount int NOT NULL, WatchedEpisodeCount int NOT NULL, WatchedDate timestamp NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL , PlexContractVersion int NOT NULL DEFAULT 0, PlexContractBlob BLOB NULL, PlexContractSize int NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS IgnoreAnime( IgnoreAnimeID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeID int NOT NULL, IgnoreType int NOT NULL);
CREATE TABLE IF NOT EXISTS Trakt_Friend( Trakt_FriendID INTEGER PRIMARY KEY AUTOINCREMENT, Username text NOT NULL, FullName text NULL, Gender text NULL, Age text NULL, Location text NULL, About text NULL, Joined int NOT NULL, Avatar text NULL, Url text NULL, LastAvatarUpdate timestamp NOT NULL);
CREATE TABLE IF NOT EXISTS CrossRef_AniDB_MAL( CrossRef_AniDB_MALID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle text, StartEpisodeType int NOT NULL, StartEpisodeNumber int NOT NULL, CrossRefSource int NOT NULL );
CREATE TABLE IF NOT EXISTS Playlist( PlaylistID INTEGER PRIMARY KEY AUTOINCREMENT, PlaylistName text, PlaylistItems text, DefaultPlayOrder int NOT NULL, PlayWatched int NOT NULL, PlayUnwatched int NOT NULL );
CREATE TABLE IF NOT EXISTS BookmarkedAnime( BookmarkedAnimeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, Priority int NOT NULL, Notes text, Downloading int NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_MylistStats( AniDB_MylistStatsID INTEGER PRIMARY KEY AUTOINCREMENT, Animes int NOT NULL, Episodes int NOT NULL, Files int NOT NULL, SizeOfFiles INTEGER NOT NULL, AddedAnimes int NOT NULL, AddedEpisodes int NOT NULL, AddedFiles int NOT NULL, AddedGroups int NOT NULL, LeechPct int NOT NULL, GloryPct int NOT NULL, ViewedPct int NOT NULL, MylistPct int NOT NULL, ViewedMylistPct int NOT NULL, EpisodesViewed int NOT NULL, Votes int NOT NULL, Reviews int NOT NULL, ViewiedLength int NOT NULL );
CREATE TABLE IF NOT EXISTS FileFfdshowPreset( FileFfdshowPresetID INTEGER PRIMARY KEY AUTOINCREMENT, Hash int NOT NULL, FileSize INTEGER NOT NULL, Preset text );
CREATE TABLE IF NOT EXISTS RenameScript( RenameScriptID INTEGER PRIMARY KEY AUTOINCREMENT, ScriptName text, Script text, IsEnabledOnImport int NOT NULL , RenamerType TEXT NOT NULL DEFAULT 'Legacy', ExtraData TEXT);
INSERT OR IGNORE INTO RenameScript VALUES(1,'Default',replace(replace('// Sample Output: [Coalgirls]_Highschool_of_the_Dead_-_01_(1920x1080_Blu-ray_H264)_[90CC6DC1].mkv
// Sub group name
DO ADD ''[%grp] ''
// Anime Name, use english name if it exists, otherwise use the Romaji name
IF I(eng) DO ADD ''%eng ''
IF I(ann);I(!eng) DO ADD ''%ann ''
// Episode Number, don''t use episode number for movies
IF T(!Movie) DO ADD ''- %enr''
// If the file version is v2 or higher add it here
IF F(!1) DO ADD ''v%ver''
// Video Resolution
DO ADD '' (%res''
// Video Source (only if blu-ray or DVD)
IF R(DVD),R(Blu-ray) DO ADD '' %src''
// Video Codec
DO ADD '' %vid''
// Video Bit Depth (only if 10bit)
IF Z(10) DO ADD '' %bitbit''
DO ADD '') ''
DO ADD ''[%CRC]''

// Replacement rules (cleanup)
DO REPLACE '' '' ''_'' // replace spaces with underscores
DO REPLACE ''H264/AVC'' ''H264''
DO REPLACE ''0x0'' ''''
DO REPLACE ''__'' ''_''
DO REPLACE ''__'' ''_''

// Replace all illegal file name characters
DO REPLACE ''<'' ''(''
DO REPLACE ''>'' '')''
DO REPLACE '':'' ''-''
DO REPLACE ''"'' ''`''
DO REPLACE ''/'' ''_''
DO REPLACE ''/'' ''_''
DO REPLACE ''\'' ''_''
DO REPLACE ''|'' ''_''
DO REPLACE ''?'' ''_''
DO REPLACE ''*'' ''_''
','\r',char(13)),'\n',char(10)),0,'Legacy',NULL);
CREATE TABLE IF NOT EXISTS AniDB_Recommendation( AniDB_RecommendationID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, UserID int NOT NULL, RecommendationType int NOT NULL, RecommendationText text );
CREATE TABLE IF NOT EXISTS CrossRef_AniDB_TraktV2( CrossRef_AniDB_TraktV2ID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TraktID text, TraktSeasonNumber int NOT NULL, TraktStartEpisodeNumber int NOT NULL, TraktTitle text, CrossRefSource int NOT NULL );
CREATE TABLE IF NOT EXISTS CrossRef_AniDB_Trakt_Episode( CrossRef_AniDB_Trakt_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBEpisodeID int NOT NULL, TraktID text, Season int NOT NULL, EpisodeNumber int NOT NULL );
CREATE TABLE IF NOT EXISTS CustomTag( CustomTagID INTEGER PRIMARY KEY AUTOINCREMENT, TagName text, TagDescription text );
INSERT OR IGNORE INTO CustomTag VALUES(1,'Dropped','Started watching this series, but have since dropped it');
INSERT OR IGNORE INTO CustomTag VALUES(2,'Pinned','Pinned this series for whatever reason you like');
INSERT OR IGNORE INTO CustomTag VALUES(3,'Ongoing','This series does not have an end date');
INSERT OR IGNORE INTO CustomTag VALUES(4,'Waiting for Series Completion','Will start watching this once this series is finished');
INSERT OR IGNORE INTO CustomTag VALUES(5,'Waiting for Blu-ray Completion','Will start watching this once all episodes are available in Blu-Ray');
CREATE TABLE IF NOT EXISTS CrossRef_CustomTag( CrossRef_CustomTagID INTEGER PRIMARY KEY AUTOINCREMENT, CustomTagID int NOT NULL, CrossRefID int NOT NULL, CrossRefType int NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Anime ( AniDB_AnimeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, EpisodeCount int NOT NULL, AirDate timestamp NULL, EndDate timestamp NULL, URL text NULL, Picname text NULL, BeginYear int NOT NULL, EndYear int NOT NULL, AnimeType int NOT NULL, MainTitle text NOT NULL, AllTitles text NOT NULL, AllTags text NOT NULL, Description text NOT NULL, EpisodeCountNormal int NOT NULL, EpisodeCountSpecial int NOT NULL, Rating int NOT NULL, VoteCount int NOT NULL, TempRating int NOT NULL, TempVoteCount int NOT NULL, AvgReviewRating int NOT NULL, ReviewCount int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeDescUpdated timestamp NOT NULL, ImageEnabled int NOT NULL, AwardList text NOT NULL, Restricted int NOT NULL, AnimePlanetID int NULL, ANNID int NULL, AllCinemaID int NULL, AnimeNfo int NULL, LatestEpisodeNumber int NULL, DisableExternalLinksFlag int NULL , ContractVersion int NOT NULL DEFAULT 0, ContractBlob BLOB NULL, ContractSize int NOT NULL DEFAULT 0, Site_JP TEXT NULL, Site_EN TEXT NULL, Wikipedia_ID TEXT NULL, WikipediaJP_ID TEXT NULL, SyoboiID INT NULL, AnisonID INT NULL, CrunchyrollID TEXT NULL);
CREATE TABLE IF NOT EXISTS VideoLocal_Place ( VideoLocal_Place_ID INTEGER PRIMARY KEY AUTOINCREMENT,VideoLocalID int NOT NULL, FilePath text NOT NULL,  ImportFolderID int NOT NULL, ImportFolderType int NOT NULL );
CREATE TABLE IF NOT EXISTS VideoLocal ( VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT, Hash text NOT NULL, CRC32 text NULL, MD5 text NULL, SHA1 text NULL, HashSource int NOT NULL, FileSize INTEGER NOT NULL, IsIgnored int NOT NULL, DateTimeUpdated timestamp NOT NULL, FileName text NOT NULL DEFAULT '', VideoCodec text NOT NULL DEFAULT '', VideoBitrate text NOT NULL DEFAULT '',VideoBitDepth text NOT NULL DEFAULT '',VideoFrameRate text NOT NULL DEFAULT '',VideoResolution text NOT NULL DEFAULT '',AudioCodec text NOT NULL DEFAULT '',AudioBitrate text NOT NULL DEFAULT '',Duration INTEGER NOT NULL DEFAULT 0,DateTimeCreated timestamp NULL, IsVariation int NULL,MediaVersion int NOT NULL DEFAULT 0,MediaBlob BLOB NULL,MediaSize int NOT NULL DEFAULT 0 , MyListID INT NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS CloudAccount (CloudID INTEGER PRIMARY KEY AUTOINCREMENT, ConnectionString text NOT NULL, Provider text NOT NULL, Name text NOT NULL);
CREATE TABLE IF NOT EXISTS VideoLocal_User ( VideoLocal_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, VideoLocalID int NOT NULL, WatchedDate timestamp NULL, ResumePosition bigint NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS AuthTokens ( AuthID INTEGER PRIMARY KEY AUTOINCREMENT, UserID int NOT NULL, DeviceName text NOT NULL, Token text NOT NULL );
CREATE TABLE IF NOT EXISTS Scan ( ScanID INTEGER PRIMARY KEY AUTOINCREMENT, CreationTime timestamp NOT NULL, ImportFolders text NOT NULL, Status int NOT NULL );
CREATE TABLE IF NOT EXISTS ScanFile ( ScanFileID INTEGER PRIMARY KEY AUTOINCREMENT, ScanID int NOT NULL, ImportFolderID int NOT NULL, VideoLocal_Place_ID int NOT NULL, FullName text NOT NULL, FileSize bigint NOT NULL, Status int NOT NULL, CheckDate timestamp NULL, Hash text NOT NULL, HashResult text NULL );
CREATE TABLE IF NOT EXISTS TvDB_Episode ( TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, Id int NOT NULL, SeriesID int NOT NULL, SeasonID int NOT NULL, SeasonNumber int NOT NULL, EpisodeNumber int NOT NULL, EpisodeName text, Overview text, Filename text, EpImgFlag int NOT NULL, AbsoluteNumber int, AirsAfterSeason int, AirsBeforeEpisode int, AirsBeforeSeason int, AirDate timestamp, Rating int);
CREATE TABLE IF NOT EXISTS AnimeCharacter ( CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBID INTEGER NOT NULL, Name TEXT NOT NULL, AlternateName TEXT NULL, Description TEXT NULL, ImagePath TEXT NULL );
CREATE TABLE IF NOT EXISTS AnimeStaff ( StaffID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBID INTEGER NOT NULL, Name TEXT NOT NULL, AlternateName TEXT NULL, Description TEXT NULL, ImagePath TEXT NULL );
CREATE TABLE IF NOT EXISTS CrossRef_Anime_Staff ( CrossRef_Anime_StaffID INTEGER PRIMARY KEY AUTOINCREMENT, AniDB_AnimeID INTEGER NOT NULL, StaffID INTEGER NOT NULL, Role TEXT NULL, RoleID INTEGER, RoleType INTEGER NOT NULL, Language TEXT NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_AnimeUpdate ( AniDB_AnimeUpdateID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, UpdatedAt timestamp NOT NULL );
CREATE TABLE IF NOT EXISTS AniDB_Episode ( AniDB_EpisodeID integer primary key autoincrement, EpisodeID int not null, AnimeID int not null, LengthSeconds int not null, Rating text not null, Votes text not null, EpisodeNumber int not null, EpisodeType int not null, AirDate int not null, DateTimeUpdated datetime not null, Description text default '' not null );
CREATE TABLE IF NOT EXISTS AniDB_Episode_Title ( AniDB_Episode_TitleID INTEGER PRIMARY KEY AUTOINCREMENT, AniDB_EpisodeID int NOT NULL, Language text NOT NULL, Title text NOT NULL );
CREATE TABLE IF NOT EXISTS CrossRef_AniDB_TvDB_Episode_Override( CrossRef_AniDB_TvDB_Episode_OverrideID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL );
CREATE TABLE IF NOT EXISTS CrossRef_AniDB_TvDB(CrossRef_AniDB_TvDBID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBID int NOT NULL, TvDBID int NOT NULL, CrossRefSource INT NOT NULL);
CREATE TABLE IF NOT EXISTS CrossRef_AniDB_TvDB_Episode(CrossRef_AniDB_TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL, MatchRating INT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_AniDB_Anime_Category_AnimeID on AniDB_Anime_Category(AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Anime_Category_AnimeID_CategoryID ON AniDB_Anime_Category (AnimeID, CategoryID);
CREATE INDEX IF NOT EXISTS IX_AniDB_Anime_Character_AnimeID on AniDB_Anime_Character(AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Anime_Character_AnimeID_CharID ON AniDB_Anime_Character(AnimeID, CharID);
CREATE INDEX IF NOT EXISTS IX_AniDB_Anime_Relation_AnimeID on AniDB_Anime_Relation(AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID ON AniDB_Anime_Relation(AnimeID, RelatedAnimeID);
CREATE INDEX IF NOT EXISTS IX_AniDB_Anime_Review_AnimeID on AniDB_Anime_Review(AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Anime_Review_AnimeID_ReviewID ON AniDB_Anime_Review(AnimeID, ReviewID);
CREATE INDEX IF NOT EXISTS IX_AniDB_Anime_Similar_AnimeID on AniDB_Anime_Similar(AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID ON AniDB_Anime_Similar(AnimeID, SimilarAnimeID);
CREATE INDEX IF NOT EXISTS IX_AniDB_Anime_Tag_AnimeID on AniDB_Anime_Tag(AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Anime_Tag_AnimeID_TagID ON AniDB_Anime_Tag(AnimeID, TagID);
CREATE INDEX IF NOT EXISTS IX_AniDB_Anime_Title_AnimeID on AniDB_Anime_Title(AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Category_CategoryID ON AniDB_Category(CategoryID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Character_CharID ON AniDB_Character(CharID);
CREATE INDEX IF NOT EXISTS IX_AniDB_Character_Seiyuu_CharID on AniDB_Character_Seiyuu(CharID);
CREATE INDEX IF NOT EXISTS IX_AniDB_Character_Seiyuu_SeiyuuID on AniDB_Character_Seiyuu(SeiyuuID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID ON AniDB_Character_Seiyuu(CharID, SeiyuuID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Seiyuu_SeiyuuID ON AniDB_Seiyuu(SeiyuuID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_File_Hash on AniDB_File(Hash);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_File_FileID ON AniDB_File(FileID);
CREATE INDEX IF NOT EXISTS IX_AniDB_File_File_Source on AniDB_File(File_Source);
CREATE INDEX IF NOT EXISTS IX_AniDB_GroupStatus_AnimeID on AniDB_GroupStatus(AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_GroupStatus_AnimeID_GroupID ON AniDB_GroupStatus(AnimeID, GroupID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_ReleaseGroup_GroupID ON AniDB_ReleaseGroup(GroupID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Review_ReviewID ON AniDB_Review(ReviewID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Tag_TagID ON AniDB_Tag(TagID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AnimeEpisode_AniDB_EpisodeID ON AnimeEpisode(AniDB_EpisodeID);
CREATE INDEX IF NOT EXISTS IX_AnimeEpisode_AnimeSeriesID on AnimeEpisode(AnimeSeriesID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AnimeSeries_AniDB_ID ON AnimeSeries(AniDB_ID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_CrossRef_AniDB_Other ON CrossRef_AniDB_Other(AnimeID, CrossRefID, CrossRefSource, CrossRefType);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_CrossRef_File_Episode_Hash_EpisodeID ON CrossRef_File_Episode(Hash, EpisodeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_FileNameHash ON FileNameHash(FileName, FileSize, Hash);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_Language_LanguageName ON Language(LanguageName);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_ScheduledUpdate_UpdateType ON ScheduledUpdate(UpdateType);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_TvDB_ImageFanart_Id ON TvDB_ImageFanart(Id);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_TvDB_ImageWideBanner_Id ON TvDB_ImageWideBanner(Id);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_TvDB_ImagePoster_Id ON TvDB_ImagePoster(Id);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_TvDB_Series_Id ON TvDB_Series(SeriesID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Anime_DefaultImage_ImageType ON AniDB_Anime_DefaultImage(AnimeID, ImageType);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_MovieDB_Movie_Id ON MovieDB_Movie(MovieId);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AnimeEpisode_User_User_EpisodeID ON AnimeEpisode_User(JMMUserID, AnimeEpisodeID);
CREATE INDEX IF NOT EXISTS IX_AnimeEpisode_User_User_AnimeSeriesID on AnimeEpisode_User(JMMUserID, AnimeSeriesID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AnimeSeries_User_User_SeriesID ON AnimeSeries_User(JMMUserID, AnimeSeriesID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AnimeGroup_User_User_GroupID ON AnimeGroup_User(JMMUserID, AnimeGroupID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_IgnoreAnime_User_AnimeID ON IgnoreAnime(JMMUserID, AnimeID, IgnoreType);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_Trakt_Friend_Username ON Trakt_Friend(Username);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_CrossRef_AniDB_Trakt_Season ON CrossRef_AniDB_Trakt(TraktID, TraktSeasonNumber);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_CrossRef_AniDB_Trakt_Anime ON CrossRef_AniDB_Trakt(AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_BookmarkedAnime_AnimeID ON BookmarkedAnime(BookmarkedAnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_FileFfdshowPreset_Hash ON FileFfdshowPreset(Hash, FileSize);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Recommendation ON AniDB_Recommendation(AnimeID, UserID);
CREATE INDEX IF NOT EXISTS IX_CrossRef_File_Episode_Hash ON CrossRef_File_Episode(Hash);
CREATE INDEX IF NOT EXISTS IX_CrossRef_File_Episode_EpisodeID ON CrossRef_File_Episode(EpisodeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_CrossRef_AniDB_TraktV2 ON CrossRef_AniDB_TraktV2(AnimeID, TraktSeasonNumber, TraktStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID ON CrossRef_AniDB_Trakt_Episode(AniDBEpisodeID);
CREATE UNIQUE INDEX IF NOT EXISTS [UIX2_AniDB_Anime_AnimeID] ON [AniDB_Anime] ([AnimeID]);
CREATE UNIQUE INDEX IF NOT EXISTS [UIX_VideoLocal_ VideoLocal_Place_ID] ON [VideoLocal_Place] ([VideoLocal_Place_ID]);
CREATE UNIQUE INDEX IF NOT EXISTS [UIX_CloudAccount_CloudID] ON [CloudAccount] ([CloudID]);
CREATE UNIQUE INDEX IF NOT EXISTS UIX2_VideoLocal_User_User_VideoLocalID ON VideoLocal_User(JMMUserID, VideoLocalID);
CREATE INDEX IF NOT EXISTS IX_VideoLocal_Hash ON VideoLocal(Hash);
CREATE INDEX IF NOT EXISTS UIX_ScanFileStatus ON ScanFile(ScanID,Status,CheckDate);
CREATE INDEX IF NOT EXISTS IX_AniDB_Anime_Character_CharID ON AniDB_Anime_Character(CharID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_TvDB_Episode_Id ON TvDB_Episode(Id);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_AnimeUpdate ON AniDB_AnimeUpdate(AnimeID);
CREATE INDEX IF NOT EXISTS IX_AniDB_Episode_AnimeID on AniDB_Episode (AnimeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_Episode_EpisodeID on AniDB_Episode (EpisodeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_TvDB_Episode_Override_AniDBEpisodeID_TvDBEpisodeID ON CrossRef_AniDB_TvDB_Episode_Override(AniDBEpisodeID,TvDBEpisodeID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_AniDB_TvDB_AniDBID_TvDBID ON CrossRef_AniDB_TvDB(AniDBID,TvDBID);
CREATE UNIQUE INDEX IF NOT EXISTS UIX_CrossRef_AniDB_TvDB_Episode_AniDBID_TvDBID ON CrossRef_AniDB_TvDB_Episode(AniDBEpisodeID,TvDBEpisodeID);