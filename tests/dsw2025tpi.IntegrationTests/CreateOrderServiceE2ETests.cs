using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

using Dsw2025Tpi.Api;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Application.Dtos;

namespace dsw2025tpi.IntegrationTests;

[Collection("IntegrationTests")]
public class CreateOrderServiceE2ETests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public CreateOrderServiceE2ETests(
        CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_EndToEndFlow_ShouldLoginCreateOrderAndVerifyReducedStock()
    {
        // ARRANGE

        using var scope = _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<Dsw2025TpiContext>();

        var product = new Product(
            $"SKU-{Guid.NewGuid().ToString()[..8]}",
            "Producto E2E",
            100,
            10,
            "Categoria Test",
            "INT-E2E"
        );

        dbContext.Set<Product>().Add(product);

        var customerEmail =
            $"cliente_{Guid.NewGuid()}@test.com";

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Cliente E2E",
            Email = customerEmail
        };

        dbContext.Set<Customer>().Add(customer);

        await dbContext.SaveChangesAsync();

        // STEP 1 - LOGIN

        var token = await AuthenticateUserAsync(
            customerEmail,
            $"user_{Guid.NewGuid():N}".Substring(0, 8));

        token.Should().NotBeNullOrWhiteSpace();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        // STEP 2 - CREATE ORDER

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

        var createOrderResponse =
            await _client.PostAsJsonAsync(
                "/api/orders",
                orderRequest);

        var responseBody =
            await createOrderResponse.Content
                .ReadAsStringAsync();

        createOrderResponse.StatusCode
            .Should()
            .Be(
                HttpStatusCode.Created,
                responseBody);

        createOrderResponse.Headers.Location
            .Should()
            .NotBeNull();

        // STEP 3 - GET PRODUCT

        var getProductResponse =
            await _client.GetAsync(
                $"/api/products/{product.Id}");

        getProductResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.OK);

        var productResult =
            await getProductResponse.Content
                .ReadFromJsonAsync<ProductModel.ResponseProduct>();

        productResult.Should().NotBeNull();

        // ASSERT

        productResult!.StockQuantity
            .Should()
            .Be(7);
    }

    private async Task<string> AuthenticateUserAsync(
        string email,
        string username)
    {
        using var scope =
            _factory.Services.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();

        var roleManager =
            scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("USER"))
        {
            await roleManager.CreateAsync(
                new IdentityRole("USER"));
        }

        var user = new IdentityUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = true
        };

        await userManager.CreateAsync(
            user,
            "Password123!");

        await userManager.AddToRoleAsync(
            user,
            "USER");

        var loginRequest = new
        {
            Username = username,
            Password = "Password123!"
        };

        var loginResponse =
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                loginRequest);

        loginResponse.EnsureSuccessStatusCode();

        var loginResult =
            await loginResponse.Content
                .ReadFromJsonAsync<LoginResponseDto>();

        return loginResult!.Token;
    }

    private sealed class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
    }
}