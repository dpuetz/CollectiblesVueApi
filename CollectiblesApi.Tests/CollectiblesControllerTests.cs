using System.Text;
using System.Text.Json;
using CollectiblesApi.Controllers;
using CollectiblesApi.Models;
using CollectiblesApi.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CollectiblesApi.Tests.Controllers
{
  public class CollectiblesControllerTests : IDisposable
  {
    private readonly Mock<IProductRepository> _mockRepo;
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly Mock<ILogger<CollectiblesController>> _mockLogger;
    private readonly CollectiblesController _controller;
    private readonly string _tempRoot;

    public CollectiblesControllerTests()
    {
      _tempRoot = Path.Combine(Path.GetTempPath(), "CollectiblesApiTests_" + Guid.NewGuid());
      Directory.CreateDirectory(_tempRoot);

      _mockRepo = new Mock<IProductRepository>();
      _mockEnv = new Mock<IWebHostEnvironment>();
      _mockEnv.Setup(e => e.ContentRootPath).Returns(_tempRoot);
      _mockLogger = new Mock<ILogger<CollectiblesController>>();

      _controller = new CollectiblesController(_mockRepo.Object, _mockEnv.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
      if (Directory.Exists(_tempRoot))
      {
        Directory.Delete(_tempRoot, recursive: true);
      }
    }

    private static Mock<IFormFile> CreateFormFile(string fileName, byte[] content)
    {
      var mockFile = new Mock<IFormFile>();
      mockFile.Setup(f => f.FileName).Returns(fileName);
      mockFile.Setup(f => f.Length).Returns(content.LongLength);
      mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
          .Returns<Stream, CancellationToken>((stream, _) => stream.WriteAsync(content, 0, content.Length));
      return mockFile;
    }

    private static string BuildItemJson(string category, bool categoryIcon = false) =>
        JsonSerializer.Serialize(new
        {
          Name = "Test Bowl",
          Description = "A test bowl",
          Price = 19.99m,
          Category = category,
          CategoryIcon = categoryIcon,
          Circa = 1950
        });

    // ---------- GetItems ----------

    [Fact]
    public async Task GetItems_NoCategory_ReturnsAllItems()
    {
      var items = new List<Product> { new Product { Id = 1, Name = "Bowl A", Category = "bowls" } };
      _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(items);

      var result = await _controller.GetItems(null);

      var okResult = Assert.IsType<OkObjectResult>(result);
      Assert.Same(items, okResult.Value);
      _mockRepo.Verify(r => r.GetAllAsync(), Times.Once);
      _mockRepo.Verify(r => r.GetByCategoryAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetItems_WithCategory_ReturnsFilteredItems()
    {
      var items = new List<Product> { new Product { Id = 2, Name = "Canister A", Category = "canisters" } };
      _mockRepo.Setup(r => r.GetByCategoryAsync("canisters")).ReturnsAsync(items);

      var result = await _controller.GetItems("canisters");

      var okResult = Assert.IsType<OkObjectResult>(result);
      Assert.Same(items, okResult.Value);
      _mockRepo.Verify(r => r.GetByCategoryAsync("canisters"), Times.Once);
      _mockRepo.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task GetItems_WhitespaceCategory_TreatedAsNoCategory()
    {
      _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<Product>());

      var result = await _controller.GetItems("   ");

      Assert.IsType<OkObjectResult>(result);
      _mockRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task GetItems_RepositoryThrows_Returns500()
    {
      _mockRepo.Setup(r => r.GetAllAsync()).ThrowsAsync(new InvalidOperationException("db down"));

      var result = await _controller.GetItems(null);

      var statusResult = Assert.IsType<ObjectResult>(result);
      Assert.Equal(500, statusResult.StatusCode);
    }

    // ---------- GetItem ----------

    [Fact]
    public async Task GetItem_ExistingId_ReturnsOk()
    {
      var product = new Product { Id = 5, Name = "Bowl" };
      _mockRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(product);

      var result = await _controller.GetItem(5);

      var okResult = Assert.IsType<OkObjectResult>(result);
      Assert.Same(product, okResult.Value);
    }

    [Fact]
    public async Task GetItem_NonExistingId_ReturnsNotFound()
    {
      _mockRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Product?)null);

      var result = await _controller.GetItem(99);

      Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetItem_RepositoryThrows_Returns500()
    {
      _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ThrowsAsync(new Exception("boom"));

      var result = await _controller.GetItem(1);

      var statusResult = Assert.IsType<ObjectResult>(result);
      Assert.Equal(500, statusResult.StatusCode);
    }

    // ---------- CreateItem ----------

    [Fact]
    public async Task CreateItem_ValidRequest_ReturnsCreatedAtAction_AndSavesFileToCategoryFolder()
    {
      var fileBytes = Encoding.UTF8.GetBytes("fake-image-bytes");
      var request = new CreateItemRequest
      {
        Item = BuildItemJson("bowls"),
        File = CreateFormFile("photo.jpg", fileBytes).Object
      };

      _mockRepo.Setup(r => r.CreateAsync(It.IsAny<Product>()))
          .ReturnsAsync((Product p) => { p.Id = 42; return p; });

      var result = await _controller.CreateItem(request);

      var created = Assert.IsType<CreatedAtActionResult>(result);
      Assert.Equal(nameof(CollectiblesController.GetItem), created.ActionName);
      var returned = Assert.IsType<Product>(created.Value);
      Assert.Equal(42, returned.Id);
      Assert.Equal("photo.jpg", returned.ImageUrl);

      var savedPath = Path.Combine(_tempRoot, "Images", "bowls", "photo.jpg");
      Assert.True(File.Exists(savedPath));
      Assert.Equal(fileBytes, await File.ReadAllBytesAsync(savedPath));

      _mockRepo.Verify(r => r.CreateAsync(It.Is<Product>(p => p.ImageUrl == "photo.jpg")), Times.Once);
    }

    [Fact]
    public async Task CreateItem_ItemDeserializesToNull_ReturnsBadRequest()
    {
      var request = new CreateItemRequest
      {
        Item = "null",
        File = CreateFormFile("photo.jpg", new byte[] { 1 }).Object
      };

      var result = await _controller.CreateItem(request);

      Assert.IsType<BadRequestObjectResult>(result);
      _mockRepo.Verify(r => r.CreateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task CreateItem_MalformedJson_Returns500()
    {
      // JsonSerializer throws JsonException, which the controller's catch-all turns into a 500,
      // not a BadRequest -- this pins down that (arguably surprising) behavior.
      var request = new CreateItemRequest
      {
        Item = "{ not valid json",
        File = CreateFormFile("photo.jpg", new byte[] { 1 }).Object
      };

      var result = await _controller.CreateItem(request);

      var statusResult = Assert.IsType<ObjectResult>(result);
      Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreateItem_UnknownCategory_ReturnsBadRequest()
    {
      var request = new CreateItemRequest
      {
        Item = BuildItemJson("teapots"),
        File = CreateFormFile("photo.jpg", new byte[] { 1 }).Object
      };

      var result = await _controller.CreateItem(request);

      var badRequest = Assert.IsType<BadRequestObjectResult>(result);
      Assert.Contains("teapots", badRequest.Value!.ToString());
      _mockRepo.Verify(r => r.CreateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task CreateItem_NoFile_ReturnsBadRequest()
    {
      var request = new CreateItemRequest { Item = BuildItemJson("bowls"), File = null! };

      var result = await _controller.CreateItem(request);

      Assert.IsType<BadRequestObjectResult>(result);
      _mockRepo.Verify(r => r.CreateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task CreateItem_EmptyFile_ReturnsBadRequest()
    {
      var request = new CreateItemRequest
      {
        Item = BuildItemJson("bowls"),
        File = CreateFormFile("photo.jpg", Array.Empty<byte>()).Object
      };

      var result = await _controller.CreateItem(request);

      Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateItem_RepositoryThrows_Returns500()
    {
      var request = new CreateItemRequest
      {
        Item = BuildItemJson("bowls"),
        File = CreateFormFile("photo.jpg", new byte[] { 1 }).Object
      };
      _mockRepo.Setup(r => r.CreateAsync(It.IsAny<Product>())).ThrowsAsync(new Exception("db error"));

      var result = await _controller.CreateItem(request);

      var statusResult = Assert.IsType<ObjectResult>(result);
      Assert.Equal(500, statusResult.StatusCode);
    }

    // ---------- UpdateItem ----------

    [Fact]
    public async Task UpdateItem_ExistingId_ReturnsNoContent()
    {
      _mockRepo.Setup(r => r.UpdateAsync(5, It.IsAny<Product>())).ReturnsAsync(true);

      var result = await _controller.UpdateItem(5, new Product { Id = 5 });

      Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UpdateItem_NonExistingId_ReturnsNotFound()
    {
      _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<int>(), It.IsAny<Product>())).ReturnsAsync(false);

      var result = await _controller.UpdateItem(99, new Product { Id = 99 });

      Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateItem_RepositoryThrows_Returns500()
    {
      _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<int>(), It.IsAny<Product>()))
          .ThrowsAsync(new Exception("boom"));

      var result = await _controller.UpdateItem(1, new Product { Id = 1 });

      var statusResult = Assert.IsType<ObjectResult>(result);
      Assert.Equal(500, statusResult.StatusCode);
    }

    // ---------- DeleteItem ----------

    [Fact]
    public async Task DeleteItem_ExistingItemWithImage_ReturnsNoContent_AndDeletesImageFile()
    {
      var imageFolder = Path.Combine(_tempRoot, "Images", "bowls");
      Directory.CreateDirectory(imageFolder);
      var imagePath = Path.Combine(imageFolder, "photo.jpg");
      await File.WriteAllTextAsync(imagePath, "fake image");

      var product = new Product { Id = 5, Category = "bowls", ImageUrl = "photo.jpg" };
      _mockRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(product);
      _mockRepo.Setup(r => r.DeleteAsync(5)).ReturnsAsync(true);

      var result = await _controller.DeleteItem(5);

      Assert.IsType<NoContentResult>(result);
      Assert.False(File.Exists(imagePath));
    }

    [Fact]
    public async Task DeleteItem_ItemNotFound_ReturnsNotFound_AndDoesNotCallDelete()
    {
      _mockRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Product?)null);

      var result = await _controller.DeleteItem(99);

      Assert.IsType<NotFoundObjectResult>(result);
      _mockRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteItem_RepositoryDeleteReturnsFalse_ReturnsNotFound()
    {
      var product = new Product { Id = 5, Category = "bowls", ImageUrl = "photo.jpg" };
      _mockRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(product);
      _mockRepo.Setup(r => r.DeleteAsync(5)).ReturnsAsync(false);

      var result = await _controller.DeleteItem(5);

      Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteItem_NoImageUrl_ReturnsNoContent_WithoutTouchingFilesystem()
    {
      var product = new Product { Id = 5, Category = "bowls", ImageUrl = null };
      _mockRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(product);
      _mockRepo.Setup(r => r.DeleteAsync(5)).ReturnsAsync(true);

      var result = await _controller.DeleteItem(5);

      Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteItem_ImageFileMissingOnDisk_StillReturnsNoContent()
    {
      var product = new Product { Id = 5, Category = "bowls", ImageUrl = "already-gone.jpg" };
      _mockRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(product);
      _mockRepo.Setup(r => r.DeleteAsync(5)).ReturnsAsync(true);

      var result = await _controller.DeleteItem(5);

      Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteItem_GetByIdThrows_Returns500()
    {
      _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ThrowsAsync(new Exception("boom"));

      var result = await _controller.DeleteItem(1);

      var statusResult = Assert.IsType<ObjectResult>(result);
      Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task DeleteItem_DeleteAsyncThrows_Returns500()
    {
      var product = new Product { Id = 5, Category = "bowls", ImageUrl = "photo.jpg" };
      _mockRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(product);
      _mockRepo.Setup(r => r.DeleteAsync(5)).ThrowsAsync(new Exception("boom"));

      var result = await _controller.DeleteItem(5);

      var statusResult = Assert.IsType<ObjectResult>(result);
      Assert.Equal(500, statusResult.StatusCode);
    }
  }
}