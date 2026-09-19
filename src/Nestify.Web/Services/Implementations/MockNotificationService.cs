using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nestify.Web.Models;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations
{
    // Mock service providing static notifications for UI development
    public class MockNotificationService : INotificationService
    {
        private readonly List<Notification> _notifications = new()
        {
            new Notification { Type = "info", Message = "New booking request received", Timestamp = DateTime.UtcNow.AddMinutes(-10) },
            new Notification { Type = "success", Message = "Your profile was updated successfully", Timestamp = DateTime.UtcNow.AddHours(-1) },
            new Notification { Type = "warning", Message = "Upcoming appointment in 30 minutes", Timestamp = DateTime.UtcNow.AddMinutes(-5) }
        };

        public Task<IReadOnlyList<Notification>> GetNotificationsAsync()
        {
            // Return a copy to simulate read‑only data source
            return Task.FromResult<IReadOnlyList<Notification>>(new List<Notification>(_notifications));
        }

        public Task MarkAsReadAsync(Guid id)
        {
            var note = _notifications.Find(n => n.Id == id);
            if (note != null) note.IsRead = true;
            return Task.CompletedTask;
        }

        public Task MarkAllAsReadAsync()
        {
            foreach (var note in _notifications) note.IsRead = true;
            return Task.CompletedTask;
        }
    }
}
