namespace LineSight.Core.Models;

/// <summary>
/// The ordered set of stations a module must pass through. Wraps the station list so the
/// routing rules can ask "what comes first / next?" without caring how stations are stored.
/// </summary>
public sealed class ProductionLine
{
    private readonly List<Station> _stations;

    public ProductionLine(IEnumerable<Station> stations)
    {
        ArgumentNullException.ThrowIfNull(stations);
        _stations = stations.OrderBy(s => s.Sequence).ToList();

        if (_stations.Count == 0)
            throw new ArgumentException("A production line needs at least one station.", nameof(stations));
        if (_stations.Select(s => s.Sequence).Distinct().Count() != _stations.Count)
            throw new ArgumentException("Station sequence numbers must be unique.", nameof(stations));
    }

    /// <summary>Stations ordered by <see cref="Station.Sequence"/>.</summary>
    public IReadOnlyList<Station> Stations => _stations;

    public Station First => _stations[0];

    public Station Last => _stations[^1];

    public Station? Find(int stationId) => _stations.FirstOrDefault(s => s.Id == stationId);

    /// <summary>The station after <paramref name="station"/>, or null if it is the last one.</summary>
    public Station? NextAfter(Station station)
    {
        var index = _stations.FindIndex(s => s.Id == station.Id);
        return index >= 0 && index < _stations.Count - 1 ? _stations[index + 1] : null;
    }
}
