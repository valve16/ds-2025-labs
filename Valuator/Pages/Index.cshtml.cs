using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;


namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly IDatabase _db;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public void OnGet()
    {

    }

    public IActionResult OnPost(string text)
    {
        _logger.LogDebug(text);

        string id = Guid.NewGuid().ToString();

        string textKey = "TEXT-" + id;
        _db.StringSet(textKey, text);

        string rankKey = "RANK-" + id;
        double rank = CalculateRank(text);
        _db.StringSet(rankKey, rank.ToString());

        //string retrievedText = _db.StringGet(textKey);
        //_logger.LogInformation("Saved text in Redis with key {TextKey}: {RetrievedText}", textKey, retrievedText);

        string similarityKey = "SIMILARITY-" + id;
        double similarity = CheckSimilarity(text, id);
        _db.StringSet(similarityKey, similarity.ToString());

        return Redirect($"summary?id={id}");
    }
    private double CalculateRank(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0.0;
        }

        int nonAlphaCount = text.Count(c => !char.IsLetter(c));
        return (double)nonAlphaCount / text.Length;
    }

    private double CheckSimilarity(string text, string currentId)
    {
        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints()[0]);
        var keys = server.Keys(pattern: "TEXT-*");
        //_logger.LogInformation(" {keys}", keys);
        foreach (var key in keys)
        {
            if (key.ToString() == "TEXT-" + currentId)
            {
                continue; // Пропускаем текущий ключ
            }
            string storedText = _db.StringGet(key);
            if (storedText == text)
            {
                return 1.0;
            }
        }
        return 0.0;
    }
}


