using Nestify.Shared.Dtos.Admin;
namespace Nestify.Web.Services.Interfaces;
public interface IVerificationAdminService { Task<List<VerificationRequestDto>> GetQueueAsync(); Task DecideAsync(string id, bool approve); }
