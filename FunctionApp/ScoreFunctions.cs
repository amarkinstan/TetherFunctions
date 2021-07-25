using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Data.SqlClient;

namespace FunctionApp
{
    public static class ScoreFunctions
    {
        [FunctionName("GetScores")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "score/{playerId}")] HttpRequest req,
            string playerId,
            ILogger log)
        {
            log.LogInformation($"Get player scores for {playerId}");

            var connStr = Environment.GetEnvironmentVariable("SQLConnectionString");

            var playerScores = new List<ScoreEntry>();
            var highScores = new List<ScoreEntry>();

            await using (var connection = new SqlConnection(connStr))
            {
                connection.Open();
                await GetPlayerScores(playerId, connection, playerScores, 1);
                await GetHighScores(connection, highScores, 1);
            }

            dynamic data = JsonConvert.SerializeObject(new {
                PlayerScores = playerScores,
                Highscores = highScores
            });
            return new OkObjectResult(data);
        }

        private static async Task GetPlayerScores(string playerId, SqlConnection connection, List<ScoreEntry> scores, int mode)
        {
            var text = $"SELECT TOP (10) * FROM [dbo].[Scores]\r\nWhere PlayerId = @playerId and  Mode='{mode}'\r\norder by Score desc";
            await using var cmd = new SqlCommand(text, connection);
            cmd.Parameters.Add("@playerId", SqlDbType.NVarChar).Value = playerId;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (reader.Read())
            {
                scores.Add(new ScoreEntry
                {
                    PlayerId = (string) reader.GetValue(1),
                    PlayerName = (string) reader.GetValue(2),
                    Score = (long) reader.GetValue(3),
                    Mode = (int) reader.GetValue(4),
                    Seed = (string) reader.GetValue(5),
                    CreatedDate = (DateTime) reader.GetValue(6)
                });
            }
        }

        private static async Task GetHighScores(SqlConnection connection, List<ScoreEntry> scores, int mode)
        {
            var text = $"SELECT TOP (10) * FROM [dbo].[Scores]\r\nWhere Mode='{mode}'\r\norder by Score desc";
            await using var cmd = new SqlCommand(text, connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (reader.Read())
            {
                scores.Add(new ScoreEntry
                {
                    PlayerId = (string)reader.GetValue(1),
                    PlayerName = (string)reader.GetValue(2),
                    Score = (long)reader.GetValue(3),
                    Mode = (int)reader.GetValue(4),
                    Seed = (string)reader.GetValue(5),
                    CreatedDate = (DateTime)reader.GetValue(6)
                });
            }
        }

    }

    public class ScoreEntry
    {
        public string PlayerName { get; set; }
        public string PlayerId { get; set; }
        public long Score { get; set; }
        public int Mode { get; set; }
        public string Seed { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
