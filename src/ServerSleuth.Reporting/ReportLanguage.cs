namespace ServerSleuth.Reporting;

/// <summary>
/// Report output language. <see cref="En"/> is backward-compatible (used by all tests and the
/// existing no-arg constructor). <see cref="ZhTw"/> renders Traditional Chinese (zh-TW) labels
/// for all UI elements; data values (service names, paths, software names, etc.) are never
/// translated — only labels, headings, column headers, and category names.
/// </summary>
public enum ReportLanguage { En, ZhTw }
