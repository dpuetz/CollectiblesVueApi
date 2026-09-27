using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace CollectiblesApi.Data
{
  public class SqliteConnectionFactory : IDbConnectionFactory
  {
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
      _connectionString = connectionString;
    }

    public DbConnection CreateConnection() => new SqliteConnection(_connectionString);
  }
}