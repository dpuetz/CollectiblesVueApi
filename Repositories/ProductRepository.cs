using CollectiblesApi.Data;
using CollectiblesApi.Models;
using Dapper;

namespace CollectiblesApi.Repositories
{
  public class ProductRepository : IProductRepository
  {
    private const string SelectColumns =
        "id, name, description, price, category, categoryIcon, imageUrl, circa";

    private readonly IDbConnectionFactory _connectionFactory;

    public ProductRepository(IDbConnectionFactory connectionFactory)
    {
      _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
      using var connection = _connectionFactory.CreateConnection();
      return await connection.QueryAsync<Product>(
          $"SELECT {SelectColumns} FROM products ORDER BY category, name");
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
      using var connection = _connectionFactory.CreateConnection();
      return await connection.QueryAsync<Product>(
          $"SELECT {SelectColumns} FROM products WHERE category = @Category ORDER BY name",
          new { Category = category });
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
      using var connection = _connectionFactory.CreateConnection();
      return await connection.QuerySingleOrDefaultAsync<Product>(
          $"SELECT {SelectColumns} FROM products WHERE id = @Id",
          new { Id = id });
    }

    public async Task<Product> CreateAsync(Product product)
    {
      using var connection = _connectionFactory.CreateConnection();
      connection.Open();
      using var transaction = connection.BeginTransaction();
      if (product == null) throw new Exception("Product cannot be null.");

      try
      {
        // Enforce "only one categoryIcon per category" before the insert,
        // so the DB's partial unique index never even has to reject us.
        product.Category = product.Category.ToLower();

        // Rule 3: if this is the first item in the category, it must own the icon
        // regardless of what the caller passed in.
        var existingInCategory = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM products WHERE category = @Category",
            new { product.Category }, transaction);
        if (existingInCategory == 0)
        {
          product.CategoryIcon = true;
        }

        if (product.CategoryIcon)
        {
          await connection.ExecuteAsync(
              "UPDATE products SET categoryIcon = 0 WHERE category = @Category AND categoryIcon = 1",
              new { product.Category }, transaction);
        }

        // Id is not included in the INSERT - SQLite assigns it via rowid.
        // Whatever the caller put in product.Id (e.g. 0) is ignored.
        var newId = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO products (name, description, price, category, categoryIcon, imageUrl, circa)
                      VALUES (@Name, @Description, @Price, @Category, @CategoryIcon, @ImageUrl, @Circa);
                      SELECT last_insert_rowid();",
            product, transaction);

        transaction.Commit();

