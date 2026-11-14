using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace dsw2025tpi.UnitTests;

public class CreateOrderServiceUnitTests
{
    private readonly Mock<IRepository> _repository = new(MockBehavior.Strict);
    private readonly OrdersManagementService _service;

    public CreateOrderServiceUnitTests()
    {
        _service = new OrdersManagementService(
            _repository.Object, NullLogger<OrdersManagementService>.Instance);
    }

    [Fact]
    public async Task CreateOrder_WithAvailableProducts_UpdatesStockAndPersistsExactTotal()
    {
        // Arrange
        var customer = new Customer { Name = "Cliente de prueba" };
        var keyboard = new Product("SKU-001", "Teclado", 12.35m, 10);
        var mouse = new Product("SKU-002", "Mouse", 7.19m, 3);
        var request = new OrderRequest(customer.Id, "Envío", "Facturación", null,
            [new(keyboard.Id, 2), new(mouse.Id, 3)]);

        _repository.Setup(repo => repo.GetById<Customer>(customer.Id)).ReturnsAsync(customer);
        _repository.Setup(repo => repo.GetById<Product>(keyboard.Id)).ReturnsAsync(keyboard);
        _repository.Setup(repo => repo.GetById<Product>(mouse.Id)).ReturnsAsync(mouse);
        _repository.Setup(repo => repo.Update(
                It.Is<Product>(product => product == keyboard && product.StockQuantity == 8)))
            .ReturnsAsync(keyboard);
        _repository.Setup(repo => repo.Update(
                It.Is<Product>(product => product == mouse && product.StockQuantity == 0)))
            .ReturnsAsync(mouse);

        Order? persisted = null;
        _repository.Setup(repo => repo.Add(It.IsAny<Order>()))
            .ReturnsAsync((Order order) => persisted = order);

        // Act
        var result = await _service.CreateOrder(request);

        // Assert
        Assert.Equal(8, keyboard.StockQuantity);
        Assert.Equal(0, mouse.StockQuantity);
        _repository.Verify(repo => repo.Update(keyboard), Times.Once);
        _repository.Verify(repo => repo.Update(mouse), Times.Once);
        _repository.Verify(repo => repo.Add(It.IsAny<Order>()), Times.Once);
        Assert.NotNull(persisted);
        Assert.Equal(customer.Id, persisted.CustomerId);
        Assert.Equal(46.27m, persisted.TotalAmount);
        Assert.NotNull(persisted.OrderItems);
        Assert.Collection(persisted.OrderItems,
            item =>
            {
                Assert.Equal(keyboard.Id, item.ProductId);
                Assert.Equal(24.70m, item.Subtotal);
            },
            item =>
            {
                Assert.Equal(mouse.Id, item.ProductId);
                Assert.Equal(21.57m, item.Subtotal);
            });
        Assert.Equal(persisted.Id, result.OrderId);
        Assert.Equal(46.27m, result.TotalAmount);
    }

    [Fact]
    public async Task CreateOrder_WithInsufficientStock_ThrowsWithoutChangingStockOrPersisting()
    {
        // Arrange
        var customer = new Customer { Name = "Cliente de prueba" };
        var product = new Product("SKU-001", "Teclado", 12.35m, 2);
        var request = new OrderRequest(customer.Id, "Envío", "Facturación", null,
            [new(product.Id, 5)]);

        _repository.Setup(repo => repo.GetById<Customer>(customer.Id)).ReturnsAsync(customer);
        _repository.Setup(repo => repo.GetById<Product>(product.Id)).ReturnsAsync(product);

        // Act
        await Assert.ThrowsAsync<InsufficientStockException>(() => _service.CreateOrder(request));

        // Assert
        Assert.Equal(2, product.StockQuantity);
        _repository.Verify(repo => repo.Update(It.IsAny<Product>()), Times.Never);
        _repository.Verify(repo => repo.Add(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrder_WithMissingCustomer_ThrowsBeforeAccessingProducts()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var request = new OrderRequest(customerId, "Envío", "Facturación", null,
            [new(Guid.NewGuid(), 1)]);
        _repository.Setup(repo => repo.GetById<Customer>(customerId)).ReturnsAsync((Customer?)null);

        // Act
        await Assert.ThrowsAsync<CustomerNotFoundException>(() => _service.CreateOrder(request));

        // Assert
        _repository.Verify(repo => repo.GetById<Customer>(customerId), Times.Once);
        _repository.VerifyNoOtherCalls();
    }
}
