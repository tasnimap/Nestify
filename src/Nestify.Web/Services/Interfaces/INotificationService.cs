using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nestify.Web.Models;

namespace Nestify.Web.Services.Interfaces
{
    public interface INotificationService
    {
        Task<IReadOnlyList<Notification>> GetNotificationsAsync();
        Task MarkAsReadAsync(Guid id);
        Task MarkAllAsReadAsync();
    }
}
