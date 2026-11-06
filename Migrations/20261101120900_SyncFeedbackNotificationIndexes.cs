using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    // Records two indexes in the model snapshot only. The database already has them:
    // Dashboard_Reporting_Feedback_Chatbot_Notification (20261028065041) created
    // IX_Feedbacks_custID_orderID (unique) and IX_Notifications_custID_isRead and dropped IX_Feedbacks_custID,
    // but ApplicationDbContext didn't declare them, so later snapshots lost them.
    // Nothing to change in the database, so Up and Down are empty on purpose.
    public partial class SyncFeedbackNotificationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
