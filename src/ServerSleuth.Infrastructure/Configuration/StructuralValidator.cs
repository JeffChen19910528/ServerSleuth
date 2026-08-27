using System.Text.Json;
using System.Xml;

namespace ServerSleuth.Infrastructure.Configuration;

/// <summary>
/// Attempts a real structural parse for JSON/XML (to know whether the file is actually valid,
/// and to collect top-level section names) without ever loading its content beyond what
/// System.Text.Json/System.Xml already need. XML parsing is hardened against XXE — DTD
/// processing prohibited, no XmlResolver — so a malicious config file cannot cause external
/// entity resolution or filesystem/network access. See skill.md §9, §27.
/// </summary>
public static class StructuralValidator
{
    public static (bool Valid, IReadOnlyList<string> Sections) TryValidateJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var sections = document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().Select(p => p.Name).ToList()
                : [];
            return (true, sections);
        }
        catch (JsonException)
        {
            return (false, []);
        }
    }

    public static (bool Valid, IReadOnlyList<string> Sections) TryValidateXml(string text)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        try
        {
            using var stringReader = new StringReader(text);
            using var xmlReader = XmlReader.Create(stringReader, settings);

            var sections = new List<string>();
            var atRoot = true;

            while (xmlReader.Read())
            {
                if (xmlReader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (atRoot)
                {
                    atRoot = false; // skip the document root itself, we want its children
                    continue;
                }

                if (xmlReader.Depth == 1)
                {
                    sections.Add(xmlReader.Name);
                }
            }

            return (true, sections);
        }
        catch (XmlException)
        {
            return (false, []);
        }
    }
}
