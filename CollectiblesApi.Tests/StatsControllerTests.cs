using CollectiblesApi.Controllers;
using CollectiblesApi.Models;
using CollectiblesApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CollectiblesApi.Tests.Controllers
{
  public class StatsControllerTests
  {
    private readonly Mock<IVisitStatsRepository> _mockRepo;
    private readonly StatsController _controller;

    public StatsControllerTests()
    {
      _mockRepo = new Mock<IVisitStatsRepository>();
      _controller = new StatsController(_mockRepo.Object);
    }

    [Fact]
    public async Task GetVisitStats_ReturnsOkResult_WithSummary()
    {
      // Arrange
      var expectedSummary = new VisitSummary
      {
        TotalRequests = 42,
        UniqueIps = 7,
        Recent = new List<RequestLogEntry>
                {
                    new RequestLogEntry
                    {
                        Id = 1,
                        Timestamp = DateTime.UtcNow.ToString("o"),
                        IpAddress = "203.0.113.5",
                        Method = "GET",
                        Path = "/api/items"
                    }
                }
      };
      _mockRepo.Setup(r => r.GetSummaryAsync()).ReturnsAsync(expectedSummary);

      // Act
      var result = await _controller.GetVisitStats();

      // Assert
      var okResult = Assert.IsType<OkObjectResult>(result);
      var summary = Assert.IsType<VisitSummary>(okResult.Value);
      Assert.Equal(42, summary.TotalRequests);
      Assert.Equal(7, summary.UniqueIps);
      Assert.Single(summary.Recent);
    }

    [Fact]
    public async Task GetVisitStats_ReturnsOkResult_WhenNoDataYet()
    {
      // Arrange
      var emptySummary = new VisitSummary
      {
        TotalRequests = 0,
        UniqueIps = 0,
        Recent = Enumerable.Empty<RequestLogEntry>()
      };
      _mockRepo.Setup(r => r.GetSummaryAsync()).ReturnsAsync(emptySummary);

      // Act
      var result = await _controller.GetVisitStats();

      // Assert
      var okResult = Assert.IsType<OkObjectResult>(result);
      var summary = Assert.IsType<VisitSummary>(okResult.Value);
      Assert.Equal(0, summary.TotalRequests);
      Assert.Empty(summary.Recent);
    }

    [Fact]
    public async Task GetVisitStats_CallsRepositoryExactlyOnce()
    {
      // Arrange
      _mockRepo.Setup(r => r.GetSummaryAsync()).ReturnsAsync(new VisitSummary());

      // Act
      await _controller.GetVisitStats();

      // Assert
      _mockRepo.Verify(r => r.GetSummaryAsync(), Times.Once);
    }
  }
}