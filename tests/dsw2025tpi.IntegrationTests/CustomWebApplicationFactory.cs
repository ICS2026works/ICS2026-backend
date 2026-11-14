using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Dsw2025Tpi.Data;

namespace dsw2025tpi.IntegrationTests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private SqliteConnection _connection = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // 1. REMOVER LAS CONFIGURACIONES DE PRODUCCIÓN (AMBOS CONTEXTOS)

            var appDbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<Dsw2025TpiContext>));
            if (appDbContextDescriptor != null) services.Remove(appDbContextDescriptor);

            var authDbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuthenticateContext>));
            if (authDbContextDescriptor != null) services.Remove(authDbContextDescriptor);

            // 2. CREAR LA CONEXIÓN SQLITE EN MEMORIA

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open(); // Se mantiene abierta durante toda la sesión

            // 3. REGISTRAR AMBOS CONTEXTOS CON SQLITE

            services.AddDbContext<Dsw2025TpiContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            services.AddDbContext<AuthenticateContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // 4. CREAR LAS TABLAS EN LA BASE DE DATOS EN MEMORIA

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();

            var appDb = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
            appDb.Database.EnsureCreated();

            var authDb = scope.ServiceProvider.GetRequiredService<AuthenticateContext>();
            authDb.Database.EnsureCreated();
     
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
