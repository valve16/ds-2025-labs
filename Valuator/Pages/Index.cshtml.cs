using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;



namespace Valuator.Pages;

public class IndexModel : PageModel
{
    //private readonly IDatabase _db;
    private readonly ILogger<IndexModel> _logger;
    private const string ExchangeName = "valuator.processing.rank";
    private const string QueueName = "valuator.processing.rank";

    private readonly IConnectionMultiplexer _mainRedis;
    private readonly IDictionary<string, IConnectionMultiplexer> _multiplexers;

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer mainRedis, IDictionary<string, IConnectionMultiplexer> multiplexers)
    {
        _logger = logger;
        _mainRedis = mainRedis;
        _multiplexers = multiplexers;
    }

    public void OnGet()
    {

    }

    public async Task<IActionResult> OnPostAsync(string text, string country)
    {
        _logger.LogDebug(text);
        if (string.IsNullOrEmpty(text))
        {
            return Redirect("/");
        }

        string id = Guid.NewGuid().ToString();
        string region = GetRegionByCountry(country);

        // Сохранение ShardKey в центральной базе данных
        var mainDb = _mainRedis.GetDatabase();
        mainDb.StringSet($"ID-{id}", region);

        var segmentDb = GetSegmentDatabase(region);

        // Сохранение текста в Redis
        string textKey = "TEXT-" + id;
        segmentDb.StringSet(textKey, text);

        _logger.LogInformation($"LOOKUP: {id}, {region}");

        // Отправка задания в RabbitMQ
        await SendMessageToRabbitMQAsync(id);

        string similarityKey = "SIMILARITY-" + id;
        double similarity = CheckSimilarity(text, id, segmentDb);
        segmentDb.StringSet(similarityKey, similarity.ToString());

        // Публикация события SimilarityCalculated
        var factory = new ConnectionFactory { HostName = "localhost" };
        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: "valuator.events",
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false
            );

        var eventMessage = new
        {
            eventType = "SimilarityCalculated",
            id,
            similarity
        };
        string json = JsonSerializer.Serialize(eventMessage);
        byte[] body = Encoding.UTF8.GetBytes(json);
        await channel.BasicPublishAsync(
            exchange: "valuator.events",
            routingKey: string.Empty,
            body: body
        );

        // Перенаправление на страницу summary
        return Redirect($"summary?id={id}");
    }

    private string GetRegionByCountry(string country)
    {
        return country switch
        {
            "Russia" => "RU",
            "France" => "EU",
            "Germany" => "EU",
            "UAE" => "ASIA",
            "India" => "ASIA",
            _ => throw new ArgumentException("Недопустимая страна")
        };
    }

    private IDatabase GetSegmentDatabase(string region)
    {
        var multiplexer = _multiplexers[region];
        return multiplexer.GetDatabase();
    }

    private async Task SendMessageToRabbitMQAsync(string id)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        // Настраиваем топологию
        await DeclareTopologyAsync(channel, CancellationToken.None);

        // Формируем сообщение передавать только id
        var message = new { Id = id };

        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        // Отправляем сообщение
        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: "",
            body: body
        );

        _logger.LogInformation($"Sent message to RabbitMQ: ID={id}");
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

    private double CheckSimilarity(string text, string currentId, StackExchange.Redis.IDatabase segmentDb)
    {
        var server = segmentDb.Multiplexer.GetServer(segmentDb.Multiplexer.GetEndPoints()[0]);
        var keys = server.Keys(pattern: "TEXT-*");
        //_logger.LogInformation(" {keys}", keys);
        foreach (var key in keys)
        {
            if (key.ToString() == "TEXT-" + currentId)
            {
                continue; // Пропускаем текущий ключ
            }
            string storedText = segmentDb.StringGet(key);
            if (storedText == text)
            {
                return 1;
            }
        }
        return 0;
    }
}


