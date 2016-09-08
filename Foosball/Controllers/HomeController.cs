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
            List<Match> allMatches = TransformStringToObject(GetDataInJsonString());
            List<UserRank> userRanks = GetRankings(allMatches);
            return View(userRanks);
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
                UpdateHashWithResults(hashtable, match.Team1.Players, int.Parse(score[0]) > int.Parse(score[1]));
                UpdateHashWithResults(hashtable, match.Team2.Players, int.Parse(score[0]) < int.Parse(score[1]));
            }

            List<UserRank> userRanks = new List<UserRank>();
            foreach (string key in hashtable.Keys)
            {
                UserRank temp = (UserRank) hashtable[key];
                temp.Username = key;                
                userRanks.Add(temp);
            }
            userRanks = userRanks
                        .OrderBy(x => x.Lost)
                        .OrderByDescending(x => x.QualityScore).ToList();
            return userRanks;
        }

        private Hashtable UpdateHashWithResults(Hashtable hashtable, List<string> players, bool won)
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
                hashtable[player] = userRank;
            }
            return hashtable;
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

        public decimal QualityScore
        {
            get
            {
                if (Won == 0)
                    return 0;
                return (decimal)Played/Won;
            }
        }
    }
}
