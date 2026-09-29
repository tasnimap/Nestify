using System.Net.Http.Json;
using System.Threading.Tasks;
using Nestify.Shared.Dtos.Notifications;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private const string NotificationsEndpoint = "api/v1/notifications";

        public NotificationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<NotificationFeedDto> GetNotificationsAsync() =>
            await _httpClient.GetFromJsonAsync<NotificationFeedDto>(NotificationsEndpoint)
            ?? throw new InvalidOperationException("The notifications endpoint returned an empty response.");

        public async Task MarkAsReadAsync(long id)
        {
            var response = await _httpClient.PostAsync($"{NotificationsEndpoint}/{id}/read", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task MarkAllAsReadAsync()
        {
            var response = await _httpClient.PostAsync($"{NotificationsEndpoint}/read-all", null);
            response.EnsureSuccessStatusCode();
        }
    }
}
