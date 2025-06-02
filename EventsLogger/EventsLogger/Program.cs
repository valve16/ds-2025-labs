using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

var factory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = Environment.GetEnvironmentVariable("RABBIT_USER") ?? "",
    Password = Environment.GetEnvironmentVariable("RABBIT_PASS") ?? ""
};

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

await channel.ExchangeDeclareAsync(exchange: "valuator.events",
                                    type: ExchangeType.Fanout,
                                    durable: true);

// declare a server-named queue
QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
string queueName = queueDeclareResult.QueueName;
await channel.QueueBindAsync(queue: queueName,
                            exchange: "valuator.events",
                            routingKey: string.Empty);

Console.WriteLine(" [*] Waiting for logs.");

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += (model, ea) =>
{
    byte[] body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    Console.WriteLine($" [x] {message}");
    return Task.CompletedTask;
};

await channel.BasicConsumeAsync(queueName, autoAck: true,
                                consumer: consumer);

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();