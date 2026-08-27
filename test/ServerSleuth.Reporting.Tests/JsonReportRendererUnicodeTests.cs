using System.Text;
using System.Text.Json;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Unicode safety — see skill.md (Phase 9A) §9: English, Traditional Chinese, and
/// Unicode application/path names must round-trip through JSON without corruption.</summary>
public class JsonReportRendererUnicodeTests
{
    [Fact]
    public void TraditionalChineseApplicationName_RoundTripsUncorrupted()
    {
        const string chineseName = "伺服器遷移評估";
        var site = EntityFactory.Site(chineseName, @"D:\伺服器\Web");
        var app = EntityFactory.Application(chineseName, "/", @"D:\伺服器\Web", siteId: site.Id);
        var webDll = EntityFactory.Dll(@"D:\伺服器\Web\主程式.dll", referencedBy: [app.Id], importsCsv: "缺少的檔案.dll");
        var missingDll = EntityFactory.Dll(@"D:\伺服器\Web\缺少的檔案.dll", notFound: true);

        var entities = new List<DiscoveryEntity> { site, app, webDll, missingDll };
        var report = TestPipeline.Run(entities);
        var result = new JsonReportRenderer().Render(report);

        Assert.Contains(chineseName, ExtractAllStrings(result.Content), StringComparer.Ordinal);
        Assert.Contains("伺服器", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void MixedEnglishAndUnicodePaths_RoundTripUncorrupted_ViaJsonParse()
    {
        const string mixedPath = @"D:\Ünïcödé Path 路徑\app.exe";
        var service = EntityFactory.Service("MixedSvc", mixedPath);
        var missingExe = EntityFactory.Dll(mixedPath, notFound: true);

        var entities = new List<DiscoveryEntity> { service, missingExe };
        var report = TestPipeline.Run(entities);
        var result = new JsonReportRenderer().Render(report);
        var json = JsonDocument.Parse(result.Content);

        var action = json.RootElement.GetProperty("Actions").EnumerateArray().Single();
        var details = action.GetProperty("Evidence").EnumerateArray().Select(e => e.GetProperty("Detail").GetString()).ToList();
        var pathDetail = Assert.Single(details, d => d != null && d.Contains("路徑", StringComparison.Ordinal));

        Assert.Contains("Ünïcödé", pathDetail);
    }

    [Fact]
    public void RenderedContent_IsValidUtf8()
    {
        var chineseName = "測試伺服器";
        var site = EntityFactory.Site(chineseName);
        var report = TestPipeline.Run([site]);
        var result = new JsonReportRenderer().Render(report);

        Assert.Equal(Encoding.UTF8, result.Encoding);
        var bytes = result.Encoding.GetBytes(result.Content);
        var roundTripped = result.Encoding.GetString(bytes);
        Assert.Equal(result.Content, roundTripped);
    }

    private static IEnumerable<string> ExtractAllStrings(string json)
    {
        var doc = JsonDocument.Parse(json);
        return Walk(doc.RootElement);

        static IEnumerable<string> Walk(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    yield return element.GetString() ?? string.Empty;
                    break;
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        foreach (var s in Walk(prop.Value)) yield return s;
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        foreach (var s in Walk(item)) yield return s;
                    }
                    break;
            }
        }
    }
}
