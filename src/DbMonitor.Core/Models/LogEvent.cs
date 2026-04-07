using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DbMonitor.Core.Models;

public class LogEvent
{
    [JsonPropertyName("@t")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("@mt")]
    public string? MessageTemplate { get; set; }

    [JsonPropertyName("@m")]
    public string? Message { get; set; }

    [JsonPropertyName("@l")]
    public string? Level { get; set; }

    [JsonPropertyName("@x")]
    public string? Exception { get; set; }

    [JsonPropertyName("EventType")]
    public string? EventType { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Properties { get; set; }
}
