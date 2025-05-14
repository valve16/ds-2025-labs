using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace RankCalculator;

class Program
{
    private static readonly ConnectionMultiplexer mainRedis = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("DB_MAIN") /*?? "localhost:6000"*/);
    private static readonly IServiceProvider serviceProvider = ConfigureServices();

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(mainRedis);

        // Регистрация сегментированных подключений Redis по регионам
        services.AddSingleton<IDictionary<string, IConnectionMultiplexer>>(sp =>
        {
            return new Dictionary<string, IConnectionMultiplexer>
            {
                { "RU", ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("DB_RU")/* ?? "localhost:6001"*/) },
                { "EU", ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("DB_EU") /*?? "localhost:6002"*/) },
                { "ASIA", ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("DB_ASIA") /*?? "localhost:6003"*/) }
            };
        });

        return services.BuildServiceProvider();
    }

    //private static readonly ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost");
    //private static readonly IDatabase db = redis.GetDatabase();
    private const string QueueName = "valuator.processing.rank";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("RankCalculator started");

        var factory = new ConnectionFactory { HostName = "localhost" };
        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        await DeclareTopologyAsync(channel);
        string consumerTag = await RunConsumer(channel);

        Console.WriteLine("Press Enter to exit");
        Console.ReadLine();

        await channel.BasicCancelAsync(consumerTag);

        Console.WriteLine("done");
    }

    private static async Task<string> RunConsumer(IChannel channel)
    {
        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += (_, eventArgs) => ConsumeAsync(channel, eventArgs);
        return await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer
        );
    }

    private static async Task ConsumeAsync(IChannel channel, BasicDeliverEventArgs eventArgs)
    {
        Console.WriteLine("Consuming");
        string message = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        var data = JsonSerializer.Deserialize<Message>(message);

        string id = data.Id;

        var mainDb = mainRedis.GetDatabase();
        string region = mainDb.StringGet("ID-" + id);

        var segmentDb = GetSegmentDatabase(region);

        // Вычисляем ранг
        double rank = CalculateRank(segmentDb.StringGet("TEXT-" + id));
        // Сохраняем результат в Redis
        segmentDb.StringSet("RANK-" + id, rank.ToString());

        // сообщение
        var eventMessage = new
        {
            eventType = "RankCalculated",
            id,
            rank
        };

        string json = JsonSerializer.Serialize(eventMessage);
        byte[] body = Encoding.UTF8.GetBytes(json);
        await channel.BasicPublishAsync(
            exchange: "valuator.events",
            routingKey: string.Empty,
            body: body
        );

        Console.WriteLine($"Processed: ID={id}, Rank={rank}");
        Console.WriteLine($"LOOKUP: {id}, {region}");
        await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
    }

    private static double CalculateRank(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        int nonAlphaCount = text.Count(c => !char.IsLetter(c));
        return (double)nonAlphaCount / text.Length;
    }

    private static async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        await channel.ExchangeDeclareAsync(
            exchange: "valuator.events",
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false
        );
    }
    private static IDatabase GetSegmentDatabase(string region)
    {
        var multiplexers = serviceProvider.GetRequiredService<IDictionary<string, IConnectionMultiplexer>>();
        if (multiplexers.TryGetValue(region, out var multiplexer))
        {
            return multiplexer.GetDatabase();
        }
        throw new ArgumentException($"No Redis connection found for region: {region}");
    }
    private class Message
    {
        public string Id { get; set; }
    }
}