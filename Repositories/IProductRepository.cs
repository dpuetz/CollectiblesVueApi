using CollectiblesApi.Models;

namespace CollectiblesApi.Repositories
{
  public interface IProductRepository
  {
    Task<IEnumerable<Product>> GetAllAsync();
    Task<IEnumerable<Product>> GetByCategoryAsync(string category);
    Task<Product?> GetByIdAsync(int id);
    Task<Product> CreateAsync(Product product);
    Task<bool> UpdateAsync(int id, Product product);
    Task<bool> DeleteAsync(int id);
    Task ResetAsync();
  }
}