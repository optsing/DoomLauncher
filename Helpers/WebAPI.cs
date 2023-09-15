using System;
using System.Collections.Generic;
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

[JsonSerializable(typeof(List<GitHubReleaseEntry>))]
internal partial class JsonGitHubReleasesContext : JsonSerializerContext
{
}

public class GitHubReleaseEntry
{
    [JsonPropertyName("assets")]
    public List<GitHubAssetEntry> Assets { get; set; } = new();
}

public class GitHubAssetEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = "";
}

public class WebAPI
{
    public const string DoomWorldDlGermany = "https://www.quaddicted.com/files/idgames/";

    private readonly HttpClient httpClient;

    public WebAPI(string userAgent)
    {
        httpClient = new();
        httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
    }

    public async Task<DoomWorldFileEntry?> GetDoomWorldWADInfo(string wadId)
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

    public Task<Stream> DownloadDoomWorldWadArchive(DoomWorldFileEntry fileEntry)
    {
        return httpClient.GetStreamAsync($"{DoomWorldDlGermany}{fileEntry.Dir}{fileEntry.Filename}");
    }

    public Task<Stream> DownloadUrl(string url)
    {
        return httpClient.GetStreamAsync(url);
    }

    public const string GitHubGZDoomEndpoint = "https://api.github.com/repos/ZDoom/gzdoom/releases?per_page=100";

    public async Task<List<GitHubReleaseEntry>> GetGZDoomGitHubReleases()
    {
        try
        {
            var jsonResponse = await httpClient.GetFromJsonAsync(GitHubGZDoomEndpoint, JsonGitHubReleasesContext.Default.ListGitHubReleaseEntry);
            return jsonResponse ?? new();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return new();
        }
    }
}
