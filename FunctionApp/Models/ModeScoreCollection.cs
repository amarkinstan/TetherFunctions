using System.Collections.Generic;

namespace FunctionApp.Models
{
    public class ModeScoreCollection
    {
        public GameMode Mode { get; set; }
        public List<ScoreEntry> PlayerScores { get; set; }
        public List<ScoreEntry> HighScores { get; set; }
    }
}
