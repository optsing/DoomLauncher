﻿using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DoomLauncher.Helpers;

[JsonSerializable(typeof(DoomWorldGetResponse))]
internal partial class JsonWebApiContext : JsonSerializerContext { }

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


public partial class WebAPI
{
    public static readonly WebAPI Current = new("Doom Launcher");

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
            var jsonResponse = await httpClient.GetFromJsonAsync($"https://www.doomworld.com/idgames/api/api.php?action=get&id={wadId}&out=json", JsonWebApiContext.Default.DoomWorldGetResponse);
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

    public async Task<DownloadEntryList?> DownloadEntriesFromJson(string url)
    {
        try
        {
            var jsonResponse = await httpClient.GetFromJsonAsync(url, JsonDownloadEntryContext.Default.DownloadEntryList);
            return jsonResponse;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return null;
        }
    }

    public async Task<string> GetDirectUrlFromModDB(string url)
    {
        var html = await httpClient.GetStringAsync(url);
        if (reModDBDirectUrl().Match(html) is var match && match.Success && match.Groups[1].Value is string directUrl)
        {
            return "https://www.moddb.com" + directUrl;
        }
        throw new Exception("ModDB url can't be parsed");
    }

    [GeneratedRegex(@"href=""(\/downloads\/mirror\/.+?)""")]
    private static partial Regex reModDBDirectUrl();
}
