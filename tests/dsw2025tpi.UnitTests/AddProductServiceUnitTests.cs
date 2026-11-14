using System.Linq.Expressions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace dsw2025tpi.UnitTests;

public class AddProductServiceUnitTests
{
    private readonly Mock<IRepository> _repository = new(MockBehavior.Strict);
    private readonly ProductModel.Request _request = new(
        "SKU-001", "INT-001", "Teclado", "Teclado USB", 1250m, 5);
    private readonly ProductsManagementService _service;

    public AddProductServiceUnitTests()
    {
        _service = new ProductsManagementService(
            _repository.Object, NullLogger<ProductsManagementService>.Instance);
    }

    [Fact]
    public async Task AddProduct_WithUniqueSku_PersistsProductAndReturnsItsData()
    {
        // Arrange
        Product? added = null;
        _repository.Setup(repo => repo.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync((Product?)null);
        _repository.Setup(repo => repo.Add(It.IsAny<Product>()))
            .ReturnsAsync((Product product) => added = product);

        // Act
        var result = await _service.AddProduct(_request);

        // Assert
        _repository.Verify(repo => repo.First<Product>(
            It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()), Times.Once);
        _repository.Verify(repo => repo.Add(It.IsAny<Product>()), Times.Once);
        Assert.NotNull(added);
        Assert.NotEqual(Guid.Empty, added.Id);
        Assert.Equal(_request.Sku, added.Sku);
        Assert.Equal(_request.InternalCode, added.InternalCode);
        Assert.Equal(_request.Name, added.Name);
        Assert.Equal(_request.Description, added.Description);
        Assert.Equal(_request.CurrentUnitPrice, added.CurrentUnitPrice);
        Assert.Equal(_request.StockQuantity, added.StockQuantity);
        Assert.True(added.IsActive);
        Assert.Equal(new ProductModel.ResponseProduct(
            added.Id, _request.Sku, _request.InternalCode, _request.Name,
            _request.Description, _request.CurrentUnitPrice, _request.StockQuantity, true), result);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddProduct_WithDuplicateSku_ThrowsWithoutPersisting()
    {
        // Arrange
        var existing = new Product(_request.Sku, "Producto existente", 100m, 1);
        var differentSku = new Product("SKU-002", "Otro producto", 100m, 1);
        _repository.Setup(repo => repo.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
            {
                var matches = predicate.Compile();
                Assert.False(matches(differentSku));
                return matches(existing) ? existing : null;
            });

        // Act
        await Assert.ThrowsAsync<DuplicatedEntityException>(() => _service.AddProduct(_request));

        // Assert
        _repository.Verify(repo => repo.First<Product>(
            It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()), Times.Once);
        _repository.Verify(repo => repo.Add(It.IsAny<Product>()), Times.Never);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddProduct_WithInvalidNumbers_ThrowsWithoutAccessingRepository()
    {
        // Arrange
        const string stockMessage = "La cantidad de stock debe ser mayor o igual a cero";
        var cases = new[]
        {
            (Request: _request with { StockQuantity = 0 }, Message: stockMessage),
            (Request: _request with { StockQuantity = -1 }, Message: stockMessage),
            (Request: _request with { CurrentUnitPrice = -1m }, Message: "El precio unitario ser mayores a cero")
        };

        foreach (var (request, expectedMessage) in cases)
        {
            // Act
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => _service.AddProduct(request));

            // Assert
            Assert.Equal(expectedMessage, exception.Message);
        }

        _repository.VerifyNoOtherCalls();
    }
}
