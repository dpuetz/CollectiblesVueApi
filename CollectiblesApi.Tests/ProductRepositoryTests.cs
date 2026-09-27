using System.Data.Common;
using CollectiblesApi.Data;
using CollectiblesApi.Models;
using CollectiblesApi.Repositories;
using Microsoft.Data.Sqlite;

namespace CollectiblesApi.Tests.Repositories
{
  public class ProductRepositoryTestConnectionFactory : IDbConnectionFactory, IDisposable
  {
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAliveConnection;

    public ProductRepositoryTestConnectionFactory()
    {
      _connectionString = $"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared";
      _keepAliveConnection = new SqliteConnection(_connectionString);
      _keepAliveConnection.Open();

      using var cmd = _keepAliveConnection.CreateCommand();
      cmd.CommandText =
          @"CREATE TABLE products (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    description TEXT,
                    price REAL NOT NULL,
                    category TEXT NOT NULL,
                    categoryIcon INTEGER NOT NULL DEFAULT 0,
                    imageUrl TEXT,
                    circa TEXT
                  );
                  CREATE UNIQUE INDEX idx_products_category_icon
                    ON products(category) WHERE categoryIcon = 1;";
      cmd.ExecuteNonQuery();
    }

    public DbConnection CreateConnection() => new SqliteConnection(_connectionString);

