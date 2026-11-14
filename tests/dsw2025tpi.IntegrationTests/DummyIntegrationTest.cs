using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Xunit;

namespace dsw2025tpi.IntegrationTests;

[Collection("IntegrationTests")]
public class DummyIntegrationTest
{
    private readonly HttpClient _client;

    public DummyIntegrationTest(CustomWebApplicationFactory<Dsw2025Tpi.Api.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_SwaggerOrRoot_ReturnsSucessOrNotFound()
    {
        //Peticion basica para ver si el servidor se levanta
        var response = await _client.GetAsync("/api/health");

        Assert.NotNull(response);//Verifica que la API responda
    }
}