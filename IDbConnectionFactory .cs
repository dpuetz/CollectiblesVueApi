using System.Data.Common;

namespace CollectiblesApi.Data
{
  public interface IDbConnectionFactory
  {
    DbConnection CreateConnection();
  }
}