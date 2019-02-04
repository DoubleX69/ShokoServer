﻿using System;
using System.Collections.Generic;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.CommandQueue.Preconditions;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{

    public class CmdAniDBGetFileMyListStatus : BaseCommand, ICommand
    {
        public int FileID { get; set; }
        public string FileName { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.AniDB;
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 6;
        public string Id => $"GetFileMyListStatus_{FileID}";
        public override List<Type> GenericPreconditions => new List<Type> {  typeof(AniDBUDPBan) };
        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.AniDB_MyListGetFile,
            ExtraParams = new[] { FileName, FileID.ToString() }
        };

        public string WorkType => WorkTypes.AniDB;

        public CmdAniDBGetFileMyListStatus(string str) : base(str)
        {
        }

        public CmdAniDBGetFileMyListStatus(int aniFileID, string fileName)
        {
            FileID = aniFileID;
            FileName = fileName;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Processing CommandRequest_GetFileMyListStatus: {FileName} ({FileID})");
            try
            {
                ReportInit(progress);
                ShokoService.AnidbProcessor.GetMyListFileStatus(FileID);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing Command AniDb.GetFileMyListStatus: {FileName} ({FileID}) - {ex}", ex);
            }
        }
    }
}