using System.Linq.Expressions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace dsw2025tpi.UnitTests;

public class GetProductsServiceUnitTests
{
    private static Mock<IRepository> CreateRepositoryMock(IEnumerable<Product> products)
    {
        var repoMock = new Mock<IRepository>();

        repoMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
            {
                var compiled = predicate.Compile();
                return products.Where(compiled).ToList();
            });

        return repoMock;
    }

    private static ProductsManagementService CreateService(IEnumerable<Product> products)
    {
        var repoMock = CreateRepositoryMock(products);
        var loggerMock = new Mock<ILogger<ProductsManagementService>>();
        return new ProductsManagementService(repoMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task GetProducts_WhenIncludeInactiveIsFalse_ExcludesInactiveProducts()
    {
        // Arrange
        var products = new List<Product>
        {
            new("SKU-01", "Producto Activo 1", 100m, 10, "Desc", "INT-01") { IsActive = true },
            new("SKU-02", "Producto Inactivo", 200m, 5, "Desc", "INT-02") { IsActive = false },
            new("SKU-03", "Producto Activo 2", 300m, 8, "Desc", "INT-03") { IsActive = true }
        };

        var service = CreateService(products);
        var filter = new ProductModel.FilterProduct(
            Status: null,
            Search: null,
            PageNumber: 1,
            PageSize: 10
        );

        // Act
        var result = await service.GetProducts(filter, includeInactive: false);

        // Assert
        result.Should().NotBeNull();
        result.ProductItems.Should().HaveCount(2);
        result.ProductItems.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public async Task GetProducts_WithPagination_ReturnsRequestedPageAndTotalCount()
    {
        // Arrange
        var products = Enumerable.Range(1, 15)
            .Select(i => new Product($"SKU-{i:00}", $"Producto {i:00}", 50m * i, 10, "Desc", $"INT-{i:00}")
            {
                IsActive = true
            })
            .ToList();

        var service = CreateService(products);
        var filter = new ProductModel.FilterProduct(
            Status: null,
            Search: null,
            PageNumber: 2,
            PageSize: 5
        );

        // Act
        var result = await service.GetProducts(filter, includeInactive: false);

        // Assert
        result.Should().NotBeNull();
        result.ProductItems.Should().HaveCount(5);
        result.Total.Should().Be(15);
    }

    [Fact]
    public async Task GetProducts_WithSearchTerm_FiltersByNameSkuOrInternalCode()
    {
        // Arrange
        var products = new List<Product>
        {
            new("SKU-001", "Mouse Gamer", 150m, 10, "Desc", "INT-10") { IsActive = true },
            new("SKU-002", "Teclado Mecanico", 250m, 10, "Desc", "INT-20") { IsActive = true },
            new("SKU-003", "Monitor", 500m, 10, "Desc", "Mouse-99") { IsActive = true }
        };

        var service = CreateService(products);
        var filter = new ProductModel.FilterProduct(
            Status: null,
            Search: "Mouse",
            PageNumber: 1,
            PageSize: 10
        );

        // Act
        var result = await service.GetProducts(filter, includeInactive: false);

        // Assert
        result.Should().NotBeNull();
        result.ProductItems.Should().HaveCount(2);
    }
}