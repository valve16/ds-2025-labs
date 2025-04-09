using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.Threading;


namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly IDatabase _db;
    private readonly ILogger<IndexModel> _logger;
    private const string ExchangeName = "valuator.processing.rank";
    private const string QueueName = "valuator.processing.rank";

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public void OnGet()
    {

    }

    //public IActionResult OnPost(string text)
    //{
    //    _logger.LogDebug(text);
    //    if (string.IsNullOrEmpty(text))
    //    {
    //        return Redirect("/");
    //    }

    //    string id = Guid.NewGuid().ToString();

    //    string textKey = "TEXT-" + id;
    //    _db.StringSet(textKey, text);

    //    string rankKey = "RANK-" + id;
    //    double rank = CalculateRank(text);
    //    _db.StringSet(rankKey, rank.ToString());

    //    //string retrievedText = _db.StringGet(textKey);
    //    //_logger.LogInformation("Saved text in Redis with key {TextKey}: {RetrievedText}", textKey, retrievedText);

    //    string similarityKey = "SIMILARITY-" + id;
    //    double similarity = CheckSimilarity(text, id);
    //    _db.StringSet(similarityKey, similarity.ToString());

    //    return Redirect($"summary?id={id}");
    //}


    //private double CalculateRank(string text)
    //{
    //    if (string.IsNullOrEmpty(text))
    //    {
    //        return 0;
    //    }

    //    int nonAlphaCount = text.Count(c => !char.IsLetter(c));
    //    return (double)nonAlphaCount / text.Length;
    //}
    public async Task<IActionResult> OnPostAsync(string text)
    {
        _logger.LogDebug(text);
        if (string.IsNullOrEmpty(text))
        {
            return Redirect("/");
        }

        string id = Guid.NewGuid().ToString();

        // Сохранение текста в Redis
        string textKey = "TEXT-" + id;
        _db.StringSet(textKey, text);

        // Отправка задания в RabbitMQ
        await SendMessageToRabbitMQAsync(id, text);

        string similarityKey = "SIMILARITY-" + id;
        double similarity = CheckSimilarity(text, id);
        _db.StringSet(similarityKey, similarity.ToString());

        // Перенаправление на страницу summary
        return Redirect($"summary?id={id}");
    }

    private async Task SendMessageToRabbitMQAsync(string id, string text)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        // Настраиваем топологию
        await DeclareTopologyAsync(channel, CancellationToken.None);

        // Формируем сообщение
        var message = new { Id = id, Text = text };
        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        // Отправляем сообщение
        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: "",
            body: body
        );

        _logger.LogInformation($"Sent message to RabbitMQ: ID={id}, Text={text}");
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Direct,
            cancellationToken: ct
        );
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct
        );
        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: "",
            cancellationToken: ct
        );
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
                return 1;
            }
        }
        return 0;
    }
}


