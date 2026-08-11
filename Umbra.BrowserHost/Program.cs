using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Umbra.Core;

var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();
var lengthBytes = new byte[4];

while (await ReadExactlyAsync(input, lengthBytes))
{
    var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
    if (length is <= 0 or > 1024 * 1024) break;
    var payload = new byte[length];
    if (!await ReadExactlyAsync(input, payload)) break;

    object response;
    try
    {
        using var message = JsonDocument.Parse(payload);
        var action = message.RootElement.TryGetProperty("action", out var actionElement) ? actionElement.GetString() : "getState";
        if (action == "recordAttempt" && message.RootElement.TryGetProperty("target", out var targetElement))
        {
            BlockAttemptHistory.Record(targetElement.GetString() ?? "", "site");
            response = new { ok = true };
        }
        else
        {
            var state = BrowserBlockingState.GetCurrent();
            response = new { ok = true, blocking = state.Blocking, sites = state.Sites };
        }
    }
    catch (Exception error)
    {
        response = new { ok = false, error = error.Message };
    }

    var json = JsonSerializer.SerializeToUtf8Bytes(response, Json.Options);
    BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, json.Length);
    await output.WriteAsync(lengthBytes);
    await output.WriteAsync(json);
    await output.FlushAsync();
}

static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer)
{
    var offset = 0;
    while (offset < buffer.Length)
    {
        var read = await stream.ReadAsync(buffer.AsMemory(offset));
        if (read == 0) return false;
        offset += read;
    }
    return true;
}