    public void Dispose() => _keepAliveConnection.Dispose();
  }

  public class ProductRepositoryTests : IDisposable
  {
    private readonly ProductRepositoryTestConnectionFactory _factory;
    private readonly ProductRepository _repository;

    public ProductRepositoryTests()
    {
      _factory = new ProductRepositoryTestConnectionFactory();
      _repository = new ProductRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    private static Product MakeProduct(
        string name = "Bowl", string category = "bowls", bool categoryIcon = false,
        decimal price = 10m, string imageUrl = "img.jpg", int circa = 1950) =>
        new Product
        {
          Name = name,
          Description = "desc",
          Price = price,
          Category = category,
          CategoryIcon = categoryIcon,
          ImageUrl = imageUrl,
          Circa = circa
        };

    // ---------- GetAllAsync / GetByCategoryAsync / GetByIdAsync ----------

    [Fact]
    public async Task GetAllAsync_ReturnsAllProducts_OrderedByCategoryThenName()
    {
      await _repository.CreateAsync(MakeProduct(name: "Zebra Bowl", category: "bowls"));
      await _repository.CreateAsync(MakeProduct(name: "Apple Bowl", category: "bowls"));
      await _repository.CreateAsync(MakeProduct(name: "Canister One", category: "canisters"));

      var results = (await _repository.GetAllAsync()).ToList();

      Assert.Equal(3, results.Count);
      Assert.Equal(new[] { "Apple Bowl", "Zebra Bowl", "Canister One" }, results.Select(p => p.Name));
    }

    [Fact]
    public async Task GetByCategoryAsync_ReturnsOnlyMatchingCategory()
    {
      await _repository.CreateAsync(MakeProduct(name: "Bowl One", category: "bowls"));
      await _repository.CreateAsync(MakeProduct(name: "Plate One", category: "plates"));

      var results = (await _repository.GetByCategoryAsync("bowls")).ToList();

      Assert.Single(results);
      Assert.Equal("Bowl One", results[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsProduct()
    {
      var created = await _repository.CreateAsync(MakeProduct(name: "Findable Bowl"));

      var found = await _repository.GetByIdAsync(created.Id);

      Assert.NotNull(found);
      Assert.Equal("Findable Bowl", found!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
      var found = await _repository.GetByIdAsync(9999);

      Assert.Null(found);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_AssignsGeneratedId()
    {
      var created = await _repository.CreateAsync(MakeProduct());

      Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task CreateAsync_LowercasesCategory()
    {
      var created = await _repository.CreateAsync(MakeProduct(category: "BOWLS"));

      Assert.Equal("bowls", created.Category);
    }

    [Fact]
    public async Task CreateAsync_FirstItemInCategory_ForcesCategoryIconTrue_RegardlessOfInput()
    {
      var created = await _repository.CreateAsync(MakeProduct(category: "bowls", categoryIcon: false));

      Assert.True(created.CategoryIcon);
    }

    [Fact]
    public async Task CreateAsync_SecondItemNotRequestingIcon_LeavesExistingIconOwnerUntouched()
    {
      var first = await _repository.CreateAsync(MakeProduct(name: "First", category: "bowls"));
      var second = await _repository.CreateAsync(MakeProduct(name: "Second", category: "bowls", categoryIcon: false));

      var firstReloaded = await _repository.GetByIdAsync(first.Id);

      Assert.False(second.CategoryIcon);
      Assert.True(firstReloaded!.CategoryIcon);
    }

    [Fact]
    public async Task CreateAsync_NewItemRequestsIcon_ClearsIconFromOtherItemsInCategory()
    {
      var first = await _repository.CreateAsync(MakeProduct(name: "First", category: "bowls"));
      var second = await _repository.CreateAsync(MakeProduct(name: "Second", category: "bowls", categoryIcon: true));

      var firstReloaded = await _repository.GetByIdAsync(first.Id);

      Assert.True(second.CategoryIcon);
      Assert.False(firstReloaded!.CategoryIcon);
    }

    [Fact]
    public async Task CreateAsync_DoesNotAffectIconInDifferentCategory()
    {
      var bowl = await _repository.CreateAsync(MakeProduct(name: "Bowl", category: "bowls"));
      await _repository.CreateAsync(MakeProduct(name: "Plate", category: "plates", categoryIcon: true));

      var bowlReloaded = await _repository.GetByIdAsync(bowl.Id);

      Assert.True(bowlReloaded!.CategoryIcon);
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesFieldsAndReturnsTrue()
    {
      var created = await _repository.CreateAsync(MakeProduct(name: "Original"));

      var updated = MakeProduct(name: "Updated Name", category: "bowls", price: 55.5m);
      var success = await _repository.UpdateAsync(created.Id, updated);

      var reloaded = await _repository.GetByIdAsync(created.Id);
      Assert.True(success);
      Assert.Equal("Updated Name", reloaded!.Name);
      Assert.Equal(55.5m, reloaded.Price);
    }

    [Fact]
    public async Task UpdateAsync_NonExistingId_ReturnsFalse()
    {
      var success = await _repository.UpdateAsync(9999, MakeProduct());

      Assert.False(success);
    }

    [Fact]
    public async Task UpdateAsync_OnlyItemInCategory_ForcesCategoryIconTrue()
    {
      var created = await _repository.CreateAsync(MakeProduct(category: "bowls"));

      await _repository.UpdateAsync(created.Id, MakeProduct(category: "bowls", categoryIcon: false));

      var reloaded = await _repository.GetByIdAsync(created.Id);
      Assert.True(reloaded!.CategoryIcon);
    }

    [Fact]
    public async Task UpdateAsync_SettingIconTrue_ClearsOtherIconHolderInCategory()
    {
      var first = await _repository.CreateAsync(MakeProduct(name: "First", category: "bowls"));
      var second = await _repository.CreateAsync(MakeProduct(name: "Second", category: "bowls", categoryIcon: false));

      await _repository.UpdateAsync(second.Id, MakeProduct(name: "Second", category: "bowls", categoryIcon: true));

      var firstReloaded = await _repository.GetByIdAsync(first.Id);
      var secondReloaded = await _repository.GetByIdAsync(second.Id);
      Assert.False(firstReloaded!.CategoryIcon);
      Assert.True(secondReloaded!.CategoryIcon);
    }

    [Fact]
    public async Task UpdateAsync_MovingCategoryWithOnlyMemberThere_ForcesIconTrueInNewCategory()
    {
      var product = await _repository.CreateAsync(MakeProduct(category: "bowls"));

      await _repository.UpdateAsync(product.Id, MakeProduct(category: "plates", categoryIcon: false));

      var reloaded = await _repository.GetByIdAsync(product.Id);
      Assert.Equal("plates", reloaded!.Category);
      Assert.True(reloaded.CategoryIcon);
    }

    // ---------- DeleteAsync ----------

    [Fact]
    public async Task DeleteAsync_ExistingId_ReturnsTrue_AndRemovesRow()
    {
      var created = await _repository.CreateAsync(MakeProduct());

      var success = await _repository.DeleteAsync(created.Id);

      Assert.True(success);
      Assert.Null(await _repository.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_ReturnsFalse()
    {
      var success = await _repository.DeleteAsync(9999);

      Assert.False(success);
    }

    [Fact]
    public async Task DeleteAsync_DeletingIconOwner_ReassignsIconToRemainingItemInCategory()
    {
      var first = await _repository.CreateAsync(MakeProduct(name: "First", category: "bowls"));
      var second = await _repository.CreateAsync(MakeProduct(name: "Second", category: "bowls", categoryIcon: false));

      await _repository.DeleteAsync(first.Id);

      var secondReloaded = await _repository.GetByIdAsync(second.Id);
      Assert.True(secondReloaded!.CategoryIcon);
    }

    [Fact]
    public async Task DeleteAsync_DeletingLastItemInCategory_DoesNotThrow()
    {
      var only = await _repository.CreateAsync(MakeProduct(category: "bowls"));

      var success = await _repository.DeleteAsync(only.Id);

      Assert.True(success);
    }

    [Fact]
    public async Task DeleteAsync_DeletingNonIconItem_DoesNotAffectExistingIconOwner()
    {
      var first = await _repository.CreateAsync(MakeProduct(name: "First", category: "bowls"));
      var second = await _repository.CreateAsync(MakeProduct(name: "Second", category: "bowls", categoryIcon: false));

      await _repository.DeleteAsync(second.Id);

      var firstReloaded = await _repository.GetByIdAsync(first.Id);
      Assert.True(firstReloaded!.CategoryIcon);
    }
  }
}