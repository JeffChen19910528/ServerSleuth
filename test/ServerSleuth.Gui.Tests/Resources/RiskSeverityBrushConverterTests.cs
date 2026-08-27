using System.IO;
using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Gui.Tests.Resources;

/// <summary>
/// GUI-1 §7: risk severity visual mapping must be based on the EXISTING
/// <see cref="RiskSeverity"/> domain enum — never an invented value. A live WPF
/// <c>Application</c>/<c>ResourceDictionary</c> load requires an STA thread this test
/// deliberately avoids needing (skill.md GUI-1 §9: "keep the GUI unit-test layer focused on
/// ViewModels/services... lightweight shell construction tests" where full UI-thread testing
/// isn't practical) — instead this reads <c>Theme.xaml</c>'s own source text directly and
/// confirms a <c>Risk.&lt;Name&gt;</c> resource key exists for EVERY current
/// <see cref="RiskSeverity"/> member, and no extra one for a value that doesn't exist on the
/// real enum.
/// </summary>
public class RiskSeverityBrushConverterTests
{
    [Fact]
    public void ThemeXaml_DefinesExactlyOneBrushResource_PerExistingRiskSeverityValue_NoInventedValue()
    {
        var themeXamlPath = FindThemeXaml();
        var xaml = File.ReadAllText(themeXamlPath);

        var enumNames = Enum.GetNames<RiskSeverity>();
        foreach (var name in enumNames)
        {
            Assert.Contains($"x:Key=\"Risk.{name}\"", xaml, StringComparison.Ordinal);
        }

        var declaredRiskKeys = System.Text.RegularExpressions.Regex.Matches(xaml, "x:Key=\"Risk\\.(\\w+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        // No brush resource exists for a severity name that isn't a real enum member.
        Assert.All(declaredRiskKeys, key => Assert.Contains(key, enumNames));
        Assert.Equal(enumNames.Length, declaredRiskKeys.Count);
    }

    [Fact]
    public void RiskSeverity_HasNoNoneValue_MatchingTheRealDomainEnum()
    {
        // skill.md GUI-1 §7's own example list mentioned "None" — the REAL domain enum (Phase
        // 7A) has no such member; this test locks in that the GUI's mapping was built against
        // the actual enum, not the spec's own approximate example.
        Assert.DoesNotContain("None", Enum.GetNames<RiskSeverity>());
    }

    private static string FindThemeXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ServerSleuth.Gui", "Resources", "Theme.xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate Theme.xaml from the test output directory.");
    }
}
