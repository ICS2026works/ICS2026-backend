using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Xunit;
using FluentAssertions;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Api; //CWAF factory con program

namespace dsw2025tpi.IntegrationTests;

[Collection("IntegrationTests")] //para compartir DB sqlite entre tests
public class CreateOrdersServiceIntegrationTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public CreateOrdersServiceIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_WhenUnauthorized_ReturnsUnauthorized()
    {
        var request = new { CustomerId = Guid.NewGuid(), Items = new[] { new { ProductId = Guid.NewGuid(), Quantity = 1 } } };
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateOrder_WhenValidData_ReturnsCreatedAndUpdatesStockInSqlite()
    {
        using var scope = _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

        // Crear producto ACTIVO
        var product = new Product(
            $"SKU-{Guid.NewGuid().ToString().Substring(0, 8)}",
            "Producto Test",
            100,
            10,
            "Test",
            "INT-01"
        );

        dbContext.Set<Product>().Add(product);

        var customerEmail = $"cliente_{Guid.NewGuid()}@test.com";

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Cliente Test",
            Email = customerEmail
        };

        dbContext.Set<Customer>().Add(customer);

        await dbContext.SaveChangesAsync();

        // Verificación de apoyo
        (await dbContext.Set<Product>()
            .AnyAsync(p => p.Id == product.Id))
            .Should().BeTrue();

        (await dbContext.Set<Customer>()
            .AnyAsync(c => c.Id == customer.Id))
            .Should().BeTrue();

        var token =
            await AuthenticateUserAsync(
                customerEmail,
                "cliente_test");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var orderRequest = new
        {
            CustomerId = customer.Id,
            ShippingAddress = "Calle Falsa 123",
            BillingAddress = "Calle Falsa 123",
            Items = new[]
            {
            new
            {
                ProductId = product.Id,
                Quantity = 3
            }
        }
        };

        var response =
            await _client.PostAsJsonAsync(
                "/api/orders",
                orderRequest);

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"La respuesta fue: {body}");

        response.Headers.Location.Should().NotBeNull();

        dbContext.ChangeTracker.Clear();

        var updatedProduct =
            await dbContext.Set<Product>()
                .FindAsync(product.Id);

        updatedProduct.Should().NotBeNull();

        updatedProduct!.StockQuantity.Should().Be(7);

        var orderInDb =
            await dbContext.Set<Order>()
                .FirstOrDefaultAsync(
                    o => o.CustomerId == customer.Id);

        orderInDb.Should().NotBeNull();

        orderInDb!.TotalAmount.Should().Be(300);
    }
    

    [Fact]
    public async Task CreateOrder_WhenStockIsInsufficient_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

        var customerEmail = $"cliente_{Guid.NewGuid()}@test.com";
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Cliente Test", Email = customerEmail };
        dbContext.Set<Customer>().Add(customer);

        var product = new Product("SKU-ESCASO", "Producto Escaso", 50, 2, "Test", "INT-02");
        dbContext.Set<Product>().Add(product);

        await dbContext.SaveChangesAsync();
        Console.WriteLine($"PRODUCT ID: {product.Id}");

        var verifyProduct = await dbContext.Set<Product>()
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        Console.WriteLine($"PRODUCT FOUND IN TEST DB: {verifyProduct != null}");

        var token = await AuthenticateUserAsync(customerEmail, $"user_{Guid.NewGuid().ToString().Substring(0, 5)}");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var orderRequest = new
        {
            CustomerId = customer.Id,
            ShippingAddress = "Calle Falsa 123",
            BillingAddress = "Calle Falsa 123",
            Items = new[] { new { ProductId = product.Id, Quantity = 5 } }
        };

        var response = await _client.PostAsJsonAsync("/api/orders", orderRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("stock");

        dbContext.ChangeTracker.Clear();
        var unchangedProduct = await dbContext.Set<Product>().FindAsync(product.Id);
        unchangedProduct!.StockQuantity.Should().Be(2);

        var orderInDb = await dbContext.Set<Order>().FirstOrDefaultAsync(o => o.CustomerId == customer.Id);
        orderInDb.Should().BeNull();
    }

    private async Task<string> AuthenticateUserAsync(string email, string username)
    {
        using var scope = _factory.Services.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("USER"))
            await roleManager.CreateAsync(new IdentityRole("USER"));

        var user = new IdentityUser { UserName = username, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, "Password123!");
        await userManager.AddToRoleAsync(user, "USER");

        // Login
        var loginRequest = new { Username = username, Password = "Password123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        loginResponse.EnsureSuccessStatusCode();
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();

        return loginContent!.Token;
    }

    private class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
    }
}
