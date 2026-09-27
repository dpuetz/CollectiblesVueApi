using CollectiblesApi.Models;

namespace CollectiblesApi.Repositories;

public interface IUserRepository
{
  Task<User?> GetByEmailAsync(string email);
  Task<List<string>> GetGroupNamesForUserAsync(int userId);

  Task<bool> EmailExistsAsync(string email);
  Task<List<UserGroupInfo>> GetGroupsByNamesAsync(IEnumerable<string> groupNames);
  Task<int> CreateUserAsync(string email, string passwordHash, string firstName, string lastName, IEnumerable<long> groupIds, bool isVendor, int vendorCompanyId);
  Task<List<UserWithGroups>> GetAllUsersWithGroupsAsync();

  Task<User?> GetUserByIdAsync(int id);
  Task<bool> UpdateUserAsync(int id, string email, string? passwordHash, string firstName, string lastName, bool isActive, IEnumerable<long> groupIds, bool isVendor, int vendorCompanyId);
}
