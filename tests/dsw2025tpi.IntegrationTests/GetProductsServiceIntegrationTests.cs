using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Api;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dsw2025tpi.IntegrationTests;

[Collection("IntegrationTests")]
public class GetProductsServiceIntegrationTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public GetProductsServiceIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_WithoutCredentials_ReturnsOkOrNoContentWithPaginationContract()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

        var uniqueTag = Guid.NewGuid().ToString()[..8];
        var product = new Product(
            $"SKU-PUB-{uniqueTag}",
            $"Producto Publico {uniqueTag}",
            150m,
            10,
            "Descripcion de prueba publica",
            $"INT-PUB-{uniqueTag}"
        );

        dbContext.Set<Product>().Add(product);
        await dbContext.SaveChangesAsync();

        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rawJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var hasProductItems = root.TryGetProperty("productItems", out var itemsElement) ||
                                  root.TryGetProperty("ProductItems", out itemsElement);
            hasProductItems.Should().BeTrue("el contrato de paginacion debe incluir productItems");
            itemsElement.ValueKind.Should().Be(JsonValueKind.Array);

            var hasTotal = root.TryGetProperty("totalItems", out _) ||
                           root.TryGetProperty("total", out _) ||
                           root.TryGetProperty("Total", out _);
            hasTotal.Should().BeTrue("el contrato de paginacion debe incluir el total de elementos");

            var pagination = JsonSerializer.Deserialize<ProductModel.ResponsePagination>(
                rawJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            pagination.Should().NotBeNull();
            pagination!.ProductItems.Should().NotBeNull();
            pagination.ProductItems.Should().Contain(p => p.Sku == product.Sku);
        }
    }

    [Fact]
    public async Task GetProducts_WithSearchAndPaginationQueryStrings_FiltersFromSqlite()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

        var uniqueToken = $"Filtro{Guid.NewGuid().ToString()[..6]}";
        var matchingA = new Product($"SKU-A-{uniqueToken}", $"{uniqueToken} Alpha", 200m, 5, "Match A", $"INT-A-{uniqueToken}");
        var matchingB = new Product($"SKU-B-{uniqueToken}", $"{uniqueToken} Beta", 300m, 8, "Match B", $"INT-B-{uniqueToken}");
        var nonMatching = new Product($"SKU-OTHER-{Guid.NewGuid().ToString()[..6]}", "Otro Producto Sin Relacion", 500m, 3, "Other", $"INT-O-{Guid.NewGuid().ToString()[..6]}");

        dbContext.Set<Product>().AddRange(matchingA, matchingB, nonMatching);
        await dbContext.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/products?search={Uri.EscapeDataString(uniqueToken)}&pageNumber=1&pageSize=10");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Respuesta inesperada: {body}");

        var result = JsonSerializer.Deserialize<ProductModel.ResponsePagination>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        result.Should().NotBeNull();
        result!.ProductItems.Should().HaveCount(2);
        result.ProductItems.Should().OnlyContain(p => p.Name != null && p.Name.Contains(uniqueToken));
        result.ProductItems.Should().NotContain(p => p.Sku == nonMatching.Sku);
    }

    [Fact]
    public async Task GetProductsAdmin_WithAdminRole_IncludesInactiveProductsFromSqlite()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

        var uniqueTag = Guid.NewGuid().ToString()[..8];
        var inactiveProduct = new Product(
            $"SKU-INACT-{uniqueTag}",
            $"Producto Inactivo {uniqueTag}",
            450m,
            4,
            "Producto desactivado para prueba admin",
            $"INT-INACT-{uniqueTag}"
        )
        {
            IsActive = false
        };

        dbContext.Set<Product>().Add(inactiveProduct);
        await dbContext.SaveChangesAsync();

        var adminUsername = $"admin_{uniqueTag}";
        var adminEmail = $"{adminUsername}@test.com";
        var token = await AuthenticateAdminAsync(adminEmail, adminUsername);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/products/admin");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Respuesta inesperada: {body}");

        var result = JsonSerializer.Deserialize<ProductModel.ResponsePagination>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        result.Should().NotBeNull();
        result!.ProductItems.Should().Contain(p => p.Sku == inactiveProduct.Sku && !p.IsActive);
    }

    private async Task<string> AuthenticateAdminAsync(string email, string username)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        const string roleName = "Admin";
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        var user = new IdentityUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = true
        };

        var created = await userManager.CreateAsync(user, "Password123!");
        created.Succeeded.Should().BeTrue();

        var roleAdded = await userManager.AddToRoleAsync(user, roleName);
        roleAdded.Succeeded.Should().BeTrue();

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