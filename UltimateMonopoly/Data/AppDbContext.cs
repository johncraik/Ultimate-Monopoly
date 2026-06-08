using JC.Communication.Extensions;
using JC.Communication.Logging.Data;
using JC.Communication.Logging.Models.Email;
using JC.Communication.Logging.Models.Messaging;
using JC.Communication.Logging.Models.Notifications;
using JC.Communication.Messaging.Data;
using JC.Communication.Messaging.Models.DomainModels;
using JC.Communication.Notifications.Data;
using JC.Communication.Notifications.Models;
using JC.Core.Models;
using JC.Github.Data;
using JC.Github.Extensions;
using JC.Github.Models;
using JC.Identity.Data;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Models;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Models.DataModels.Social;

namespace UltimateMonopoly.Data;

public class AppDbContext : IdentityDataDbContext<AppUser, AppRole>,
    IGithubDbContext, IEmailDbContext, IMessagingDbContext, INotificationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options, IUserInfo userInfo)
        : base(options, userInfo) { }

    public DbSet<BoardSkin> BoardSkins { get; set; }
    public DbSet<BoardSkinSpace> BoardSkinSpaces { get; set; }
    public DbSet<SharedBoardSkin> SharedBoardSkins { get; set; }
    
    //Games
    public DbSet<Game> Games { get; set; }
    public DbSet<GamePlayer> GamePlayers { get; set; }
    public DbSet<GameTurn> GameTurns { get; set; }
    public DbSet<GameSnapshot> GameSnapshots { get; set; }
    public DbSet<GameTurnEvents> GameTurnEvents { get; set; }
    
    // Social
    public DbSet<Friend> Friends { get; set; }
    public DbSet<FriendRequest> FriendRequests { get; set; }
    public DbSet<BlockedUser> BlockedUsers { get; set; }
    public DbSet<ReportedUser> ReportedUsers { get; set; }
    
    // Github
    public DbSet<ReportedIssue> ReportedIssues { get; set; }
    public DbSet<IssueComment> IssueComments { get; set; }

    // Email
    public DbSet<EmailLog> EmailLogs { get; set; }
    public DbSet<EmailRecipientLog> EmailRecipientLogs { get; set; }
    public DbSet<EmailContentLog> EmailContentLogs { get; set; }
    public DbSet<EmailSentLog> EmailSentLogs { get; set; }

    // Messaging
    public DbSet<ChatThread> ChatThreads { get; set; }
    public DbSet<ThreadDeleted> DeletedThreads { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ChatParticipant> ChatParticipants { get; set; }
    public DbSet<ChatMetadata> ChatMetadata { get; set; }
    public DbSet<ThreadActivityLog> ThreadActivityLogs { get; set; }
    public DbSet<MessageReadLog> MessageReadLogs { get; set; }

    // Notifications
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<NotificationStyle> NotificationStyles { get; set; }
    public DbSet<NotificationLog> NotificationLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyGithubMappings();
        modelBuilder.ApplyEmailMappings();
        modelBuilder.ApplyMessagingMappings();
        modelBuilder.ApplyNotificationMappings();
    }
}
