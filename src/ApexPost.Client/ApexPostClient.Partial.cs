namespace ApexPost.Client;

/// <summary>
/// Hand-written extension point of the generated client (the generated file declares this class partial).
/// </summary>
public partial class ApexPostClient
{
    // The API sends and accepts enums as strings (for example "InTransit").
    static partial void UpdateJsonSerializerSettings(System.Text.Json.JsonSerializerOptions settings)
    {
        settings.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }
}
