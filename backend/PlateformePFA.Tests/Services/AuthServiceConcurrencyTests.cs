using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Auth;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using FluentAssertions;
using Xunit;

namespace PlateformePFA.Tests.Services
{
    public class FakeAuditService : IAuditService
    {
        public Task LogAsync(
            string action,
            string entityType = "",
            int? entityId = null,
            string? message = null,
            int? userId = null,
            string? userName = null)
        {
            return Task.CompletedTask;
        }
    }

    public class AuthServiceConcurrencyTests : IDisposable
    {
        private readonly string _dbName;
        private readonly string _connectionString;
        private readonly SqliteConnection _keepAliveConnection;

        public AuthServiceConcurrencyTests()
        {
            // Use a unique database name per test run to prevent test cross-contamination
            _dbName = $"concurrency-test-{Guid.NewGuid()}";
            _connectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared";

            // We must keep one connection open so SQLite doesn't destroy the in-memory database
            _keepAliveConnection = new SqliteConnection(_connectionString);
            _keepAliveConnection.Open();

            // Initialize schema
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_keepAliveConnection)
                .Options;

            using var context = new AppDbContext(options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _keepAliveConnection.Dispose();
        }

        private AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connectionString)
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task RefreshAsync_with_concurrency_rotates_only_once()
        {
            // Arrange
            using (var context = CreateDbContext())
            {
                var user = new Utilisateur
                {
                    Email = "test-concurrency@eniad.dz",
                    Nom = "Test",
                    Prenom = "User",
                    MotDePasseHash = "hash",
                    Role = "Admin",
                    EstActif = true
                };
                context.Utilisateurs.Add(user);
                await context.SaveChangesAsync();

                var token = new RefreshToken
                {
                    UtilisateurId = user.Id,
                    Token = "initial-refresh-token",
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                    CreeLe = DateTime.UtcNow
                };
                context.RefreshTokens.Add(token);
                await context.SaveChangesAsync();
            }

            // Create configuration using ConfigurationBuilder
            var myConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT_SECRET"] = "Test-JWT-Secret-With-At-Least-32-Chars-And-Digits-1234567890!",
                    ["JWT_ISSUER"] = "test",
                    ["JWT_AUDIENCE"] = "test",
                })
                .Build();

            var fakeAudit = new FakeAuditService();

            // Setup two distinct DbContexts using distinct connections to the shared SQLite DB
            using var context1 = CreateDbContext();
            using var context2 = CreateDbContext();

            var authService1 = new AuthService(
                context1,
                myConfiguration,
                NullLogger<AuthService>.Instance,
                fakeAudit
            );

            var authService2 = new AuthService(
                context2,
                myConfiguration,
                NullLogger<AuthService>.Instance,
                fakeAudit
            );

            // Act: Start two refresh operations concurrently
            var task1 = authService1.RefreshAsync("initial-refresh-token");
            var task2 = authService2.RefreshAsync("initial-refresh-token");

            AuthResponseDto? result1 = null;
            AuthResponseDto? result2 = null;
            Exception? ex1 = null;
            Exception? ex2 = null;

            try
            {
                result1 = await task1;
            }
            catch (Exception ex)
            {
                ex1 = ex;
            }

            try
            {
                result2 = await task2;
            }
            catch (Exception ex)
            {
                ex2 = ex;
            }

            // Assert: Exactly one operation should succeed, the other must fail (returns null or throws)
            var successCount = 0;
            var failureCount = 0;

            if (result1 != null && ex1 == null) successCount++;
            else failureCount++;

            if (result2 != null && ex2 == null) successCount++;
            else failureCount++;

            successCount.Should().Be(1, "Exactly one refresh attempt must succeed");
            failureCount.Should().Be(1, "Exactly one refresh attempt must fail");

            // Verify in the database that the token is indeed revoked
            using var verifyContext = CreateDbContext();
            var tokenInDb = await verifyContext.RefreshTokens.SingleAsync(rt => rt.Token == "initial-refresh-token");
            tokenInDb.RevokedAt.Should().NotBeNull("The rotated token must be marked as revoked");
        }
    }
}
