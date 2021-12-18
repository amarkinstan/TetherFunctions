using System;

namespace FunctionApp.Models
{
    public class ScoreEntry
    {
        /// <summary>
        /// The ranking of this score
        /// </summary>
        public long Rank { get; set; }
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
        /// The game mode this score was achieved in
        /// </summary>
        public GameMode Mode { get; set; }
        /// <summary>
        /// The world seed the score was achieved in
        /// </summary>
        public string Seed { get; set; }
        /// <summary>
        /// The UTC date the score was achieved in
        /// </summary>
        public DateTime CreatedDate { get; set; }

       
    }


}
