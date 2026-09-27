using System.Data;
using Dapper;

namespace CollectiblesApi.Data
{
  // SQLite stores whole-number NUMERIC values (e.g. 325) as Int64, not as a
  // float/real. Dapper's default deserializer doesn't reliably convert
  // Int64 -> decimal, so we teach it how here.  
  public class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
  {
    public override decimal Parse(object value)
    {
      return Convert.ToDecimal(value);
    }

    public override void SetValue(IDbDataParameter parameter, decimal value)
    {
      parameter.Value = value;
    }
  }
}