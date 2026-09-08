using System.Net;
using System.Text.Json;

namespace StorageClient.Redesign.Services;

public static class ApiErrorFormatter
{
    public static string Format(HttpRequestException exception)
    {
        if (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Авторизация выполнена, но API не принял полученный токен.";
        }

        if (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return "У этой учётной записи нет доступа к складу.";
        }

        string? serverMessage = TryReadServerMessage(exception.Message);
        return serverMessage
            ?? "Не удалось подключиться к API склада. Проверьте адрес сервера и доступность сети.";
    }

    private static string? TryReadServerMessage(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            foreach (string propertyName in new[] { "message", "detail", "error" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out JsonElement value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Сервер вернул обычный текст, а не JSON.
        }

        return string.IsNullOrWhiteSpace(text) || text.Length > 240 ? null : text;
    }
}
