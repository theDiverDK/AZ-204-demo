using Microsoft.Azure.Cosmos;
using ConferenceHub.Models;
using System.Text.Json;

public class DataMigration
{
    public static async Task MigrateSessionsAsync(string cosmosConnectionString, string databaseName)
    {
        var cosmosClient = new CosmosClient(cosmosConnectionString);
        var container = cosmosClient.GetContainer(databaseName, "Sessions");

        // Read existing sessions from JSON
        var jsonPath = "Data/sessions.json";
        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        var oldSessions = JsonSerializer.Deserialize<List<OldSession>>(jsonContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (oldSessions == null) return;

        // Migrate each session
        foreach (var oldSession in oldSessions)
        {
            var newSession = new Session
            {
                Id = Guid.NewGuid().ToString(),
                ConferenceId = "az204-2025",
                SessionNumber = oldSession.Id,
                Title = oldSession.Title,
                Speaker = oldSession.Speaker,
                StartTime = oldSession.StartTime,
                EndTime = oldSession.EndTime,
                Room = oldSession.Room,
                Description = oldSession.Description,
                Capacity = oldSession.Capacity,
                CurrentRegistrations = oldSession.CurrentRegistrations,
                SlideUrl = oldSession.SlideUrl,
                Track = DetermineTrack(oldSession.Title),
                Level = "Intermediate",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await container.CreateItemAsync(newSession, new PartitionKey(newSession.ConferenceId));
            Console.WriteLine($"Migrated session: {newSession.Title}");
        }

        Console.WriteLine($"Successfully migrated {oldSessions.Count} sessions");
    }

    private static string DetermineTrack(string title)
    {
        if (title.Contains("Cloud") || title.Contains("Azure")) return "Cloud";
        if (title.Contains("Function") || title.Contains("Serverless")) return "Serverless";
        if (title.Contains("Container") || title.Contains("Docker")) return "DevOps";
        if (title.Contains("Security")) return "Security";
        if (title.Contains("Storage") || title.Contains("Database")) return "Data";
        return "General";
    }

    // Old session model for migration
    private class OldSession
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Speaker { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Room { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public int CurrentRegistrations { get; set; }
        public string? SlideUrl { get; set; }
    }
}