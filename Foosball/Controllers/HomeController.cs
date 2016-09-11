using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace Foosball.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            List<UserRank> userRanks = GetUserRanks();
            return View(userRanks);
        }

        public ActionResult User(string q)
        {
            int rank;
            int bestWinStreak;
            int worstLosingtreak;
            UserProfile user = new UserProfile
            {
                Name = q,
                UserRank = GetUserRank(q, out rank),
                BestMatch = GetBestWonMatch(q, out bestWinStreak),
                WorstMatch = GetWorstLostMatch(q, out worstLosingtreak),
                Rank = rank,
                BestWinStreak = bestWinStreak,
                WortsLosingStreak = worstLosingtreak
            };
            return View(user);
        }

        private List<UserRank> GetUserRanks()
        {
            List<Match> allMatches = GetAllMatches();
            List<UserRank> userRanks = GetRankings(allMatches);
            return userRanks;
        }

        private List<Match> GetAllMatches()
        {
            return TransformStringToObject(GetDataInJsonString());
        }

        private List<Match> GetAllMatches(string username)
        {
            return
                GetAllMatches()
                    .Where(x => x.Team1.Players.Contains(username) || x.Team2.Players.Contains(username))
                    .ToList();
        }

        private List<Match> GetAllWonMatches(string username, out Match bestWin, out int bestWinStreak)
        {
            List<Match> matches = GetAllMatches(username);
            List<Match> wonMatches = new List<Match>();
            int maxGoalDifference = 0;
            bestWinStreak = 0;
            int currentWinStreak = 0;
            bestWin = null;
            foreach (Match match in matches)
            {
                int factor = 1;
                if (match.Team2.Players.Contains(username))
                    factor = factor * -1;

                List<string> score = match.Score.Split('-').ToList();
                int goalDifference = int.Parse(score[0]) - int.Parse(score[1]);
                goalDifference = goalDifference * factor;

                if (goalDifference > -1)
                {
                    currentWinStreak++;

                    if (currentWinStreak > bestWinStreak)
                        bestWinStreak = currentWinStreak;

                    wonMatches.Add(match);
                    if (goalDifference > maxGoalDifference)
                    {
                        maxGoalDifference = goalDifference;
                        bestWin = match;
                    }
                }
                else
                {
                    currentWinStreak = 0;
                }
            }
            return wonMatches;
        }
        private List<Match> GetAllDefeatedMatches(string username, out Match worstDefeat, out int worstLosingStreak)
        {
            List<Match> matches = GetAllMatches(username);
            List<Match> defeatedMatches = new List<Match>();
            int maxGoalDifference = 0;
            worstLosingStreak = 0;
            int currentlosingStreak = 0;
            worstDefeat = null;
            foreach (Match match in matches)
            {
                int factor = 1;
                if (match.Team1.Players.Contains(username))
                    factor = factor * -1;

                List<string> score = match.Score.Split('-').ToList();
                int goalDifference = int.Parse(score[0]) - int.Parse(score[1]);
                goalDifference = goalDifference * factor;

                if (goalDifference > -1)
                {
                    currentlosingStreak++;

                    if (currentlosingStreak > worstLosingStreak)
                        worstLosingStreak = currentlosingStreak;

                    defeatedMatches.Add(match);
                    if (goalDifference > maxGoalDifference)
                    {
                        maxGoalDifference = goalDifference;
                        worstDefeat = match;
                    }
                }
                else
                {
                    currentlosingStreak = 0;
                }
            }
            return defeatedMatches;
        }

        private UserRank GetUserRank(string username, out int rank)
        {
            List<UserRank> userRanks = GetUserRanks();
            UserRank user = userRanks.FirstOrDefault(x => x.Username.ToLower().Equals(username.ToLower()));
            rank = userRanks.IndexOf(user) + 1;
            return user;
        }

        private string GetDataInJsonString()
        {
            string text;
            string filePath = ConfigurationManager.AppSettings["filepath"];
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
            {
                text = streamReader.ReadToEnd();
            }
            return text;
        }

        private List<Match> TransformStringToObject(string text)
        {
            List<Match> matches = JsonConvert.DeserializeObject<List<Match>>(text);
            return matches;
        }

        private List<UserRank> GetRankings(List<Match> allMatches)
        {
            Hashtable hashtable = new Hashtable();
            foreach (Match match in allMatches)
            {
                List<string> matchPlayers = match.Team1.Players.Concat(match.Team2.Players).ToList();
                foreach (string player in matchPlayers)
                {
                    if (!hashtable.ContainsKey(player))
                        hashtable.Add(player, new UserRank());

                    UserRank userRank = (UserRank)hashtable[player];
                    userRank.Played = userRank.Played + 1;
                    hashtable[player] = userRank;
                }

                List<string> score = match.Score.Split('-').ToList();

                bool hasTeam1Won = int.Parse(score[0]) > int.Parse(score[1]);
                int team1GoalDifference = int.Parse(score[0]) - int.Parse(score[1]);

                bool hasTeam2Won = int.Parse(score[1]) > int.Parse(score[0]);
                int team2GoalDifference = int.Parse(score[1]) - int.Parse(score[0]);

                UpdateHashWithResults(hashtable, match.Team1.Players, hasTeam1Won, team1GoalDifference);
                UpdateHashWithResults(hashtable, match.Team2.Players, hasTeam2Won, team2GoalDifference);
            }

            List<UserRank> userRanks = new List<UserRank>();
            foreach (string key in hashtable.Keys)
            {
                UserRank temp = (UserRank)hashtable[key];
                temp.Username = key;
                userRanks.Add(temp);
            }
            userRanks = userRanks
                        .OrderByDescending(x => x.QualityScore)
                //.OrderBy(x => x.GoalDifference)
                        .ToList();
            return userRanks;
        }

        private Hashtable UpdateHashWithResults(Hashtable hashtable, List<string> players, bool won, int goalDifference)
        {
            foreach (string player in players)
            {
                UserRank userRank = (UserRank)hashtable[player];
                if (userRank.Trend == null)
                    userRank.Trend = new List<string>();

                if (won)
                {
                    userRank.Won = userRank.Won + 1;
                    userRank.Trend.Add("W");
                }
                else
                {
                    userRank.Lost = userRank.Lost + 1;
                    userRank.Trend.Add("L");
                }
                userRank.GoalDifference = userRank.GoalDifference + goalDifference;
                hashtable[player] = userRank;
            }
            return hashtable;
        }

        private Match GetBestWonMatch(string username, out int winStreak)
        {
            Match best;
            GetAllWonMatches(username, out best, out winStreak);
            return best;
        }

        private Match GetWorstLostMatch(string username, out int losingStreak)
        {
            Match worst;
            GetAllDefeatedMatches(username, out worst, out losingStreak);
            return worst;
        }
    }

    public class Match
    {
        public Team Team1 { get; set; }
        public Team Team2 { get; set; }
        public string Score { get; set; }
    }

    public class Team
    {
        public List<string> Players { get; set; }
    }

    public class UserRank
    {
        public string Username { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public List<string> Trend { get; set; }
        public int GoalDifference { get; set; }

        public decimal QualityScore
        {
            get
            {
                var scrore = ((decimal)Won / Played) + ((decimal)GoalDifference / 1000);
                return scrore;
            }
        }
    }

    public class UserProfile
    {
        public string Name { get; set; }
        public UserRank UserRank { get; set; }
        public Match BestMatch { get; set; }
        public Match WorstMatch { get; set; }
        public int Rank { get; set; }
        public int BestWinStreak { get; set; }
        public int WortsLosingStreak { get; set; }
    }
}
