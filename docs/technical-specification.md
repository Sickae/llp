LLP is a high-performance, local log analysis tool designed to handle large files with Kibana-like search and filtering capabilities. It is built using **WPF** on **.NET 10**.

#### 1. Core Objectives
- **Efficiency**: Handle multi-gigabyte log files without exhausting system memory.
- **Searchability**: Provide powerful, near-instant search and filtering using a syntax familiar to Kibana users (KQL-like or Lucene-like).
- **Usability**: Offer a clean, responsive UI for navigating through millions of log lines.

#### 2. Functional Requirements
- **File Management**:
    - Open local text files (.log, .txt, .json).
    - Support for "Tail -f" (real-time monitoring of active logs).
- **Processing & Indexing**:
    - Automatic detection of log formats (Timestamp, Level, Message).
    - Optional in-memory or temporary disk-based indexing for rapid filtering.
- **Search & Filter**:
    - Full-text search.
    - Field-specific filtering (e.g., `level:ERROR` or `status:500`).
    - Time-range filtering.
    - Support for logical operators (`AND`, `OR`, `NOT`).
- **Visualization**:
    - Histogram of log frequency over time.
    - Color-coded log levels (Error = Red, Warning = Yellow).

#### 3. Technical Architecture

##### 3.1. Storage & Memory Strategy
To handle "large log files," the application will use **Virtualization** and **Memory-Mapped Files**:
- **Memory-Mapped Files (MMF)**: Instead of loading the whole file into RAM, the app maps the file to virtual memory, letting the OS handle caching.
- **UI Virtualization**: Using WPF's `VirtualizingStackPanel` to ensure the UI only renders the lines currently visible on screen.
- **Background Indexing**: A background process scans the file to identify line offsets and metadata, enabling instant jumps to specific lines.

##### 3.2. Processing Pipeline
1.  **Ingestion**: Read file using `FileStream` with asynchronous I/O.
2.  **Parsing**: Use a pipeline of Regex or high-performance string parsers (`ReadOnlySpan<char>`) to extract fields.
3.  **Search Engine**: Implement a lightweight inverted index or use an embedded library like **Lucene.NET** or a simple SQLite FTS5 (Full-Text Search) for complex queries.

#### 4. UI/UX Design (WPF)
- **Top Bar**: Search input field with auto-completion and "Time Range" picker.
- **Left Panel**: "Fields" list (similar to Kibana) showing unique keys found in logs.
- **Main Area**: A `DataGrid` or customized `ListView` optimized for high-performance scrolling.
- **Bottom Bar**: Statistics (Total lines, Filtered count, File size, Processing status).

#### 5. Proposed Technology Stack
- **Framework**: .NET 10 (WPF).
- **Language**: C# 14.
- **Performance**: `System.IO.Pipelines` for high-performance log parsing.
- **Logging/Internal**: Serilog for self-diagnostics.
- **Storage (Optional)**: SQLite for persistent indexing of extremely large files.

#### 6. Implementation Status
The project has successfully completed all planned milestones:
- **Phase 1**: Core architecture with Memory-Mapped Files and UI virtualization is operational.
- **Phase 2**: Pluggable parsing system (Regex/JSON) with automatic format detection is implemented.
- **Phase 3**: Advanced query engine supporting field-specific search and logical operators is integrated.
- **Phase 4**: High-performance indexing using SQLite FTS5 for large files is functional.
- **Phase 5**: Real-time "Tail -f" support, color-coded log levels, and export (CSV/JSON) are available.
- **Phase 6**: User documentation and final release packaging completed.