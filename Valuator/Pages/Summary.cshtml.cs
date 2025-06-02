using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
namespace Valuator.Pages;

[Authorize]
public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IDatabase _db;

    public SummaryModel(ILogger<SummaryModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public double Rank { get; set; }
    public double Similarity { get; set; }

    public IActionResult OnGet(string id)
    {
        _logger.LogDebug(id);
        if (!User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/Login");
        }

        string userKey = "USER-" + id;
        string? author = _db.StringGet(userKey);
        if (author != User.Identity.Name)
        {
            return Forbid(); // 403 - доступ запрещен
        }

        // TODO: (pa1) проинициализировать свойства Rank и Similarity значениями из БД (Redis)
        string rankKey = "RANK-" + id;
        string similarityKey = "SIMILARITY-" + id;

        string? rankValue = _db.StringGet(rankKey);
        string? similarityValue = _db.StringGet(similarityKey);

        if (rankValue != null)
        {
            Rank = double.Parse(rankValue);
        }
        else
        {
            Rank = -1; // Индикатор, что rank еще не вычислен
        }

        Similarity = double.TryParse(similarityValue, out double similarity) ? similarity : 0;
        return Page();
    }


}

