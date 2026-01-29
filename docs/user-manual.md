# Local Log Processor (LLP) - User Manual

LLP is a high-performance local log analysis tool designed to handle large files efficiently.

## Features

- **Large File Support**: Uses Memory-Mapped Files to open multi-gigabyte files instantly without high RAM usage.
- **High-Performance Search**: SQLite FTS5-backed indexing for near-instant full-text search.
- **Kibana-like Queries**: Support for field-specific searches, logical operators, and comparison operators.
- **Real-Time Monitoring**: "Tail -f" mode to watch logs as they are written.
- **Structured Parsing**: Automatically detects and parses JSON and common Regex log formats.
- **Visualizations**: Histogram of log frequency over time.
- **Export**: Save filtered results to CSV or JSON.

## Getting Started

1. **Launch the Application**: Run `LLP.UI.exe`.
2. **Open a Log File**: Click "Open Log File" and select a `.log`, `.txt`, or `.json` file.
3. **Indexing**: Upon opening, the app will start indexing the file in the background. You can see the progress in the status bar. Search is available immediately but might be incomplete until indexing finishes.

## Searching and Filtering

The search bar supports powerful query syntax:

### Full-Text Search
Simply type any word to find lines containing it.
`error`

### Field-Specific Search
If the log format is recognized, you can filter by specific fields:
`level:ERROR`
`message:"connection failed"`

### Logical Operators
Combine filters using `AND`, `OR`, (implicit AND by default), and `NOT`:
`level:ERROR NOT database`
`level:WARN OR level:ERROR`

### Comparison Operators
Filter by time or numeric values:
`timestamp:>2024-01-29T10:00:00`
`status:<500`

## Real-Time Monitoring (Tail -f)

Toggle the "Tail -f" button to automatically scroll to and display new lines as they are appended to the log file.

## Exporting Data

After applying filters, click the "Export" button to save the visible log entries to a CSV or JSON file for further analysis.

## Performance Tips

- For very large files, allow the background indexing to complete for the best search experience.
- The UI uses virtualization, so scrolling through millions of lines should remain smooth regardless of file size.

## Installation & Deployment

LLP is designed to be a portable Windows application.

### Creating a Portable Build
To create a single-file portable executable, run the following command in the project root:

```powershell
dotnet publish LLP.UI\LLP.UI.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:IncludeNativeLibrariesForSelfExtract=true
```

The resulting executable will be found in `LLP.UI\bin\Release\net10.0-windows\win-x64\publish\LLP.UI.exe`.
