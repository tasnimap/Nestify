using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations
{
    public sealed class MockCurrentUserService : ICurrentUserService
    {
        public string UserId { get; private set; } = "user-prapty";
        public string DisplayName { get; private set; } = "Prapty";

        public void SwitchTo(string userId, string displayName)
        {
            UserId = userId;
            DisplayName = displayName;
        }
    }
}
