using System;
using System.Security.Cryptography;
using System.Text;

namespace FunctionApp.Models
{
    public class ScoreRequest
    {
        private const string HashSaltName = "HashSalt";
        
        /// <summary>
        /// The display name for the owner of this score
        /// </summary>
        public string PlayerName { get; set; }
        /// <summary>
        /// Id for the owner of this score
        /// </summary>
        public string PlayerId { get; set; }
        /// <summary>
        /// Score number
        /// </summary>
        public long Score { get; set; }
        /// <summary>
        /// A hashed version of the data in the entry, ready to be validated against
        /// </summary>
        public string ScoreHash { get; set; }
        /// <summary>
        /// The game mode this score was achieved in
        /// </summary>
        public GameMode Mode { get; set; }
        /// <summary>
        /// The world seed the score was achieved in
        /// </summary>
        public string Seed { get; set; }

        /// <summary>
        /// Validate the ScoreHash to determine if the entry is legitimate
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            var computed =
                $"{Score}{PlayerId}{Seed}{DateTime.UtcNow.Date.Ticks}{Environment.GetEnvironmentVariable(HashSaltName)}";
            var bytes = Encoding.UTF8.GetBytes(CreateHash(computed));
            var encoded = Convert.ToBase64String(bytes);
            return encoded == ScoreHash;
        }

        /// <summary>
        /// The standard implementation of a SHA256 Hash
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        private static string CreateHash(string input)
        {
            using var sha256Hash = SHA256.Create();
            // ComputeHash - returns byte array  
            var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Convert byte array to a string   
            var sb = new StringBuilder();
            foreach (var t in bytes)
            {
                sb.Append(t.ToString("x2"));
            }

            return sb.ToString();
        }
    }


}
