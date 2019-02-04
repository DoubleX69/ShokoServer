﻿using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class Trakt_EpisodeRepository : BaseRepository<Trakt_Episode, int>
    {
        private PocoIndex<int, Trakt_Episode, int> Shows;
        private PocoIndex<int, Trakt_Episode, int, int> ShowsSeasons;

        internal override int SelectKey(Trakt_Episode entity) => entity.Trakt_EpisodeID;
            
        internal override void PopulateIndexes()
        {
            Shows = new PocoIndex<int, Trakt_Episode, int>(Cache, a => a.Trakt_ShowID);
            ShowsSeasons = new PocoIndex<int, Trakt_Episode,int, int>(Cache, a => a.Trakt_ShowID,a=>a.Season);
        }

        internal override void ClearIndexes()
        {
            Shows = null;
            ShowsSeasons = null;
        }

        public List<Trakt_Episode> GetByShowID(int showID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Shows.GetMultiple(showID);
                return Table.Where(a => a.Trakt_ShowID == showID).ToList();
            }
        }

        public List<Trakt_Episode> GetByShowIDAndSeason(int showID, int seasonNumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ShowsSeasons.GetMultiple(showID,seasonNumber);
                return Table.Where(a => a.Trakt_ShowID == showID && a.Season==seasonNumber).ToList();
            }
        }

        public Trakt_Episode GetByShowIDSeasonAndEpisode(int showID, int seasonNumber, int epnumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ShowsSeasons.GetMultiple(showID, seasonNumber).FirstOrDefault(a=>a.EpisodeNumber==epnumber);
                return Table.FirstOrDefault(a => a.Trakt_ShowID == showID && a.Season == seasonNumber && a.EpisodeNumber==epnumber);
            }
        }
        public int GetNumberOfEpisodesForSeason(int showID, int seasonNumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ShowsSeasons.GetMultiple(showID, seasonNumber).Count();
                return Table.Count(a => a.Trakt_ShowID == showID && a.Season == seasonNumber);
            }
        }
        public int GetLastSeasonForSeries(int showID)
        {
            using (RepoLock.ReaderLock())
            {
                List<int> max;
                if (IsCached)
                    max = Shows.GetMultiple(showID).Select(xref => xref.Season).ToList();
                else
                    max = Table.Where(a => a.Trakt_ShowID==showID).Select(xref => xref.Season).ToList();
                if (max.Count == 0) return -1;
                return max.Max();
            }
        }
        public Trakt_Episode GetByReference(string reference)
        {
            string[] sp = reference.Split('_');
            return GetByShowIDSeasonAndEpisode(int.Parse(sp[0]), int.Parse(sp[1]), int.Parse(sp[2]));
        }
        public List<int> GetSeasonNumbersForSeries(int showID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Shows.GetMultiple(showID).Select(a=>a.Season).Distinct().OrderBy(a=>a).ToList();
                return Table.Where(a => a.Trakt_ShowID == showID).ToList().Select(a => a.Season).Distinct().OrderBy(a=>a).ToList();
            }
        }
    }
}