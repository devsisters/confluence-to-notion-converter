using ConfluenceCrawler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpYaml.Serialization;
using System.Net.Http.Headers;
using System.Text;

var host = Host
    .CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<Serializer>();
        services.AddSingleton<SettingsManager>();
        services.AddHttpClient("confluence", (sp, httpClient) =>
        {
            var settingsManager = sp.GetRequiredService<SettingsManager>();
            var settings = settingsManager.LoadSettings() ??
                throw new Exception("Cannot load crawler settings.");

            var domain = settings.Confluence.Domain;
            var userName = settings.Confluence.UserName;
            var token = settings.Confluence.Token;

            httpClient.BaseAddress = new Uri($"https://{domain}", UriKind.Absolute);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("utf-8"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes($"{userName}:{token}"))
            );
        });

        services.AddSingleton<ConfluenceService>();
        services.AddSingleton<CrawlerService>();
        services.AddSingleton<FileSystemHelper>();
        services.AddSingleton<PageScrapper>();
        services.AddSingleton<ContentInspector>();
    })
    .Build();

var crawler = host.Services.GetRequiredService<CrawlerService>();
crawler.DoCrawling();
