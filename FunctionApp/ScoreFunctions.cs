using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace FunctionApp
{
    public static class ScoreFunctions
    {
        [FunctionName("GetScores")]
        public static async Task<IActionResult> GetScores(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "score/{playerId}")]
            HttpRequest req,
            string playerId,
            ILogger log)
        {
            log.LogInformation($"Get player scores for {playerId}");

            var connStr = Environment.GetEnvironmentVariable("SQLConnectionString");

            return await GetScoresFromDb(playerId, connStr);
        }

        [FunctionName("PostScores")]
        public static async Task<IActionResult> PostScore(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "score/{playerId}")]
            HttpRequest req,
            string playerId,
            ILogger log)
        {
            log.LogInformation($"Posting player scores for {playerId}");

            ScoreEntry request = null;
            try
            {
                var content = await new StreamReader(req.Body).ReadToEndAsync();
                request = JsonConvert.DeserializeObject<ScoreEntry>(content);
            }
            catch (Exception exception)
            {
                log.LogInformation($"Deserialize failed for {playerId} - {exception.Message}");
            }

            var connStr = Environment.GetEnvironmentVariable("SQLConnectionString");

            if (request != null && IsValid(request) && playerId == request.PlayerId)
            {
                log.LogInformation($"Post is valid for {playerId}");
                try
                {
                    var rows = await PostScoresToDb(connStr, request);
                    log.LogInformation(rows != 1
                        ? $"SQL write failed for {playerId}"
                        : $"SQL write succeed for {playerId}");
                }
                catch (DbException exception)
                {
                    log.LogInformation($"SQL write failed for {playerId} - {exception.Message}");
                }
            }

            return new OkResult();
        }

        private static async Task<IActionResult> GetScoresFromDb(string playerId, string connStr)
        {
            var playerScores = new List<ScoreEntry>();
            var highScores = new List<ScoreEntry>();

            await using (var connection = new SqlConnection(connStr))
            {
                connection.Open();
                await GetPlayerScores(playerId, connection, playerScores, 1);
                await GetHighScores(connection, highScores, 1);
            }

            dynamic data = JsonConvert.SerializeObject(new
            {
                PlayerScores = playerScores,
                HighScores = highScores
            });
            return new OkObjectResult(data);
        }

        private static bool IsValid(ScoreEntry toTest)
        {
            var computed = toTest.Score + toTest.PlayerId + toTest.Seed + DateTime.UtcNow.Date +
                           Environment.GetEnvironmentVariable("HashSalt");
            var bytes = Encoding.UTF8.GetBytes(CreateMD5(computed));
            var encoded = Convert.ToBase64String(bytes);
            return encoded == toTest.ScoreHash;
        }

        private static async Task GetPlayerScores(string playerId, SqlConnection connection, List<ScoreEntry> scores,
            int mode)
        {
            var text =
$@"SELECT Top 10 *
FROM (SELECT
        DENSE_RANK() OVER (ORDER BY Score DESC) [Rank], *
    FROM [dbo].[Scores]
	Where Mode = '{mode}'
) ResultSet
Where PlayerId = @playerId
and Mode = '{mode}'
ORDER BY SCORE DESC";
            await using var cmd = new SqlCommand(text, connection);
            cmd.Parameters.AddWithValue("@playerId", playerId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (reader.Read())
            {
                scores.Add(new ScoreEntry
                {
                    Rank = (long) reader.GetValue(0),
                    PlayerId = (string) reader.GetValue(2),
                    PlayerName = (string) reader.GetValue(3),
                    Score = (long) reader.GetValue(4),
                    Mode = (int) reader.GetValue(5),
                    Seed = (string) reader.GetValue(6),
                    CreatedDate = (DateTime) reader.GetValue(7)
                });
            }
        }

        private static async Task GetHighScores(SqlConnection connection, List<ScoreEntry> scores, int mode)
        {
            var text = $"SELECT TOP (10) * FROM [dbo].[Scores]\r\nWhere Mode='{mode}'\r\norder by Score desc";
            await using var cmd = new SqlCommand(text, connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            var rank = 1;
            while (reader.Read())
            {
                scores.Add(new ScoreEntry
                {
                    Rank = rank,
                    PlayerId = (string) reader.GetValue(1),
                    PlayerName = (string) reader.GetValue(2),
                    Score = (long) reader.GetValue(3),
                    Mode = (int) reader.GetValue(4),
                    Seed = (string) reader.GetValue(5),
                    CreatedDate = (DateTime) reader.GetValue(6)
                });
                rank++;
            }
        }

        private static async Task<int> PostScoresToDb(string connStr, ScoreEntry score)
        {
            await using var connection = new SqlConnection(connStr);
            connection.Open();
            const string text = "INSERT INTO [dbo].[Scores] (PlayerId, PlayerName, Score, Mode, Seed, CreatedDate)\r\n" +
                                "VALUES(@playerId, @playerName, @score, @mode, @seed, @date); ";
            await using var cmd = new SqlCommand(text, connection);
            cmd.Parameters.AddWithValue("playerId", score.PlayerId);
            cmd.Parameters.AddWithValue("playerName", score.PlayerName);
            cmd.Parameters.AddWithValue("score", score.Score);
            cmd.Parameters.AddWithValue("mode", score.Mode);
            cmd.Parameters.AddWithValue("seed", score.Seed);
            cmd.Parameters.AddWithValue("date", DateTime.UtcNow.Date);
            return await cmd.ExecuteNonQueryAsync();
        }

        // ReSharper disable once InconsistentNaming
        private static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = Encoding.ASCII.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);

            // Convert the byte array to hexadecimal string
            var sb = new StringBuilder();
            foreach (var t in hashBytes)
            {
                sb.Append(t.ToString("X2"));
            }

            return sb.ToString();
        }
    }

    public class ScoreEntry
    {
        public long Rank { get; set; }
        public string PlayerName { get; set; }
        public string PlayerId { get; set; }
        public long Score { get; set; }
        public string ScoreHash { get; set; }
        public int Mode { get; set; }
        public string Seed { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
