using System.Net.WebSockets;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure CORS to allow frontend connection
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:3000", "http://localhost:3001")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseWebSockets();

// Global state
var dataBuffer = new DataBuffer();
var clients = new List<WebSocket>();

// Start TCP client to receive data from datagen.js
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("localhost", 9000);
            Console.WriteLine("Connected to data generator on port 9000");
            
            using var stream = tcpClient.GetStream();
            using var reader = new StreamReader(stream);
            
            while (tcpClient.Connected)
            {
                var line = await reader.ReadLineAsync();
                if (line != null)
                {
                    try
                    {
                        // Parse as int array first (since datagen.js sends integers)
                        var intValues = JsonSerializer.Deserialize<int[]>(line);
                        if (intValues != null && intValues.Length == 10)
                        {
                            // Convert to float array
                            var floatValues = intValues.Select(v => (float)v).ToArray();
                            
                            var dataPoint = new DataPoint 
                            { 
                                Timestamp = DateTime.UtcNow, 
                                Values = floatValues 
                            };
                            
                            dataBuffer.Add(dataPoint);
                            
                            // Broadcast to all connected WebSocket clients
                            var message = JsonSerializer.Serialize(new
                            {
                                type = "data",
                                payload = dataPoint
                            });
                            
                            var bytes = Encoding.UTF8.GetBytes(message);
                            var deadClients = new List<WebSocket>();
                            
                            foreach (var client in clients.ToList())
                            {
                                try
                                {
                                    if (client.State == WebSocketState.Open)
                                    {
                                        await client.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                                    }
                                    else
                                    {
                                        deadClients.Add(client);
                                    }
                                }
                                catch
                                {
                                    deadClients.Add(client);
                                }
                            }
                            
                            // Remove dead clients
                            foreach (var client in deadClients)
                            {
                                clients.Remove(client);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                        Console.WriteLine($"Raw data: {line}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TCP Connection Error: {ex.Message}");
            Console.WriteLine("Retrying in 5 seconds...");
            await Task.Delay(5000);
        }
    }
});

// WebSocket endpoint
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var ws = await context.WebSockets.AcceptWebSocketAsync();
        clients.Add(ws);
        Console.WriteLine($"Client connected. Total clients: {clients.Count}");
        
        try
        {
            // Send historical data
            var history = dataBuffer.GetRecent();
            var historyMessage = JsonSerializer.Serialize(new
            {
                type = "history",
                payload = history
            });
            await ws.SendAsync(Encoding.UTF8.GetBytes(historyMessage), WebSocketMessageType.Text, true, CancellationToken.None);
            
            // Keep connection alive and handle incoming messages
            var buffer = new byte[1024];
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
        }
        finally
        {
            clients.Remove(ws);
            Console.WriteLine($"Client disconnected. Total clients: {clients.Count}");
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Health check endpoint
app.MapGet("/health", () => new { status = "healthy", clients = clients.Count });

Console.WriteLine("Backend server starting on http://localhost:5000");
await app.RunAsync("http://localhost:5000");

// Data models
public class DataPoint
{
    public DateTime Timestamp { get; set; }
    public float[] Values { get; set; } = new float[10];
}

public class DataBuffer
{
    private readonly Queue<DataPoint> buffer = new();
    private readonly object lockObj = new();
    private const int MaxBufferSize = 3000; // 30 seconds at 100Hz

    public void Add(DataPoint point)
    {
        lock (lockObj)
        {
            buffer.Enqueue(point);

            // Keep only last 30 seconds of data
            while (buffer.Count > MaxBufferSize)
            {
                buffer.Dequeue();
            }
        }
    }

    public List<DataPoint> GetRecent()
    {
        lock (lockObj)
        {
            return buffer.ToList();
        }
    }
}

