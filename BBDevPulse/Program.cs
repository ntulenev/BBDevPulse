using System.Net.Http.Headers;
using System.Text;

using BBDevPulse.Abstractions;
using BBDevPulse.API;
using BBDevPulse.Configuration;
using BBDevPulse.Logic;
using BBDevPulse.Math;
using BBDevPulse.Presentation;
using BBDevPulse.Presentation.Formatters;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

Console.OutputEncoding = Encoding.UTF8;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(static (__, config) =>
    {
        _ = config.SetBasePath(AppContext.BaseDirectory)
             .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
    })
    .ConfigureServices((context, services) =>
    {
        _ = services.AddOptions<BitbucketOptions>()
            .Bind(context.Configuration.GetSection(BitbucketOptions.SECTION_NAME))
            .ValidateOnStart();
        _ = services.AddSingleton<IValidateOptions<BitbucketOptions>, BitbucketOptionsValidator>();

        _ = services.AddSingleton<IBitbucketMapper, BBDevPulse.API.Mappers.BitbucketMapper>();
        _ = services.AddSingleton<IPullRequestActivityMapper, BBDevPulse.API.Mappers.PullRequestActivityMapper>();
        _ = services.AddSingleton<IPaginatorHelper, PaginatorHelper>();

        _ = services.AddHttpClient<IBitbucketTransport, BitbucketTransport>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<BitbucketOptions>>().Value;
            client.BaseAddress = new Uri("https://api.bitbucket.org/2.0/");

            var authBytes = Encoding.ASCII.GetBytes($"{options.Username}:{options.AppPassword}");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        _ = services.AddTransient<IBitbucketClient, BitbucketClient>();

        _ = services.AddSingleton<IReportPresenter, SpectreReportPresenter>();
        _ = services.AddSingleton<IAuthPresenter, SpectreAuthPresenter>();
        _ = services.AddSingleton<IRepositoryListPresenter, SpectreRepositoryListPresenter>();
        _ = services.AddSingleton<IRepositoryAnalysisPresenter, SpectreRepositoryAnalysisPresenter>();
        _ = services.AddSingleton<IBranchFilterPresenter, SpectreBranchFilterPresenter>();
        _ = services.AddSingleton<IPullRequestReportPresenter, SpectrePullRequestReportPresenter>();
        _ = services.AddSingleton<IStatisticsPresenter, SpectreStatisticsPresenter>();
        _ = services.AddSingleton<IStatisticsCalculator, StatisticsCalculator>();
        _ = services.AddSingleton<IDateDiffFormatter, DateDiffFormatter>();
        _ = services.AddSingleton<IActivityAnalyzer, ActivityAnalyzer>();
        _ = services.AddSingleton<IPullRequestAnalyzer, PullRequestAnalyzer>();
        _ = services.AddSingleton<IReportRunner, ReportRunner>();
    })
    .Build();

await host.Services
    .GetRequiredService<IReportRunner>()
    .RunAsync(CancellationToken.None)
    .ConfigureAwait(false);
