using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Nestify.Web.Models;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private const string NotificationsEndpoint = "api/notifications";

        public NotificationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyList<Notification>> GetNotificationsAsync()
        {
            try
            {
                var notifications = await _httpClient.GetFromJsonAsync<List<Notification>>(NotificationsEndpoint);
                return notifications ?? new List<Notification>();
            }
            catch
            {
                return new List<Notification>();
            }
        }

        public async Task MarkAsReadAsync(Guid id)
        {
            var response = await _httpClient.PostAsync($"{NotificationsEndpoint}/{id}/read", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task MarkAllAsReadAsync()
        {
            var response = await _httpClient.PostAsync($"{NotificationsEndpoint}/readall", null);
            response.EnsureSuccessStatusCode();
        }
    }
}
