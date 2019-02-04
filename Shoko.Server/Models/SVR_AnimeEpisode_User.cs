﻿using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Repositories;
using System.ComponentModel.DataAnnotations.Schema;
using Shoko.Server.Compression.LZ4;

namespace Shoko.Server.Models
{
    public class SVR_AnimeEpisode_User : AnimeEpisode_User
    {

        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; }
        public int ContractSize { get; set; }


        public const int CONTRACT_VERSION = 3;

        [NotMapped]
        internal CL_AnimeEpisode_User _contract;

        [NotMapped]
        internal virtual CL_AnimeEpisode_User Contract
        {
            get
            {
                if ((_contract == null) && (ContractBlob != null) && (ContractBlob.Length > 0) && (ContractSize > 0))
                    _contract = CompressionHelper.DeserializeObject<CL_AnimeEpisode_User>(ContractBlob, ContractSize);
                if (_contract != null)
                {
                    SVR_AnimeSeries_User seuse = Repo.Instance.AnimeSeries_User.GetByUserAndSeriesID(JMMUserID, AnimeSeriesID);
                    _contract.UnwatchedEpCountSeries = seuse?.UnwatchedEpisodeCount ?? 0;
                    SVR_AnimeEpisode aep = Repo.Instance.AnimeEpisode.GetByID(AnimeEpisodeID);
                    _contract.LocalFileCount = aep?.GetVideoLocals().Count ?? 0;
                }
                return _contract;
            }
            set
            {
                _contract = value;
                ContractBlob = CompressionHelper.SerializeObject(value, out int outsize);
                ContractSize = outsize;
                ContractVersion = CONTRACT_VERSION;
            }
        }

        public void CollectContractMemory()
        {
            _contract = null;
        }
    }
}