        product.Id = (int)newId;
        return product;
      }
      catch
      {
        transaction.Rollback();
        throw;
      }
    }

    public async Task<bool> UpdateAsync(int id, Product product)
    {
      using var connection = _connectionFactory.CreateConnection();
      connection.Open();
      using var transaction = connection.BeginTransaction();
      if (product == null) throw new Exception("Product cannot be null.");
      try
      {
        product.Category = product.Category.ToLower();

        // Rule 3: if there are no other items in this category, this one must
        // own the icon regardless of what the caller passed in.
        var othersInCategory = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM products WHERE category = @Category AND id != @Id",
            new { product.Category, Id = id }, transaction);
        if (othersInCategory == 0)
        {
          product.CategoryIcon = true;
        }

        if (product.CategoryIcon)
        {
          // Clear the flag on any other row in the same category
          // (excluding this row, in case it already held the icon).
          await connection.ExecuteAsync(
              "UPDATE products SET categoryIcon = 0 WHERE category = @Category AND categoryIcon = 1 AND id != @Id",
              new { product.Category, Id = id }, transaction);
        }

        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE products
                      SET name = @Name,
                          description = @Description,
                          price = @Price,
                          category = @Category,
                          categoryIcon = @CategoryIcon,
                          imageUrl = @ImageUrl,
                          circa = @Circa
                      WHERE id = @Id",
            new
            {
              product.Name,
              product.Description,
              product.Price,
              product.Category,
              product.CategoryIcon,
              product.ImageUrl,
              product.Circa,
              Id = id
            }, transaction);

        transaction.Commit();
        return rowsAffected > 0;
      }
      catch
      {
        transaction.Rollback();
        throw;
      }
    }

    // Demo-only: wipes the table and reseeds it with the original fixed dataset,
    // including the original ids, so relative ordering / FKs (if any) stay stable.
    public async Task ResetAsync()
    {
      using var connection = _connectionFactory.CreateConnection();
      connection.Open();
      using var transaction = connection.BeginTransaction();
      try
      {
        await connection.ExecuteAsync("DELETE FROM products", transaction: transaction);

        // Reset SQLite's AUTOINCREMENT counter (no-op if the table isn't using
        // AUTOINCREMENT / sqlite_sequence doesn't have a row for it yet).
        await connection.ExecuteAsync(
            "DELETE FROM sqlite_sequence WHERE name = 'products'", transaction: transaction);

        var originalProducts = new[]
        {
          new { Id = 1, Name = "Blue Crackle Glass", Description = "Hat-shaped bowl and ruffle-edged bowl. Hand blown by a glass artisan.", Price = 52.50m, Category = "bowls", CategoryIcon = false, ImageUrl = "Blue Crackle Glass.jpg", Circa = 1970 },
          new { Id = 2, Name = "Green Beehive Bowl", Description = "Green fired-on beehive-patterned mixing bowl by Jeanette Glass Company.", Price = 325.00m, Category = "bowls", CategoryIcon = false, ImageUrl = "Green Beehive Bowl.jpg", Circa = 1940 },
          new { Id = 3, Name = "Peacock Pattern Bowl", Description = "EAPG bowl with peacock feathers design. Georgia pattern.", Price = 930.00m, Category = "bowls", CategoryIcon = false, ImageUrl = "Peacock Pattern Bowl.jpg", Circa = 1890 },
          new { Id = 4, Name = "Pink Doric Bowl", Description = "Pink depression glass oval bowl. Jeannette Glass Company. Doric pattern.", Price = 1120.00m, Category = "bowls", CategoryIcon = false, ImageUrl = "Pink Doric Bowl.jpg", Circa = 1932 },
          new { Id = 5, Name = "Two-Panel Oval Bowl", Description = "Apple-green EAPG oval bowl by King, Son & Company Pittsburgh, PA.", Price = 1040.00m, Category = "bowls", CategoryIcon = true, ImageUrl = "Two-Panel Oval Bowl.jpg", Circa = 1900 },
          new { Id = 6, Name = "Delphite Coffee Canister", Description = "Jeannette delphite blue coffee canister with zinc lid.", Price = 800.00m, Category = "canisters", CategoryIcon = true, ImageUrl = "Delphite Coffee.jpg", Circa = 1930 },
          new { Id = 7, Name = "Hazel Atlas Flour Canister", Description = "Green fired-on Hazel Atlas Art Deco flour canister with black lid.", Price = 920.00m, Category = "canisters", CategoryIcon = false, ImageUrl = "Hazel Atlas Flour.jpg", Circa = 1940 },
          new { Id = 8, Name = "Hazel Atlas Tea Canister", Description = "Green fired-on Hazel Atlas Art Deco tea canister with black lid.", Price = 115.00m, Category = "canisters", CategoryIcon = false, ImageUrl = "Hazel Atlas Tea.jpg", Circa = 1930 },
          new { Id = 9, Name = "Red Flour Canister", Description = "Fired-on red flour Canister. Hocking glass company.", Price = 280.00m, Category = "canisters", CategoryIcon = false, ImageUrl = "Red Flour.jpg", Circa = 1940 },
          new { Id = 10, Name = "Jadite Tea Canister", Description = "Jadite green tea canister by Jeannette Glass Company. Retro design.", Price = 830.00m, Category = "canisters", CategoryIcon = false, ImageUrl = "Jadite Tea.jpg", Circa = 1930 },
          new { Id = 11, Name = "Adam Shakers", Description = "A pair of depression glass pink shakers. Adam pattern. Square-footed.", Price = 930.00m, Category = "shakers", CategoryIcon = false, ImageUrl = "Adam Shakers.jpg", Circa = 1932 },
          new { Id = 12, Name = "Amber Sharon Shakers", Description = "Amber salt and pepper shakers. Sharon pattern a.k.a Cabbage Rose pattern.", Price = 1120.00m, Category = "shakers", CategoryIcon = false, ImageUrl = "Amber Sharon Shakers.jpg", Circa = 1930 },
          new { Id = 13, Name = "Blue Flour Shaker", Description = "Fired-on blue flour shaker from McKee range set.", Price = 590.00m, Category = "shakers", CategoryIcon = false, ImageUrl = "Blue Flour Shaker.jpg", Circa = 1940 },
          new { Id = 14, Name = "Blue Sugar Shaker", Description = "Fired-on blue sugar shaker. Circa 1950.", Price = 935.00m, Category = "shakers", CategoryIcon = true, ImageUrl = "Blue Sugar Shaker.jpg", Circa = 1940 },
          new { Id = 15, Name = "Blue Pepper Shaker", Description = "Fired-on blue pepper shaker from McKee range set.", Price = 155.00m, Category = "shakers", CategoryIcon = false, ImageUrl = "Blue Pepper Shaker.jpg", Circa = 1940 },
          new { Id = 16, Name = "Green Parrot Plate", Description = "Green plate with parrots pattern. Circa 1932.", Price = 135.00m, Category = "plates", CategoryIcon = false, ImageUrl = "Green Parrot Plate.jpg", Circa = 1935 },
          new { Id = 17, Name = "Red American Sweetheart Plate", Description = "Red plate, American Sweetheart pattern, 16 inches diameter. Circa 1932.", Price = 1125.00m, Category = "plates", CategoryIcon = true, ImageUrl = "Red American Sweetheart Plate.jpg", Circa = 1935 },
          new { Id = 18, Name = "White American Sweetheart Plate", Description = "White plate, American Sweetheart pattern, 16 inches diameter. Gold trim. Circa 1932.", Price = 425.00m, Category = "plates", CategoryIcon = false, ImageUrl = "White American Sweetheart Plate.jpg", Circa = 1935 },
          new { Id = 19, Name = "Amber Diana Plate", Description = "Amber plate with swirl design. Diana pattern. Circa 1932.", Price = 125.00m, Category = "plates", CategoryIcon = false, ImageUrl = "Amber Diana Plate.jpg", Circa = 1932 },
          new { Id = 20, Name = "Yellow Horseshoe Plate", Description = "Yellow plate with horseshoe pattern. Circa 1932.", Price = 625.00m, Category = "plates", CategoryIcon = false, ImageUrl = "Yellow Horseshoe Plate.jpg", Circa = 1932 },
        };

        await connection.ExecuteAsync(
            @"INSERT INTO products (id, name, description, price, category, categoryIcon, imageUrl, circa)
                      VALUES (@Id, @Name, @Description, @Price, @Category, @CategoryIcon, @ImageUrl, @Circa)",
            originalProducts, transaction);

        transaction.Commit();
      }
      catch
      {
        transaction.Rollback();
        throw;
      }
    }

    public async Task<bool> DeleteAsync(int id)
    {
      using var connection = _connectionFactory.CreateConnection();
      connection.Open();
      using var transaction = connection.BeginTransaction();
      try
      {
        // Rule 4: if the item being deleted owns the category icon, we need to
        // hand it off to another item in the same category before/after removal.
        var target = await connection.QuerySingleOrDefaultAsync<Product>(
            $"SELECT {SelectColumns} FROM products WHERE id = @Id", new { Id = id }, transaction);

        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM products WHERE id = @Id", new { Id = id }, transaction);

        if (rowsAffected > 0 && target != null && target.CategoryIcon)
        {
          await connection.ExecuteAsync(
              @"UPDATE products SET categoryIcon = 1
                      WHERE id = (SELECT id FROM products WHERE category = @Category LIMIT 1)",
              new { target.Category }, transaction);
        }

        transaction.Commit();
        return rowsAffected > 0;
      }
      catch
      {
        transaction.Rollback();
        throw;
      }
    }
  }
}