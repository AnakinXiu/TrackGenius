using System.Collections.Generic;

namespace TrackGenius.Model
{
    public class BestLapRanker : IRaceRanker
    {
        public IList<ICar> RankCars(IEnumerable<ICar> raceCars)
        {
            return new List<ICar>();
        }
    }
}