using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using FunctionApp.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace FunctionApp
{
    public class ScoreRepository
    {
        /// <summary>
        /// Get an object containing the scores for each mode in the game from backing database
        /// </summary>
        /// <param name="playerId">The player id to gets scores for</param>
        /// <param name="connStr">Connection string</param>
        /// <returns>A list of scores for all game modes</returns>
        public static async Task<IActionResult> GetScoresFromDb(string playerId, string connStr)
        {
            // Modes we want to get scores for
            var scores = new List<ModeScoreCollection>
            {
                new ModeScoreCollection { Mode = GameMode.Free }, 
                new ModeScoreCollection { Mode = GameMode.Timed },
                new ModeScoreCollection { Mode = GameMode.Pursuit },
                new ModeScoreCollection { Mode = GameMode.DailyRace }
            };

            await using (var connection = new SqlConnection(connStr))
            {
                connection.Open();
                foreach (var collection in scores)
                {
                    // Get the list of player scores
                    await GetPlayerScores(playerId, connection, collection, collection.Mode);
                    // And the list of top scores in that mode
                    await GetHighScores(connection, collection, collection.Mode);
                }

            }

            dynamic data = JsonConvert.SerializeObject(scores);
            return new OkObjectResult(data);
        }

        /// <summary>
        /// Post a score to the backing database
        /// </summary>
        /// <param name="connStr">Connections string</param>
        /// <param name="score">Score request</param>
        /// <returns>Number of rows modified</returns>
        public static async Task<int> PostScoresToDb(string connStr, ScoreRequest score)
        {
            await using var connection = new SqlConnection(connStr);
            connection.Open();

            const string query =
                "INSERT INTO [dbo].[Scores] (PlayerId, PlayerName, Score, Mode, Seed, CreatedDate)\r\n" +
                "VALUES(@playerId, @playerName, @score, @mode, @seed, @date); ";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("playerId", score.PlayerId);
            cmd.Parameters.AddWithValue("playerName", score.PlayerName);
            cmd.Parameters.AddWithValue("score", score.Score);
            cmd.Parameters.AddWithValue("mode", (int) score.Mode);
            cmd.Parameters.AddWithValue("seed", score.Seed);
            cmd.Parameters.AddWithValue("date", DateTime.UtcNow.Date);

            return await cmd.ExecuteNonQueryAsync();
        }


        private static async Task GetPlayerScores(
            string playerId, 
            SqlConnection connection, 
            ModeScoreCollection scores,
            GameMode mode)
        {
            // Use DENSE_RANK to ge the position of each score in the list
            var query =
$@"SELECT Top 10 *
FROM (SELECT
        DENSE_RANK() OVER (ORDER BY Score DESC) [Rank], *
    FROM [dbo].[Scores]
	Where Mode = '{(int) mode}'
) ResultSet
Where PlayerId = @playerId
and Mode = '{(int) mode}'
ORDER BY SCORE DESC";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@playerId", playerId);

            await using var reader = await cmd.ExecuteReaderAsync();

            scores.PlayerScores = new List<ScoreEntry>();

            while (reader.Read())
            {
                scores.PlayerScores.Add(new ScoreEntry
                {
                    Rank = (long) reader.GetValue(0),
                    PlayerId = (string) reader.GetValue(2),
                    PlayerName = (string) reader.GetValue(3),
                    Score = (long) reader.GetValue(4),
                    Mode = (GameMode) (int) reader.GetValue(5),
                    Seed = (string) reader.GetValue(6),
                    CreatedDate = (DateTime) reader.GetValue(7)
                });
            }
        }

        private static async Task GetHighScores(SqlConnection connection, ModeScoreCollection scores, GameMode mode)
        {
            var query = $"SELECT TOP (10) * FROM [dbo].[Scores]\r\nWhere Mode='{(int) mode}'\r\norder by Score desc";
            await using var cmd = new SqlCommand(query, connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            // rank must always start at 1
            var rank = 1;

            scores.HighScores = new List<ScoreEntry>();

            while (reader.Read())
            {
                scores.HighScores.Add(new ScoreEntry
                {
                    Rank = rank,
                    PlayerId = (string) reader.GetValue(1),
                    PlayerName = (string) reader.GetValue(2),
                    Score = (long) reader.GetValue(3),
                    Mode = (GameMode) (int) reader.GetValue(4),
                    Seed = (string) reader.GetValue(5),
                    CreatedDate = (DateTime) reader.GetValue(6)
                });
                rank++;
            }
        }
        
    }
}
