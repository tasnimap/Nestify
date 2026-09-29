using System.Threading.Tasks;
using Nestify.Shared.Dtos.Notifications;

namespace Nestify.Web.Services.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationFeedDto> GetNotificationsAsync();
        Task MarkAsReadAsync(long id);
        Task MarkAllAsReadAsync();
    }
}
