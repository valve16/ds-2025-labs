using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Text.Json;

namespace RankCalculator;

class Program
{
    private static readonly ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost");
    private static readonly IDatabase db = redis.GetDatabase();
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

        // Вычисляем ранг
        double rank = CalculateRank(db.StringGet("TEXT-" + id));
        // Сохраняем результат в Redis
        db.StringSet("RANK-" + id, rank.ToString());

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

    private class Message
    {
        public string Id { get; set; }
    }
}