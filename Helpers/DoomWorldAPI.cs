using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DoomLauncher;

[JsonSerializable(typeof(DoomWorldGetResponse))]
internal partial class JsonDoomWorldGetResponseContext : JsonSerializerContext
{
}

public class DoomWorldGetResponse
{
    [JsonPropertyName("content")]
    public DoomWorldFileEntry? Content { get; set; }
}

public class DoomWorldFileEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; } = 0;

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("dir")]
    public string Dir { get; set; } = "";

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";
}

public class DoomWorldAPI
{
    public const string DL_GERMANY = "https://www.quaddicted.com/files/idgames/";

    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task<DoomWorldFileEntry?> GetWADInfo(string wadId)
    {
        try
        {
            var jsonResponse = await httpClient.GetFromJsonAsync($"https://www.doomworld.com/idgames/api/api.php?action=get&id={wadId}&out=json", JsonDoomWorldGetResponseContext.Default.DoomWorldGetResponse);
            return jsonResponse?.Content;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return null;
        }
    }

    public static Task<Stream> DownloadWadArchive(DoomWorldFileEntry fileEntry)
    {
        return httpClient.GetStreamAsync($"{DL_GERMANY}{fileEntry.Dir}{fileEntry.Filename}");
    }
}
