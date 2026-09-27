using System.Text.Json;
using CollectiblesApi.Models;
using CollectiblesApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace CollectiblesApi.Controllers
{
  [ApiController]
  [Route("api")]
  public class CollectiblesController : ControllerBase
  {
    private readonly IProductRepository _repository;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CollectiblesController> _logger;

    public CollectiblesController(IProductRepository repository, IWebHostEnvironment env, ILogger<CollectiblesController> logger)
    {
      _repository = repository;
      _env = env;
      _logger = logger;
    }

    // GET api/items
    // GET api/items?category=bowls
    [HttpGet("items")]
    [AllowAnonymous]
    public async Task<IActionResult> GetItems([FromQuery] string? category = null)
    {
      try
      {
        var items = string.IsNullOrWhiteSpace(category)
            ? await _repository.GetAllAsync()
            : await _repository.GetByCategoryAsync(category);

        return Ok(items);
      }
      catch (Exception ex)
      {
        return StatusCode(500, ex.Message);
      }
    }

    // GET api/items/5
    [HttpGet("items/{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetItem(int id)
    {
      try
      {
        var item = await _repository.GetByIdAsync(id);
        return item is null ? NotFound($"No product found with id {id}") : Ok(item);
      }
      catch (Exception ex)
      {
        return StatusCode(500, ex.Message);
      }
    }

    // POST api/items
    private static readonly Dictionary<string, string> CategoryFolders = new(StringComparer.OrdinalIgnoreCase)
    {
      ["bowls"] = "bowls",
      ["canisters"] = "canisters",
      ["plates"] = "plates",
      ["shakers"] = "shakers",
    };

    [HttpPost("items")]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateItem([FromForm] CreateItemRequest request)
    {
      try
      {
        var newItem = JsonSerializer.Deserialize<Product>(
            request.Item,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (newItem == null)
        {
          return BadRequest("Invalid item data.");
        }

        if (!CategoryFolders.TryGetValue(newItem.Category, out var folderName))
        {
          return BadRequest($"Unknown category: {newItem.Category}");
        }

        if (request.File == null || request.File.Length == 0)
        {
          return BadRequest("No file uploaded.");
        }

        var imagesRoot = Path.Combine(_env.ContentRootPath, "Images");
        var targetFolder = Path.Combine(imagesRoot, folderName);
        Directory.CreateDirectory(targetFolder);

        var safeFileName = Path.GetFileName(request.File.FileName);
        var targetPath = Path.Combine(targetFolder, safeFileName);

        using (var stream = new FileStream(targetPath, FileMode.Create))
        {
          await request.File.CopyToAsync(stream);
        }

        newItem.ImageUrl = safeFileName;

        var created = await _repository.CreateAsync(newItem);
        return CreatedAtAction(nameof(GetItem), new { id = created.Id }, created);
      }
      catch (Exception ex)
      {
        return StatusCode(500, ex.Message);
      }
    }

    // PUT api/items/5
    [HttpPut("items/{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] Product updatedItem)
    {
      try
      {
        var success = await _repository.UpdateAsync(id, updatedItem);
        return success ? NoContent() : NotFound($"No product found with id {id}");
      }
      catch (Exception ex)
      {
        return StatusCode(500, ex.Message);
      }
    }


    // DELETE api/items/5
    [HttpDelete("items/{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteItem(int id)
    {
      try
      {
        var item = await _repository.GetByIdAsync(id);
        if (item == null)
        {
          return NotFound($"No product found with id {id}");
        }

        var success = await _repository.DeleteAsync(id);
        if (!success)
        {
          return NotFound($"No product found with id {id}");
        }

        if (CategoryFolders.TryGetValue(item.Category, out var folderName) &&
            !string.IsNullOrWhiteSpace(item.ImageUrl))
        {
          var imagePath = Path.Combine(_env.ContentRootPath, "Images", folderName, item.ImageUrl);
          try
          {
            if (System.IO.File.Exists(imagePath))
            {
              System.IO.File.Delete(imagePath);
            }
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to delete image file at {ImagePath} for item {Id}", imagePath, id);
          }
        }

        return NoContent();
      }
      catch (Exception ex)
      {
        return StatusCode(500, ex.Message);
      }
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset()
    {
      try
      {
        await _repository.ResetAsync();

        var imagesRoot = Path.Combine(_env.ContentRootPath, "Images");
        var originalImagesRoot = Path.Combine(_env.ContentRootPath, "ImagesOriginal");

        if (!Directory.Exists(originalImagesRoot))
        {
          return StatusCode(500, "ImagesOriginal folder not found; cannot reset images.");
        }

        if (Directory.Exists(imagesRoot))
        {
          Directory.Delete(imagesRoot, recursive: true);
        }

        CopyDirectory(originalImagesRoot, imagesRoot);

        return NoContent();
      }
      catch (Exception ex)
      {
        return StatusCode(500, ex.Message);
      }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
      Directory.CreateDirectory(destDir);

      foreach (var filePath in Directory.GetFiles(sourceDir))
      {
        var destFilePath = Path.Combine(destDir, Path.GetFileName(filePath));
        System.IO.File.Copy(filePath, destFilePath, overwrite: true);
      }

      foreach (var subDir in Directory.GetDirectories(sourceDir))
      {
        var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
        CopyDirectory(subDir, destSubDir);
      }
    }

  }
}