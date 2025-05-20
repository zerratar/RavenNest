using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class GameSession : Entity<GameSession>
    {
        [PersistentData] private DateTime? refreshed;
        [PersistentData] private Guid userId;
        [PersistentData] private DateTime started;
        [PersistentData] private DateTime? stopped;
        [PersistentData] private DateTime? updated;
        [PersistentData] private bool local;
        [PersistentData] private long? revision;
        [PersistentData] private int status;

        //[Obsolete("Will be removed in the future, do not use. As we will not support local players")]
        //public bool Local { get => local; set => Set(ref local, value); }

        ///// <summary>
        ///// Gets or sets the owner User Id of this session
        ///// </summary>
        //public Guid UserId { get => userId; set => Set(ref userId, value); }

        ///// <summary>        
        ///// Gets or sets the date and time the session was first started
        ///// </summary>
        //public DateTime Started { get => started; set => Set(ref started, value); }

        ///// <summary>
        ///// Gets or sets the date and time this session was last restarted, this should be set whenever BeginSession is called, even if the session is already active.
        ///// </summary>
        //public DateTime? Refreshed { get => refreshed; set => Set(ref refreshed, value); }
        ///// <summary>
        ///// Gets or sets the date and time the session was stopped, this is only set when the session is stopped and can no longer be used.
        ///// </summary>
        //public DateTime? Stopped { get => stopped; set => Set(ref stopped, value); }
        ///// <summary>
        ///// Gets or sets the date and time the session was last updated, this should be set whenever the session is updated or changed.
        ///// </summary>
        //public DateTime? Updated { get => updated; set => Set(ref updated, value); }
        //public int Status { get => status; set => Set(ref status, value); }
        //public long? Revision { get => revision; set => Set(ref revision, value); }
    }
}
