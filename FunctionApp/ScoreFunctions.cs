using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using FunctionApp.Models;

namespace FunctionApp
{
    public static class ScoreFunctions
    {
        private const string ConnectionStringParameter = "SQLConnectionString";

        /// <summary>
        /// Function endpoint to get current scores for a player across all game modes
        /// </summary>
        /// <param name="req">Http Request</param>
        /// <param name="playerId">Player Id from query string</param>
        /// <param name="log">logger injection</param>
        /// <returns></returns>
        [FunctionName("GetScores")]
        public static async Task<IActionResult> GetScores(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "score/{playerId}")]
            HttpRequest req,
            string playerId,
            ILogger log)
        {
            log.LogInformation($"Get player scores for {playerId}");

            var connStr = Environment.GetEnvironmentVariable(ConnectionStringParameter);

            return await ScoreRepository.GetScoresFromDb(playerId, connStr);
        }

        /// <summary>
        /// Function endpoint to post a score for a game mode. Returns nothing as success and failure are silent.
        /// The main concerns here are users trying to spoof high scores.
        /// </summary>
        /// <param name="req">Http request</param>
        /// <param name="playerId">Player Id from query string</param>
        /// <param name="log">logger injection</param>
        /// <returns></returns>
        [FunctionName("PostScores")]
        public static async Task<IActionResult> PostScore(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "score/{playerId}")]
            HttpRequest req,
            string playerId,
            ILogger log)
        {
            log.LogInformation($"Posting player scores for {playerId}");

            ScoreRequest request = null;
            try
            {
                var content = await new StreamReader(req.Body).ReadToEndAsync();
                request = JsonConvert.DeserializeObject<ScoreRequest>(content);
            }
            catch (Exception exception)
            {
                log.LogInformation($"Deserialize failed for {playerId} - {exception.Message}");
            }

            var connStr = Environment.GetEnvironmentVariable(ConnectionStringParameter);

            // Only post valid scores
            if (request != null && request.IsValid() && playerId == request.PlayerId)
            {
                log.LogInformation($"Post is valid for {playerId}");
                try
                {
                    var rows = await ScoreRepository.PostScoresToDb(connStr, request);
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
        
    }
    
    
}
