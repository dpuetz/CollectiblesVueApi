using Dapper;
using Microsoft.Data.Sqlite;
using CollectiblesApi.Models;

namespace CollectiblesApi.Repositories;

public class UserRepository : IUserRepository
{
  private readonly string _connectionString;

  public UserRepository(string connectionString)
  {
    _connectionString = connectionString;
  }

  public async Task<User?> GetByEmailAsync(string email)
  {
    const string sql = """
            SELECT Id, Email, PasswordHash, IsActive, CreatedAtUtc, FirstName, LastName, VendorCompanyId, IsVendor
            FROM Users
            WHERE Email = @Email COLLATE NOCASE
            """;

    using var connection = new SqliteConnection(_connectionString);
    return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
  }

  public async Task<List<string>> GetGroupNamesForUserAsync(int userId)
  {
    const string sql = """
            SELECT ug.Name
            FROM UserGroups ug
            INNER JOIN UserUserGroups uug ON uug.UserGroupId = ug.Id
            WHERE uug.UserId = @UserId
            ORDER BY ug.Name
            """;

    using var connection = new SqliteConnection(_connectionString);
    var groups = await connection.QueryAsync<string>(sql, new { UserId = userId });
    return groups.ToList();
  }

  public async Task<bool> EmailExistsAsync(string email)
  {
    using var connection = new SqliteConnection(_connectionString);
    var count = await connection.ExecuteScalarAsync<int>(
        "SELECT COUNT(*) FROM Users WHERE Email = @Email",
        new { Email = email });
    return count > 0;
  }

  public async Task<List<UserGroupInfo>> GetGroupsByNamesAsync(IEnumerable<string> groupNames)
  {
    using var connection = new SqliteConnection(_connectionString);
    var groups = await connection.QueryAsync<UserGroupInfo>(
        "SELECT Id, Name FROM UserGroups WHERE Name IN @Names",
        new { Names = groupNames });
    return groups.ToList();
  }

  public async Task<int> CreateUserAsync(string email, string passwordHash, string firstName, string lastName,
      IEnumerable<long> groupIds, bool isVendor, int vendorCompanyId)
  {
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();
    using var transaction = connection.BeginTransaction();

    try
    {
      var userId = await connection.ExecuteScalarAsync<long>(
          """
            INSERT INTO Users (Email, PasswordHash, IsActive, FirstName, LastName, IsVendor, VendorCompanyId)
            VALUES (@Email, @PasswordHash, 1, @FirstName, @LastName, @IsVendor, @VendorCompanyId )
            RETURNING Id;
            """,
          new
          {
            Email = email,
            PasswordHash = passwordHash,
            FirstName = firstName,
            LastName = lastName,
            IsVendor = isVendor,
            VendorCompanyId = vendorCompanyId
          },
          transaction);

      foreach (var groupId in groupIds)
      {
        await connection.ExecuteAsync(
            "INSERT INTO UserUserGroups (UserId, UserGroupId) VALUES (@UserId, @GroupId)",
            new { UserId = userId, GroupId = groupId },
            transaction);
      }

      transaction.Commit();
      return (int)userId;
    }
    catch
    {
      transaction.Rollback();
      throw;
    }
  }

  public async Task<List<UserWithGroups>> GetAllUsersWithGroupsAsync()
  {
    const string sql = """
        SELECT u.Id, u.Email, u.PasswordHash, u.IsActive, u.CreatedAtUtc, u.FirstName, u.LastName,
               u.IsVendor, u.VendorCompanyId,
               ug.Name
        FROM Users u
        LEFT JOIN UserUserGroups uug ON uug.UserId = u.Id
        LEFT JOIN UserGroups ug ON ug.Id = uug.UserGroupId
        ORDER BY u.Id
        """;

    using var connection = new SqliteConnection(_connectionString);

    var userMap = new Dictionary<int, UserWithGroups>();

    await connection.QueryAsync<User, string?, UserWithGroups>(
        sql,
        (user, groupName) =>
        {
          if (!userMap.TryGetValue(user.Id, out var entry))
          {
            entry = new UserWithGroups { User = user };
            userMap[user.Id] = entry;
          }

          if (groupName is not null)
            entry.Groups.Add(groupName);

          return entry;
        },
        splitOn: "Name");

    return userMap.Values.ToList();
  }

  public async Task<User?> GetUserByIdAsync(int id)
  {
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();
    return await connection.QuerySingleOrDefaultAsync<User>(
        "SELECT * FROM Users WHERE Id = @Id",
        new { Id = id });
  }

  public async Task<bool> UpdateUserAsync(int id, string email, string? passwordHash,
  string firstName, string lastName, bool isActive, IEnumerable<long> groupIds, bool isVendor,
  int vendorCompanyId)
  {
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();
    using var transaction = connection.BeginTransaction();
    try
    {
      int rows;
      if (passwordHash is not null)
      {
        rows = await connection.ExecuteAsync(
            """
                UPDATE Users
                SET Email = @Email, PasswordHash = @PasswordHash, FirstName = @FirstName, LastName = @LastName, 
                  IsActive = @IsActive, IsVendor = @IsVendor, VendorCompanyId = @VendorCompanyId
                WHERE Id = @Id
                """,
            new
            {
              Id = id,
              Email = email,
              PasswordHash = passwordHash,
              FirstName = firstName,
              LastName = lastName,
              IsActive = isActive,
              IsVendor = isVendor,
              VendorCompanyId = vendorCompanyId
            },
            transaction);
      }
      else
      {
        rows = await connection.ExecuteAsync(
            """
                UPDATE Users
                SET Email = @Email, FirstName = @FirstName, LastName = @LastName, IsActive = @IsActive,
                IsVendor = @IsVendor, VendorCompanyId = @VendorCompanyId
                WHERE Id = @Id
                """,
            new
            {
              Id = id,
              Email = email,
              FirstName = firstName,
              LastName = lastName,
              IsActive = isActive,
              IsVendor = isVendor,
              VendorCompanyId = vendorCompanyId
            },
            transaction);
      }

      if (rows == 0)
      {
        transaction.Rollback();
        return false;
      }

      await connection.ExecuteAsync(
          "DELETE FROM UserUserGroups WHERE UserId = @UserId",
          new { UserId = id },
          transaction);

      foreach (var groupId in groupIds)
      {
        await connection.ExecuteAsync(
            "INSERT INTO UserUserGroups (UserId, UserGroupId) VALUES (@UserId, @GroupId)",
            new { UserId = id, GroupId = groupId },
            transaction);
      }

      transaction.Commit();
      return true;
    }
    catch
    {
      transaction.Rollback();
      throw;
    }
  }

}
