using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IConnectionMultiplexer _mainRedis;
    private readonly IDictionary<string, IConnectionMultiplexer> _multiplexers;

    public SummaryModel(ILogger<SummaryModel> logger,
                        IConnectionMultiplexer mainRedis,
                        IDictionary<string, IConnectionMultiplexer> multiplexers)
    {
        _logger = logger;
        _mainRedis = mainRedis;
        _multiplexers = multiplexers;
    }

    public double Rank { get; set; }
    public double Similarity { get; set; }

    public void OnGet(string id)
    {
        _logger.LogDebug(id);

        var mainDb = _mainRedis.GetDatabase();
        string? region = mainDb.StringGet("ID-" + id);

        if (string.IsNullOrEmpty(region))
        {
            Rank = -1;
            Similarity = 0;
            _logger.LogWarning($"Region not found for ID: {id}");
            return;
        }
        // TODO: (pa1) проинициализировать свойства Rank и Similarity значениями из БД (Redis)
        var segmentDb = GetSegmentDatabase(region);

        string? rankValue = segmentDb.StringGet("RANK-" + id);
        string? similarityValue = segmentDb.StringGet("SIMILARITY-" + id);

        if (rankValue != null)
        {
            Rank = double.Parse(rankValue);
        }
        else
        {
            Rank = -1; // Индикатор, что rank еще не вычислен
        }

        Similarity = double.TryParse(similarityValue, out double similarity) ? similarity : 0;
        _logger.LogInformation($"LOOKUP: {id}, {region}");
    }
    private IDatabase GetSegmentDatabase(string region)
    {
        if (_multiplexers.TryGetValue(region, out var multiplexer))
        {
            return multiplexer.GetDatabase();
        }
        throw new ArgumentException($"No Redis connection found for region: {region}");
    }

}

