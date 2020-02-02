using System.Collections.Generic;

namespace TrackGenius.Model
{
    public interface IRaceRanker
    {
        IList<ICar> RankCars(IEnumerable<ICar> raceCars);
    }
}