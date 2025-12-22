# FeedStackEngine - Project Document

## Overview
**FeedStackEngine** is a serverless backend news aggregator engine built on Azure's low-cost stack. It automatically wakes up every hour to fetch, parse, and store news articles from configured RSS feeds into Azure Table Storage.

### Goals
- **Cost-Effective**: Leverage Azure's consumption-based pricing (Functions + Table Storage)
- **Automated**: Timer-triggered aggregation with no manual intervention
- **Configurable**: Central JSON configuration for managing RSS feed sources
- **Scalable**: Serverless architecture that scales with demand

---

## Table of Contents
1. [Project Architecture](#project-architecture)
2. [System Design](#system-design)
3. [Technology Stack](#technology-stack)
4. [Data Models](#data-models)
5. [API Design](#api-design)
6. [Implementation Plan](#implementation-plan)

---

## Project Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         AZURE CLOUD                              │
│  ┌─────────────┐    ┌──────────────────┐    ┌────────────────┐  │
│  │   Timer     │───▶│  Azure Function  │───▶│  Azure Table   │  │
│  │  (Hourly)   │    │  (Aggregator)    │    │    Storage     │  │
│  └─────────────┘    └────────┬─────────┘    └────────────────┘  │
│                              │                                   │
│                              ▼                                   │
│                     ┌────────────────┐                          │
│                     │  feeds.json    │                          │
│                     │  (Config)      │                          │
│                     └────────────────┘                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌─────────────────┐
                    │  External RSS   │
                    │     Feeds       │
                    │  (Internet)     │
                    └─────────────────┘
```

### Components

| Component | Description |
|-----------|-------------|
| **Timer Trigger** | Azure Functions timer that fires every hour (CRON: `0 0 * * * *`) |
| **Aggregator Function** | Core logic that reads config, fetches RSS feeds, parses articles |
| **Feed Configuration** | `feeds.json` - Central JSON file containing all RSS feed URLs and metadata |
| **Azure Table Storage** | NoSQL storage for persisted news articles |
| **RSS Parser** | Module to parse RSS/Atom feed formats |

### Directory Structure
```
FeedStackEngine/
├── src/
│   └── FeedStackEngine.Functions/           # Azure Functions project
│       ├── Functions/
│       │   ├── NewsAggregatorFunction.cs     # Timer-triggered (hourly) - fetch feeds
│       │   └── ArticleArchiverFunction.cs    # Timer-triggered (daily) - archive old articles
│       ├── Services/
│       │   ├── IFeedParserService.cs         # Interface for feed parsing
│       │   ├── FeedParserService.cs          # RSS feed parsing implementation
│       │   ├── IArticleTableService.cs       # Interface for table operations
│       │   ├── ArticleTableService.cs        # Azure Table operations (hot + cold)
│       │   ├── IArticleLookupService.cs      # Interface for deduplication
│       │   ├── ArticleLookupService.cs       # Deduplication service
│       │   ├── IFeedConfigService.cs         # Interface for feed config
│       │   └── FeedConfigService.cs          # Load feeds from config
│       ├── Models/
│       │   ├── ArticleEntity.cs              # Article table entity
│       │   ├── ArticleLookupEntity.cs        # Lookup table entity
│       │   ├── FeedSource.cs                 # Feed source model
│       │   └── FeedConfiguration.cs          # Configuration model
│       ├── Utilities/
│       │   ├── TimestampHelper.cs            # Inverted timestamp utilities
│       │   └── HashHelper.cs                 # MD5 hashing for article URLs
│       ├── Config/
│       │   └── feeds.json                    # RSS feed configuration
│       ├── Program.cs                        # Function app startup & DI
│       ├── host.json                         # Azure Functions host config
│       ├── local.settings.json               # Local dev settings
│       └── FeedStackEngine.Functions.csproj  # Project file
├── tests/
│   └── FeedStackEngine.Tests/
│       ├── Services/
│       │   ├── FeedParserServiceTests.cs
│       │   ├── ArticleTableServiceTests.cs
│       │   └── ArticleLookupServiceTests.cs
│       ├── Utilities/
│       │   └── TimestampHelperTests.cs
│       └── FeedStackEngine.Tests.csproj
├── FeedStackEngine.sln                       # Solution file
└── README.md
```

---

## System Design

### Core Features

1. **Scheduled Aggregation**
   - Timer-triggered Azure Function runs every hour
   - Fetches all configured RSS feeds in parallel
   - Handles feed failures gracefully (continues with other feeds)

2. **Feed Configuration Management**
   - Central `feeds.json` file for all RSS sources
   - Support for feed metadata (category, priority, enabled/disabled)
   - Easy to add/remove feeds without code changes

3. **Article Storage**
   - Store articles in Azure Table Storage
   - Deduplication based on article URL/GUID
   - Track article metadata (source, published date, fetch date)

4. **Error Handling & Logging**
   - Log failed feed fetches
   - Retry logic for transient failures
   - Application Insights integration (optional)

### Data Flow

```
1. Timer fires (every hour)
       │
       ▼
2. Load feeds.json configuration
       │
       ▼
3. Fetch RSS feeds in parallel
       │
       ▼
4. Parse each feed (RSS/Atom)
       │
       ▼
5. Extract articles with metadata
       │
       ▼
6. Check for duplicates in Table Storage
       │
       ▼
7. Insert new articles into Table Storage
       │
       ▼
8. Log summary (new articles, failures, etc.)
```

### Dependencies

| Dependency | Purpose |
|------------|---------|
| `Microsoft.Azure.Functions.Worker` | Azure Functions (.NET Isolated) runtime |
| `Azure.Data.Tables` | Azure Table Storage SDK |
| `CodeHollow.FeedReader` | Parse RSS/Atom feeds (or System.ServiceModel.Syndication) |
| `System.Net.Http` | HTTP requests to feeds |

---

## Technology Stack

| Layer | Technology | Reasoning |
|-------|------------|-----------|
| **Runtime** | .NET 8 (LTS) | Enterprise-grade, excellent Azure integration, strong typing |
| **Language** | C# 12 | Type safety, async/await, LINQ for data processing |
| **Compute** | Azure Functions v4 (.NET Isolated) | Pay-per-execution, auto-scaling, ~1M free executions/month |
| **Database** | Azure Table Storage | Ultra low-cost NoSQL, pay for storage + transactions |
| **Configuration** | JSON file (embedded) | Simple, version-controlled, no extra service needed |
| **Monitoring** | Application Insights (optional) | Logging, metrics, alerting |

### Cost Estimate (Low Usage)
- **Azure Functions**: Free tier covers ~1M executions/month
- **Azure Table Storage**: ~$0.045/GB/month + minimal transaction costs
- **Estimated Monthly Cost**: < $1/month for typical usage

---

## Data Models

### Feed Source (feeds.json)

```json
{
  "feeds": [
    {
      "id": "techcrunch",
      "name": "TechCrunch",
      "url": "https://techcrunch.com/feed/",
      "category": "technology",
      "country": "US",
      "priority": "headline",
      "enabled": true,
      "parserConfig": null
    },
    {
      "id": "bbc-world",
      "name": "BBC World News",
      "url": "http://feeds.bbci.co.uk/news/world/rss.xml",
      "category": "world",
      "country": "GB",
      "priority": "headline",
      "enabled": true,
      "parserConfig": {
        "imageSource": "mediaThumbnail",
        "descriptionSource": "summary"
      }
    },
    {
      "id": "reddit-tech",
      "name": "Reddit Technology",
      "url": "https://www.reddit.com/r/technology/.rss",
      "category": "technology",
      "country": "US",
      "priority": "standard",
      "enabled": true,
      "parserConfig": {
        "imageSource": "contentHtml",
        "imageRegex": "<img[^>]+src=\"([^\"]+)\"",
        "descriptionSource": "contentHtml",
        "stripHtml": true
      }
    }
  ],
  "settings": {
    "retentionDays": 90,
    "maxConcurrentFeeds": 10
  },
  "priorityDefinitions": {
    "headline": {
      "displayOrder": 1,
      "description": "Top-tier sources for breaking news and headlines",
      "boostFactor": 2.0
    },
    "featured": {
      "displayOrder": 2,
      "description": "Quality sources for featured stories",
      "boostFactor": 1.5
    },
    "standard": {
      "displayOrder": 3,
      "description": "General news sources",
      "boostFactor": 1.0
    },
    "supplemental": {
      "displayOrder": 4,
      "description": "Additional coverage, lower priority",
      "boostFactor": 0.5
    }
  },
  "parserDefaults": {
    "imageSource": "auto",
    "descriptionSource": "auto",
    "stripHtml": true,
    "maxDescriptionLength": 500
  }
}
```

---

## Feed Priority System

Different feeds serve different purposes. The priority system allows categorizing feeds by their editorial value and controlling how articles are served.

### Priority Tiers

| Priority | Display Order | Boost Factor | Use Case |
|----------|---------------|--------------|----------|
| **headline** | 1 (highest) | 2.0x | Major news outlets for breaking news (BBC, Reuters, AP) |
| **featured** | 2 | 1.5x | Quality sources for in-depth coverage (TechCrunch, Ars Technica) |
| **standard** | 3 | 1.0x | General news sources, blogs, aggregators |
| **supplemental** | 4 (lowest) | 0.5x | Niche sources, community content (Reddit, forums) |

### How Priority Affects Serving

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     PRIORITY-BASED ARTICLE SERVING                      │
│                                                                         │
│   Use Case 1: "Headlines Only" (Hero/Top Section)                       │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  Filter: Priority = 'headline'                                  │   │
│   │  Result: Only articles from top-tier sources                    │   │
│   │  Example: BBC, Reuters, Associated Press                        │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│   Use Case 2: "Featured News" (Main Feed)                               │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  Filter: Priority IN ('headline', 'featured')                   │   │
│   │  Result: High-quality articles only                             │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│   Use Case 3: "All News" (Full Feed)                                    │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  Filter: None (all priorities)                                  │   │
│   │  Order: By recency, optionally weighted by boost factor         │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│   Use Case 4: "Mixed Feed" (Weighted Serving)                           │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  Algorithm: Score = recencyScore * boostFactor                  │   │
│   │  Result: Headlines appear higher even if slightly older         │   │
│   └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

### Example Feed Assignments

| Feed | Priority | Reasoning |
|------|----------|-----------|
| BBC News | `headline` | Major international news organization |
| Reuters | `headline` | Wire service, breaking news authority |
| TechCrunch | `featured` | Quality tech journalism |
| Ars Technica | `featured` | In-depth technical coverage |
| The Verge | `standard` | General tech news |
| Reddit r/technology | `supplemental` | Community aggregated, mixed quality |
| Personal blogs | `supplemental` | Niche content, lower priority |

### Configuration in feeds.json

Priority is defined per-feed with global definitions for boost factors:

```json
{
  "priorityDefinitions": {
    "headline": { "displayOrder": 1, "boostFactor": 2.0 },
    "featured": { "displayOrder": 2, "boostFactor": 1.5 },
    "standard": { "displayOrder": 3, "boostFactor": 1.0 },
    "supplemental": { "displayOrder": 4, "boostFactor": 0.5 }
  }
}
```

This allows adjusting boost factors without changing individual feed configurations.

---

## Feed Parser Customization

Different RSS/Atom feeds use varying conventions for fields like images, descriptions, and authors. The parser supports per-feed customization to handle these differences.

### Common Feed Variations

| Field | Standard Location | Common Variations |
|-------|-------------------|-------------------|
| **Image** | `<enclosure type="image/*">` | `<media:content>`, `<media:thumbnail>`, embedded in `<content:encoded>`, `<description>` HTML |
| **Description** | `<description>` | `<summary>`, `<content:encoded>`, `<content>` |
| **Author** | `<author>` | `<dc:creator>`, `<creator>` |
| **Date** | `<pubDate>` | `<published>`, `<updated>`, `<dc:date>` |
| **Categories** | `<category>` | Multiple `<category>` tags, `<dc:subject>` |

### Parser Configuration Options

```csharp
public class FeedParserConfig
{
    /// <summary>
    /// Image extraction strategy
    /// </summary>
    public ImageSource ImageSource { get; set; } = ImageSource.Auto;
    
    /// <summary>
    /// Custom regex for extracting image from HTML content (when ImageSource = ContentHtml)
    /// </summary>
    public string? ImageRegex { get; set; }
    
    /// <summary>
    /// Description extraction strategy
    /// </summary>
    public DescriptionSource DescriptionSource { get; set; } = DescriptionSource.Auto;
    
    /// <summary>
    /// Whether to strip HTML tags from description
    /// </summary>
    public bool StripHtml { get; set; } = true;
    
    /// <summary>
    /// Maximum description length (truncate if longer)
    /// </summary>
    public int MaxDescriptionLength { get; set; } = 500;
    
    /// <summary>
    /// Custom date format if non-standard
    /// </summary>
    public string? DateFormat { get; set; }
    
    /// <summary>
    /// XPath or property name for custom field extraction
    /// </summary>
    public Dictionary<string, string>? CustomMappings { get; set; }
}

public enum ImageSource
{
    Auto,           // Try all sources in order: enclosure → media:thumbnail → media:content → contentHtml
    Enclosure,      // Standard <enclosure type="image/*">
    MediaThumbnail, // <media:thumbnail url="...">
    MediaContent,   // <media:content url="..." medium="image">
    ContentHtml,    // Extract from HTML in <content:encoded> or <description> using regex
    None            // Skip image extraction
}

public enum DescriptionSource
{
    Auto,           // Try: description → summary → content (first non-empty)
    Description,    // <description> only
    Summary,        // <summary> only (Atom)
    Content,        // <content:encoded> or <content>
    ContentHtml     // <content:encoded> with HTML parsing
}
```

### Parser Strategy Pattern

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       FEED PARSER ARCHITECTURE                          │
│                                                                         │
│   ┌─────────────────┐                                                   │
│   │   FeedSource    │                                                   │
│   │   (feeds.json)  │                                                   │
│   └────────┬────────┘                                                   │
│            │                                                            │
│            ▼                                                            │
│   ┌─────────────────┐    ┌──────────────────────────────────────────┐  │
│   │  FeedParser     │    │  Parser Resolution:                      │  │
│   │  Service        │───▶│  1. Check parserConfig in feed           │  │
│   └────────┬────────┘    │  2. Fall back to parserDefaults          │  │
│            │             │  3. Use Auto detection                    │  │
│            │             └──────────────────────────────────────────┘  │
│            ▼                                                            │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │                    EXTRACTION STRATEGIES                         │  │
│   │                                                                  │  │
│   │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │  │
│   │  │  Image      │  │ Description │  │   Author    │             │  │
│   │  │  Extractor  │  │  Extractor  │  │  Extractor  │  ...        │  │
│   │  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘             │  │
│   │         │                │                │                     │  │
│   │         ▼                ▼                ▼                     │  │
│   │  ┌─────────────────────────────────────────────────────────┐   │  │
│   │  │ Auto Mode: Try strategies in order until success        │   │  │
│   │  │ 1. Enclosure → 2. MediaThumbnail → 3. MediaContent →   │   │  │
│   │  │ 4. ContentHtml → 5. null                                │   │  │
│   │  └─────────────────────────────────────────────────────────┘   │  │
│   └─────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

### Example: Handling Different Feeds

**Feed 1: TechCrunch (Standard RSS 2.0)**
```xml
<item>
  <title>Article Title</title>
  <description>Summary text...</description>
  <enclosure url="https://image.jpg" type="image/jpeg"/>
</item>
```
Config: `null` (use defaults, Auto detection works)

**Feed 2: BBC News (Media RSS)**
```xml
<item>
  <title>Article Title</title>
  <description>Summary...</description>
  <media:thumbnail url="https://bbc.com/image.jpg"/>
</item>
```
Config: `{ "imageSource": "mediaThumbnail" }`

**Feed 3: Reddit (Image in HTML content)**
```xml
<entry>
  <title>Article Title</title>
  <content type="html">
    &lt;img src="https://reddit.com/image.jpg"&gt; Some text...
  </content>
</entry>
```
Config: `{ "imageSource": "contentHtml", "imageRegex": "<img[^>]+src=\"([^\"]+)\"" }`

**Feed 4: Text-only Blog (No images)**
```xml
<item>
  <title>Article Title</title>
  <description>Full text content...</description>
</item>
```
Config: `{ "imageSource": "none" }` (skip image extraction, avoid errors)

### Directory Structure Update

```
FeedStackEngine/
├── src/
│   └── FeedStackEngine.Functions/
│       ├── Services/
│       │   ├── Parsing/
│       │   │   ├── IFeedParserService.cs
│       │   │   ├── FeedParserService.cs
│       │   │   ├── Extractors/
│       │   │   │   ├── IImageExtractor.cs
│       │   │   │   ├── EnclosureImageExtractor.cs
│       │   │   │   ├── MediaThumbnailExtractor.cs
│       │   │   │   ├── ContentHtmlImageExtractor.cs
│       │   │   │   ├── IDescriptionExtractor.cs
│       │   │   │   ├── StandardDescriptionExtractor.cs
│       │   │   │   └── ContentEncodedExtractor.cs
│       │   │   └── FeedParserFactory.cs
```

### Multi-Table Storage Design

To optimize for **fast latest news queries** while supporting **90-day retention with cold archival**, we use a multi-table architecture:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         TABLE STORAGE DESIGN                            │
│                                                                         │
│  ┌─────────────────────┐     90 days      ┌─────────────────────┐      │
│  │   NewsArticles      │ ───────────────▶ │   NewsArchive       │      │
│  │   (Hot Storage)     │   Daily Cleanup  │   (Cold Storage)    │      │
│  │                     │                  │                     │      │
│  │ • Latest 90 days    │                  │ • Articles > 90 days│      │
│  │ • Fast recency query│                  │ • Rarely accessed   │      │
│  │ • Inverted timestamp│                  │ • Standard partition│      │
│  └─────────────────────┘                  └─────────────────────┘      │
│                                                                         │
│  ┌─────────────────────┐                                               │
│  │   ArticleLookup     │  (Deduplication & cross-reference)            │
│  │   (Index Table)     │                                               │
│  └─────────────────────┘                                               │
└─────────────────────────────────────────────────────────────────────────┘
```

---

### Table 1: NewsArticles (Hot Storage - Latest 90 Days)

**Optimized for**: "Get latest news" queries, sorted by most recent first.

| Property | Type | Description |
|----------|------|-------------|
| **PartitionKey** | string | Date bucket: `YYYY-MM-DD` (e.g., "2025-12-21") |
| **RowKey** | string | `{InvertedTimestamp}_{FeedId}_{ArticleHash}` |
| **Title** | string | Article headline |
| **Link** | string | URL to full article |
| **Description** | string | Article summary/snippet |
| **PublishedDate** | DateTime | Original publish date |
| **FetchedDate** | DateTime | When we fetched it |
| **FeedId** | string | Source feed identifier |
| **FeedName** | string | Human-readable feed name |
| **Author** | string | Article author (if available) |
| **Category** | string | Feed category |
| **Country** | string | Country code (ISO 3166-1 alpha-2, e.g., "US", "GB") |
| **Priority** | string | Feed priority tier: "headline", "featured", "standard", "supplemental" |
| **ImageUrl** | string | Thumbnail/featured image (if available) |
| **ArticleHash** | string | MD5 hash of article URL (for dedup) |
| **Tags** | string | Comma-separated tags (e.g., "AI,Technology,Startups") - *future use* |

**Key Design Decisions:**

1. **PartitionKey = Date (`YYYY-MM-DD`)**
   - Enables efficient range queries: "Get all articles from today"
   - Easy cleanup: Delete entire partition when > 90 days old
   - Natural data distribution across partitions

2. **RowKey = `{InvertedTimestamp}_{FeedId}_{ArticleHash}`**
   - **Inverted Timestamp**: `(MAX_TICKS - publishedTimestamp)` ensures newest articles sort first
   - Within same partition, newest articles returned first
   - FeedId allows filtering within results
   - ArticleHash ensures uniqueness

**Example RowKey**: `2518570316799999999_techcrunch_a1b2c3d4`

```
Inverted Timestamp Calculation:
MAX_TICKS = 9999999999999 (or DateTime.MaxValue.Ticks)
InvertedTicks = MAX_TICKS - article.PublishedDate.Ticks
```

**Query Patterns:**

| Query | Implementation |
|-------|----------------|
| Latest 50 articles | Query today's partition, take 50 (already sorted newest first) |
| Latest from last 7 days | Query partitions: 2025-12-21, 2025-12-20, ... 2025-12-15 |
| Latest by category | Query + filter by Category property |
| Latest from specific feed | Query + filter by FeedId property |
| Latest by country | Query + filter by Country property |
| **Top headlines only** | Query + filter by `Priority eq 'headline'` |
| **Headlines + Featured** | Query + filter by `Priority eq 'headline' or Priority eq 'featured'` |

---

### Table 2: NewsArchive (Cold Storage - 90+ Days)

**Optimized for**: Long-term storage with **same schema as hot storage** for consistent querying.

| Property | Type | Description |
|----------|------|-------------|
| **PartitionKey** | string | Date bucket: `YYYY-MM-DD` (same as hot storage) |
| **RowKey** | string | `{InvertedTimestamp}_{FeedId}_{ArticleHash}` (same as hot storage) |
| **Title** | string | Article headline |
| **Link** | string | URL to full article |
| **Description** | string | Article summary/snippet |
| **PublishedDate** | DateTime | Original publish date |
| **FetchedDate** | DateTime | When we fetched it |
| **FeedId** | string | Source feed identifier |
| **FeedName** | string | Human-readable feed name |
| **Author** | string | Article author (if available) |
| **Category** | string | Feed category |
| **Country** | string | Country code (ISO 3166-1 alpha-2, e.g., "US", "GB") |
| **Priority** | string | Feed priority tier: "headline", "featured", "standard", "supplemental" |
| **ImageUrl** | string | Thumbnail/featured image (if available) |
| **ArticleHash** | string | MD5 hash of article URL |
| **ArchivedDate** | DateTime | When moved to cold storage |
| **Tags** | string | Comma-separated tags (e.g., "AI,Technology,Startups") - *future use* |

**Key Design Decisions:**
- **Same schema as NewsArticles** - enables identical query patterns for old stories
- **Same PartitionKey/RowKey strategy** - query by date, sorted by recency
- Can query historical data with same code/logic as hot storage
- Only difference: ArchivedDate property to track when archived

---

### Table 3: ArticleLookup (Deduplication Index)

**Purpose**: Fast duplicate detection AND single-article retrieval for future API.

| Property | Type | Description |
|----------|------|-------------|
| **PartitionKey** | string | `{FeedId}` |
| **RowKey** | string | `{ArticleHash}` (MD5 of URL) |
| **OriginalPartition** | string | Points to NewsArticles PartitionKey (date) |
| **OriginalRowKey** | string | Full RowKey for O(1) article fetch |
| **CreatedDate** | DateTime | First seen date |

**Usage:**
1. **Deduplication**: Before inserting, check if `{FeedId}/{ArticleHash}` exists
2. **Single Article API**: Lookup by `{FeedId}/{ArticleHash}` → get OriginalPartition + OriginalRowKey → fetch full article
3. If exists → skip (duplicate)
4. If not → insert into NewsArticles AND ArticleLookup

---

### Retention & Archival Process

```
┌─────────────────────────────────────────────────────────────────┐
│              DAILY ARCHIVAL FUNCTION (Timer: 2 AM UTC)          │
│                                                                 │
│   1. Calculate cutoff date: Today - 90 days                     │
│                     │                                           │
│                     ▼                                           │
│   2. Query NewsArticles partitions older than cutoff            │
│                     │                                           │
│                     ▼                                           │
│   3. Batch copy articles to NewsArchive                         │
│                     │                                           │
│                     ▼                                           │
│   4. Delete from NewsArticles (by partition - efficient!)       │
│                     │                                           │
│                     ▼                                           │
│   5. Update ArticleLookup with new location (or keep for dedup) │
│                     │                                           │
│                     ▼                                           │
│   6. Log archival summary                                       │
└─────────────────────────────────────────────────────────────────┘
```

**Benefits of Date-Partitioned Hot Storage:**
- **Fast cleanup**: Delete entire partition vs. scanning individual rows
- **Predictable performance**: Each day's data isolated
- **Easy debugging**: Query specific date ranges

---

## API Design

### Current Scope: No External API
This is a **backend aggregation engine** only. The initial scope focuses on:
- Timer-triggered data collection
- Storage in Azure Tables

### Future API Readiness Review

The following analysis ensures the current data model and architecture can support a comprehensive API layer without requiring schema migrations or redesign.

#### ✅ Supported Query Patterns

| Query Pattern | Data Model Support | Implementation |
|---------------|-------------------|----------------|
| **Latest articles** | ✅ Ready | Date PartitionKey + Inverted RowKey = newest first |
| **Paginate results** | ✅ Ready | Use continuation tokens from Table Storage |
| **Filter by category** | ✅ Ready | Category field indexed, filter on query |
| **Filter by country** | ✅ Ready | Country field indexed |
| **Filter by priority** | ✅ Ready | Priority field (headline/featured/standard) |
| **Filter by feed** | ✅ Ready | FeedId field indexed |
| **Filter by date range** | ✅ Ready | Query specific date partitions |
| **Get single article** | ⚠️ Needs design | See "Single Article Lookup" below |
| **Search by keyword** | ✅ Planned | Phase 6 - KeywordIndex table design ready |
| **Personalized feed** | ✅ Planned | Phase 6 - Tags + UserPreferences design ready |
| **List feeds** | ✅ Ready | Read from feeds.json config |
| **Feed statistics** | ⚠️ Needs design | See "Aggregation Stats" below |

#### ⚠️ Design Gap 1: Single Article Lookup

**Problem**: Current RowKey format `{InvertedTimestamp}_{FeedId}_{ArticleHash}` requires knowing the exact timestamp to fetch a single article.

**Solution**: Use ArticleLookup table (already designed) for O(1) article retrieval.

```
GET /api/articles/{feedId}/{articleHash}

1. Query ArticleLookup: PartitionKey={feedId}, RowKey={articleHash}
2. Get OriginalPartition from lookup result
3. Construct full RowKey and fetch from NewsArticles
```

**Enhancement needed**: Store full RowKey in ArticleLookup for direct lookup.

| Property | Type | Description |
|----------|------|-------------|
| **PartitionKey** | string | `{FeedId}` |
| **RowKey** | string | `{ArticleHash}` (MD5 of URL) |
| **OriginalPartition** | string | NewsArticles PartitionKey (date) |
| **OriginalRowKey** | string | **[NEW]** Full RowKey for direct fetch |
| **CreatedDate** | DateTime | First seen date |

#### ⚠️ Design Gap 2: Aggregation Statistics

**Problem**: No efficient way to get counts/stats without full table scans.

**Solution**: Add a FeedStats table for pre-computed statistics.

**Table: FeedStats** (Future - Phase 6)

| Property | Type | Description |
|----------|------|-------------|
| **PartitionKey** | string | `"stats"` (single partition for atomic reads) |
| **RowKey** | string | `{FeedId}` or `"global"` |
| **TotalArticles** | int | Cumulative article count |
| **ArticlesToday** | int | Articles fetched today |
| **LastFetchTime** | DateTime | Last successful fetch |
| **LastFetchCount** | int | Articles from last fetch |
| **FailureCount** | int | Consecutive failures |

**Stats API Endpoints:**
```
GET /api/stats              → Global statistics
GET /api/stats/{feedId}     → Per-feed statistics
GET /api/feeds              → Feed list with inline stats
```

**Update Strategy**: Increment counters during aggregation (minimal overhead).

#### ⚠️ Design Gap 3: Combined Hot + Cold Queries

**Problem**: Seamless querying across 90-day boundary (hot → cold tables).

**Solution**: Service layer abstraction that queries both tables transparently.

```csharp
public async Task<PagedResult<Article>> GetArticles(ArticleQuery query)
{
    var results = new List<Article>();
    
    // 1. Query hot storage first (NewsArticles)
    var hotResults = await QueryHotStorage(query);
    results.AddRange(hotResults);
    
    // 2. If date range extends beyond 90 days, also query cold storage
    if (query.StartDate < DateTime.UtcNow.AddDays(-90))
    {
        var coldResults = await QueryColdStorage(query);
        results.AddRange(coldResults);
    }
    
    // 3. Merge, sort, paginate
    return Paginate(results.OrderByDescending(a => a.PublishedDate), query.PageSize);
}
```

**No schema change needed** - both tables already use identical schema.

#### ✅ Design Gap 4: Efficient Pagination

**Status**: Already supported via Azure Table Storage continuation tokens.

```csharp
public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public string? ContinuationToken { get; set; }  // Opaque token for next page
    public bool HasMore => ContinuationToken != null;
}

// API: GET /api/articles?pageSize=20&continuationToken={token}
```

**Implementation Note**: Continuation tokens are partition-scoped. For cross-partition queries (multiple days), we need custom cursor logic:

```json
{
  "currentPartition": "2025-12-22",
  "tableToken": "base64-continuation-token",
  "partitionsRemaining": ["2025-12-21", "2025-12-20"]
}
```

#### ✅ Planned Future API Endpoints

| Endpoint | Method | Query Parameters | Description |
|----------|--------|------------------|-------------|
| `GET /api/articles` | GET | `pageSize`, `continuationToken`, `category`, `country`, `priority`, `feedId`, `startDate`, `endDate` | Paginated article list with filters |
| `GET /api/articles/{feedId}/{articleHash}` | GET | - | Single article by ID |
| `GET /api/articles/headlines` | GET | `pageSize`, `country` | Shortcut for priority=headline |
| `GET /api/articles/search` | GET | `q`, `pageSize` | Keyword search (Phase 6) |
| `GET /api/feeds` | GET | `category`, `country`, `enabled` | List configured feeds |
| `GET /api/feeds/{feedId}` | GET | - | Single feed details + stats |
| `GET /api/stats` | GET | - | Global aggregation statistics |
| `GET /api/stats/{feedId}` | GET | - | Per-feed statistics |
| `GET /api/health` | GET | - | Health check |
| `GET /api/categories` | GET | - | List available categories |
| `GET /api/countries` | GET | - | List available countries |

#### Response Models (Conceptual)

```csharp
// Article List Response
public class ArticleListResponse
{
    public List<ArticleDto> Articles { get; set; }
    public string? ContinuationToken { get; set; }
    public int TotalCount { get; set; }  // If available from stats
}

public class ArticleDto
{
    public string Id { get; set; }           // {feedId}_{articleHash}
    public string Title { get; set; }
    public string Description { get; set; }
    public string Link { get; set; }
    public string ImageUrl { get; set; }
    public DateTime PublishedDate { get; set; }
    public string FeedId { get; set; }
    public string FeedName { get; set; }
    public string Category { get; set; }
    public string Country { get; set; }
    public string Priority { get; set; }
    public string Author { get; set; }
    public List<string> Tags { get; set; }   // Split from comma-separated
}

// Feed List Response
public class FeedDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string Category { get; set; }
    public string Country { get; set; }
    public string Priority { get; set; }
    public bool Enabled { get; set; }
    public FeedStatsDto Stats { get; set; }  // Inline stats if available
}
```

### API Design Accommodations Summary

| Accommodation | Status | Action Needed |
|---------------|--------|---------------|
| Pagination support | ✅ Ready | Use continuation tokens |
| Multi-filter queries | ✅ Ready | All filter fields exist |
| Single article fetch | ⚠️ Enhancement | Add OriginalRowKey to ArticleLookup |
| Hot+Cold seamless query | ✅ Ready | Service layer handles both tables |
| Aggregation stats | ⚠️ Optional | Add FeedStats table in Phase 6 |
| Keyword search | ✅ Designed | KeywordIndex table ready for Phase 6 |
| Personalization | ✅ Designed | Tags + UserPreferences ready for Phase 6 |

### Recommended Schema Enhancement (Before Phase 1)

Add `OriginalRowKey` to ArticleLookup entity for efficient single-article API:

```csharp
public class ArticleLookupEntity : ITableEntity
{
    public string PartitionKey { get; set; }  // FeedId
    public string RowKey { get; set; }        // ArticleHash
    public string OriginalPartition { get; set; }
    public string OriginalRowKey { get; set; }  // [NEW] Full RowKey
    public DateTime CreatedDate { get; set; }
    // ... ITableEntity members
}
```

This single addition ensures O(1) article retrieval for future API without redesign.

---

## Implementation Plan

*Each step is designed to be small, testable, and buildable independently. Complete each step before moving to the next. Mark items complete as you progress.*

---

### Phase 1: Project Setup & Foundation

#### Step 1.1: Create Solution Structure
- [ ] Create new Azure Functions project (.NET 8 Isolated worker)
  ```
  func init FeedStackEngine.Functions --worker-runtime dotnet-isolated --target-framework net8.0
  ```
- [ ] Create solution file `FeedStackEngine.sln`
- [ ] Create test project `FeedStackEngine.Tests` (xUnit)
- [ ] Add project references between solution projects
- [ ] Verify solution builds successfully

#### Step 1.2: Configure Project Dependencies
- [ ] Add NuGet package: `Azure.Data.Tables`
- [ ] Add NuGet package: `CodeHollow.FeedReader`
- [ ] Add NuGet package: `Microsoft.Extensions.Http` (for HttpClientFactory)
- [ ] Add NuGet package: `Microsoft.Extensions.Options` (for configuration)
- [ ] Add test packages: `xUnit`, `Moq`, `FluentAssertions`
- [ ] Verify all packages restore successfully

#### Step 1.3: Set Up Project Structure
- [ ] Create folder structure:
  ```
  src/FeedStackEngine.Functions/
  ├── Functions/
  ├── Services/
  ├── Models/
  ├── Utilities/
  └── Config/
  ```
- [ ] Create `host.json` with appropriate settings
- [ ] Create `local.settings.json` with placeholder connection strings
- [ ] Add `local.settings.json` to `.gitignore`

#### Step 1.4: Configure Dependency Injection
- [ ] Set up `Program.cs` with HostBuilder
- [ ] Register IHttpClientFactory
- [ ] Register IConfiguration for settings
- [ ] Create placeholder service registrations (to be implemented)
- [ ] Verify application starts without errors

**Checkpoint 1**: Solution builds, runs, and shows "No functions found" (expected at this stage)

---

### Phase 2: Models & Configuration

#### Step 2.1: Create Feed Configuration Models
- [ ] Create `FeedSource.cs` model class
  - Properties: Id, Name, Url, Category, Country, Priority, Enabled
- [ ] Create `FeedParserConfig.cs` model class
  - Properties: ImageSource, ImageRegex, DescriptionSource, StripHtml, MaxDescriptionLength
- [ ] Create `FeedConfiguration.cs` root model
  - Properties: Feeds (list), Settings, ParserDefaults, PriorityDefinitions
- [ ] Create `PriorityDefinition.cs` model
  - Properties: DisplayOrder, Description, BoostFactor
- [ ] Write unit tests for model serialization/deserialization

#### Step 2.2: Create Table Entity Models
- [ ] Create `ArticleEntity.cs` implementing `ITableEntity`
  - All properties: PartitionKey, RowKey, Title, Link, Description, PublishedDate, FetchedDate, FeedId, FeedName, Author, Category, Country, Priority, ImageUrl, ArticleHash, Tags
- [ ] Create `ArticleLookupEntity.cs` implementing `ITableEntity`
  - Properties: PartitionKey (FeedId), RowKey (ArticleHash), OriginalPartition, OriginalRowKey, CreatedDate
- [ ] Write unit tests verifying entity property mappings

#### Step 2.3: Create Initial feeds.json
- [ ] Create `Config/feeds.json` with 3-5 sample feeds
- [ ] Include at least one `headline` priority feed
- [ ] Include at least one `standard` priority feed
- [ ] Include feeds from different countries (US, GB)
- [ ] Include at least one feed requiring custom parserConfig
- [ ] Set file to "Copy to Output Directory: Copy if newer"

#### Step 2.4: Create Feed Configuration Service
- [ ] Create `IFeedConfigService.cs` interface
  - Method: `Task<FeedConfiguration> GetConfigurationAsync()`
  - Method: `Task<IReadOnlyList<FeedSource>> GetEnabledFeedsAsync()`
- [ ] Create `FeedConfigService.cs` implementation
  - Load and deserialize feeds.json
  - Cache configuration in memory
  - Filter for enabled feeds only
- [ ] Register service in DI container
- [ ] Write unit tests with mock configuration

**Checkpoint 2**: Can load and parse feeds.json, unit tests pass

---

### Phase 3: Utility Classes

#### Step 3.1: Create Timestamp Helper
- [ ] Create `Utilities/TimestampHelper.cs`
- [ ] Implement `GetInvertedTimestamp(DateTime date)` method
  - Returns string that sorts newest first
- [ ] Implement `GetDatePartition(DateTime date)` method
  - Returns `YYYY-MM-DD` format string
- [ ] Implement `ParseInvertedTimestamp(string inverted)` method
  - Reverse operation for debugging/logging
- [ ] Write comprehensive unit tests for edge cases
  - Test with UTC dates
  - Test with min/max dates
  - Verify sort order is correct (newest first)

#### Step 3.2: Create Hash Helper
- [ ] Create `Utilities/HashHelper.cs`
- [ ] Implement `ComputeArticleHash(string url)` method
  - MD5 hash, return as lowercase hex string
  - Normalize URL before hashing (trim, lowercase)
- [ ] Implement `CreateRowKey(DateTime publishedDate, string feedId, string articleHash)` method
  - Combine inverted timestamp + feedId + hash
- [ ] Write unit tests verifying consistent hash output
  - Same URL always produces same hash
  - Different URLs produce different hashes

#### Step 3.3: Create HTML Utilities
- [ ] Create `Utilities/HtmlHelper.cs`
- [ ] Implement `StripHtmlTags(string html)` method
  - Remove all HTML tags, decode entities
- [ ] Implement `TruncateText(string text, int maxLength)` method
  - Truncate at word boundary, add ellipsis
- [ ] Implement `ExtractFirstImageUrl(string html)` method
  - Regex to find first <img src="...">
- [ ] Write unit tests with various HTML samples

**Checkpoint 3**: All utility classes complete with passing tests

---

### Phase 4: Table Storage Services

#### Step 4.1: Create Base Table Service
- [ ] Create `Services/ITableStorageService.cs` base interface
- [ ] Create `Services/TableStorageServiceBase.cs` abstract class
  - Inject TableServiceClient via constructor
  - Implement GetTableClient(tableName) helper
  - Handle table creation if not exists

#### Step 4.2: Create Article Table Service
- [ ] Create `Services/IArticleTableService.cs` interface
  - Method: `Task<bool> ArticleExistsAsync(string feedId, string articleHash)`
  - Method: `Task InsertArticleAsync(ArticleEntity article)`
  - Method: `Task InsertArticleBatchAsync(IEnumerable<ArticleEntity> articles)`
  - Method: `Task<IEnumerable<ArticleEntity>> GetArticlesByDateAsync(string datePartition, int? limit)`
- [ ] Create `Services/ArticleTableService.cs` implementation
  - Table name: `NewsArticles`
  - Implement all interface methods
  - Use batch operations for efficiency
- [ ] Register service in DI container
- [ ] Write integration tests (requires Azurite - see Step 4.5)

#### Step 4.3: Create Article Lookup Service
- [ ] Create `Services/IArticleLookupService.cs` interface
  - Method: `Task<bool> ExistsAsync(string feedId, string articleHash)`
  - Method: `Task CreateLookupAsync(ArticleLookupEntity lookup)`
  - Method: `Task<ArticleLookupEntity?> GetLookupAsync(string feedId, string articleHash)`
- [ ] Create `Services/ArticleLookupService.cs` implementation
  - Table name: `ArticleLookup`
  - Implement all interface methods
- [ ] Register service in DI container

#### Step 4.4: Create Combined Storage Service
- [ ] Create `Services/INewsStorageService.cs` interface
  - Method: `Task<SaveResult> SaveArticleAsync(ArticleEntity article)`
  - Returns: SaveResult { Saved, Duplicate, Error }
  - Method: `Task<SaveBatchResult> SaveArticlesBatchAsync(IEnumerable<ArticleEntity> articles)`
- [ ] Create `Services/NewsStorageService.cs` implementation
  - Inject IArticleTableService and IArticleLookupService
  - Check lookup first, then save to both tables
  - Handle duplicates gracefully
- [ ] Register service in DI container

#### Step 4.5: Set Up Local Testing with Azurite
- [ ] Install Azurite: `npm install -g azurite`
- [ ] Create script/instructions to start Azurite
- [ ] Update `local.settings.json` with Azurite connection string
  - `"AzureWebJobsStorage": "UseDevelopmentStorage=true"`
- [ ] Write integration test that:
  - Inserts an article
  - Verifies it exists in both tables
  - Attempts duplicate insert (should skip)
- [ ] Verify all integration tests pass with Azurite

**Checkpoint 4**: Can save articles to local Table Storage, deduplication works

---

### Phase 5: Feed Parser Service

#### Step 5.1: Create Parser Models
- [ ] Create `Models/ParsedArticle.cs`
  - Properties: Title, Link, Description, PublishedDate, Author, ImageUrl, Categories
  - Note: This is the intermediate model before converting to ArticleEntity
- [ ] Create `Models/FeedParseResult.cs`
  - Properties: FeedSource, Articles (list), Success, ErrorMessage, FetchDuration

#### Step 5.2: Create Basic Feed Parser
- [ ] Create `Services/IFeedParserService.cs` interface
  - Method: `Task<FeedParseResult> ParseFeedAsync(FeedSource feed)`
- [ ] Create `Services/FeedParserService.cs` implementation
  - Inject IHttpClientFactory
  - Use CodeHollow.FeedReader to fetch and parse
  - Extract basic fields: title, link, description, pubDate, author
  - Handle RSS 2.0 and Atom formats
- [ ] Register service in DI container
- [ ] Write unit test with mock HTTP response
- [ ] Write integration test with real RSS feed (e.g., BBC)

#### Step 5.3: Add Image Extraction
- [ ] Implement `ExtractImageUrl` method in FeedParserService
- [ ] Support extraction strategies:
  - Enclosure tag (RSS standard)
  - media:thumbnail (Media RSS)
  - media:content with medium="image"
  - First image from content HTML (fallback)
- [ ] Use feed's parserConfig.ImageSource to select strategy
- [ ] Default to "Auto" (try all in order)
- [ ] Write unit tests for each extraction strategy

#### Step 5.4: Add Description Processing
- [ ] Implement `ProcessDescription` method
- [ ] Strip HTML tags if parserConfig.StripHtml is true
- [ ] Truncate to parserConfig.MaxDescriptionLength
- [ ] Decode HTML entities
- [ ] Write unit tests with various HTML inputs

#### Step 5.5: Add Category/Tag Extraction
- [ ] Implement `ExtractCategories` method
- [ ] Collect all <category> tags from RSS item
- [ ] Combine with feed-level category
- [ ] Store as comma-separated string in Tags field
- [ ] Write unit tests

#### Step 5.6: Add Error Handling & Timeout
- [ ] Configure HttpClient timeout (30 seconds)
- [ ] Wrap feed fetch in try-catch
- [ ] Return FeedParseResult with Success=false on error
- [ ] Log errors with feed URL and exception details
- [ ] Test with invalid URL, timeout URL, malformed XML

**Checkpoint 5**: Can parse multiple RSS feeds, extract all fields, handle errors

---

### Phase 6: Article Mapping & Aggregation Logic

#### Step 6.1: Create Article Mapper
- [ ] Create `Services/IArticleMapper.cs` interface
  - Method: `ArticleEntity MapToEntity(ParsedArticle article, FeedSource feed)`
- [ ] Create `Services/ArticleMapper.cs` implementation
  - Set PartitionKey from PublishedDate (YYYY-MM-DD)
  - Generate RowKey using TimestampHelper + HashHelper
  - Copy all fields from ParsedArticle
  - Add feed metadata: FeedId, FeedName, Category, Country, Priority
- [ ] Register service in DI container
- [ ] Write unit tests verifying all mappings

#### Step 6.2: Create Aggregation Service
- [ ] Create `Services/IAggregationService.cs` interface
  - Method: `Task<AggregationResult> AggregateAllFeedsAsync()`
- [ ] Create `Models/AggregationResult.cs`
  - Properties: TotalFeeds, SuccessfulFeeds, FailedFeeds, NewArticles, Duplicates, Errors, Duration
- [ ] Create `Services/AggregationService.cs` implementation
  - Inject: IFeedConfigService, IFeedParserService, IArticleMapper, INewsStorageService
  - Load enabled feeds
  - Parse each feed (parallel with concurrency limit)
  - Map articles to entities
  - Save to storage (handles deduplication)
  - Collect and return statistics
- [ ] Register service in DI container

#### Step 6.3: Add Parallel Processing with Throttling
- [ ] Use `SemaphoreSlim` to limit concurrent feed fetches
- [ ] Read concurrency limit from settings (default: 10)
- [ ] Process feeds using `Task.WhenAll` with semaphore
- [ ] Ensure one slow/failing feed doesn't block others
- [ ] Write test verifying parallel execution

#### Step 6.4: Add Logging Throughout
- [ ] Inject `ILogger<T>` into all services
- [ ] Log at appropriate levels:
  - Information: Starting aggregation, feed completed, summary
  - Warning: Feed parse failed, but continuing
  - Error: Critical failures
- [ ] Include structured logging properties (feedId, articleCount, duration)

**Checkpoint 6**: Full aggregation pipeline works end-to-end locally

---

### Phase 7: Timer-Triggered Function

#### Step 7.1: Create NewsAggregator Function
- [ ] Create `Functions/NewsAggregatorFunction.cs`
- [ ] Add TimerTrigger attribute with CRON: `0 0 * * * *` (every hour)
- [ ] Inject IAggregationService
- [ ] Call AggregateAllFeedsAsync()
- [ ] Log aggregation results summary
- [ ] Handle and log any exceptions

#### Step 7.2: Test Locally with Manual Trigger
- [ ] Update CRON to `*/1 * * * * *` (every minute) for testing
- [ ] Start Azurite
- [ ] Run function locally: `func start`
- [ ] Verify function triggers
- [ ] Verify articles saved to Azurite tables
- [ ] Check logs for proper output
- [ ] Revert CRON to hourly after testing

#### Step 7.3: Add Function Configuration
- [ ] Read settings from `local.settings.json`:
  - `MaxConcurrentFeeds`
  - `FeedTimeoutSeconds`
  - `StorageConnectionString`
- [ ] Create `Options/AggregationOptions.cs` for typed configuration
- [ ] Register options in DI with `.Configure<AggregationOptions>()`
- [ ] Verify settings are applied

**Checkpoint 7**: Timer function works, aggregates feeds hourly, saves to storage

---

### Phase 8: Archival Function

#### Step 8.1: Create Archive Table Service
- [ ] Create `Services/IArchiveTableService.cs` interface
  - Method: `Task ArchiveArticleAsync(ArticleEntity article, DateTime archivedDate)`
  - Method: `Task ArchiveArticlesBatchAsync(IEnumerable<ArticleEntity> articles, DateTime archivedDate)`
- [ ] Create `Services/ArchiveTableService.cs` implementation
  - Table name: `NewsArchive`
  - Add ArchivedDate property when copying
  - Use batch operations for efficiency
- [ ] Register service in DI container

#### Step 8.2: Create Archival Service
- [ ] Create `Services/IArchivalService.cs` interface
  - Method: `Task<ArchivalResult> ArchiveOldArticlesAsync(int retentionDays)`
- [ ] Create `Models/ArchivalResult.cs`
  - Properties: PartitionsProcessed, ArticlesArchived, ArticlesDeleted, Errors, Duration
- [ ] Create `Services/ArchivalService.cs` implementation
  - Calculate cutoff date (today - retentionDays)
  - Query NewsArticles partitions older than cutoff
  - Copy articles to NewsArchive table
  - Delete articles from NewsArticles table
  - Return summary
- [ ] Register service in DI container

#### Step 8.3: Create ArticleArchiver Function
- [ ] Create `Functions/ArticleArchiverFunction.cs`
- [ ] Add TimerTrigger with CRON: `0 0 2 * * *` (daily at 2 AM UTC)
- [ ] Inject IArchivalService
- [ ] Read retentionDays from settings (default: 90)
- [ ] Call ArchiveOldArticlesAsync()
- [ ] Log archival results summary

#### Step 8.4: Test Archival Locally
- [ ] Insert test articles with old dates (>90 days ago) using a test script
- [ ] Run archival function manually
- [ ] Verify articles moved to NewsArchive table
- [ ] Verify articles deleted from NewsArticles table
- [ ] Verify ArticleLookup entries preserved

**Checkpoint 8**: Archival function works, moves old articles to cold storage

---

### Phase 9: Testing & Quality

#### Step 9.1: Unit Test Coverage
- [ ] Achieve 80%+ code coverage on Services
- [ ] Test all edge cases in utilities
- [ ] Test error handling paths
- [ ] Mock all external dependencies

#### Step 9.2: Integration Tests
- [ ] Test full aggregation with Azurite
- [ ] Test archival with Azurite
- [ ] Test with real RSS feeds (mark as integration, not unit)
- [ ] Test duplicate handling across multiple runs

#### Step 9.3: End-to-End Testing
- [ ] Run aggregator multiple times
- [ ] Verify no duplicate articles
- [ ] Verify article counts match expectations
- [ ] Test with feeds that fail (invalid URL)
- [ ] Verify failures don't block other feeds

**Checkpoint 9**: All tests pass, code is production-ready

---

### Phase 10: Azure Deployment

#### Step 10.1: Create Azure Resources
- [ ] Create Resource Group: `rg-feedstackengine`
- [ ] Create Storage Account: `stfeedstackengine`
  - SKU: Standard_LRS (cheapest)
  - Enable Table Storage
- [ ] Create Function App: `func-feedstackengine`
  - Runtime: .NET 8 Isolated
  - Plan: Consumption (serverless)
  - Link to Storage Account
- [ ] Note down connection strings

#### Step 10.2: Configure Application Settings
- [ ] Add `AzureWebJobsStorage` connection string
- [ ] Add `StorageConnectionString` for Table Storage
- [ ] Add `MaxConcurrentFeeds` setting
- [ ] Add `FeedTimeoutSeconds` setting
- [ ] Add `RetentionDays` setting

#### Step 10.3: Deploy Application
- [ ] Publish from Visual Studio / VS Code
  - OR use Azure CLI: `func azure functionapp publish func-feedstackengine`
- [ ] Verify deployment succeeded
- [ ] Check function app is running in Azure Portal

#### Step 10.4: Verify Production Operation
- [ ] Monitor first timer trigger (wait for next hour)
- [ ] Check Application Insights / Logs for execution
- [ ] Verify articles in Table Storage using Azure Storage Explorer
- [ ] Verify no errors in logs
- [ ] Monitor for 24 hours

**Checkpoint 10**: Application running in Azure, aggregating hourly

---

### Phase 11: Polish & Monitoring (Optional)

#### Step 11.1: Add Application Insights
- [ ] Enable Application Insights on Function App
- [ ] Add `Microsoft.ApplicationInsights.WorkerService` package
- [ ] Configure in Program.cs
- [ ] Add custom metrics for article counts

#### Step 11.2: Add Alerting
- [ ] Create alert for function failures
- [ ] Create alert for zero articles aggregated
- [ ] Configure email notifications

#### Step 11.3: Add Health Endpoint
- [ ] Create HTTP-triggered health check function
- [ ] Return storage connectivity status
- [ ] Return last aggregation timestamp

---

## Step Completion Tracking

| Phase | Steps | Description | Status |
|-------|-------|-------------|--------|
| 1 | 1.1 - 1.4 | Project Setup | ⬜ Not Started |
| 2 | 2.1 - 2.4 | Models & Configuration | ⬜ Not Started |
| 3 | 3.1 - 3.3 | Utility Classes | ⬜ Not Started |
| 4 | 4.1 - 4.5 | Table Storage Services | ⬜ Not Started |
| 5 | 5.1 - 5.6 | Feed Parser Service | ⬜ Not Started |
| 6 | 6.1 - 6.4 | Aggregation Logic | ⬜ Not Started |
| 7 | 7.1 - 7.3 | Timer Function | ⬜ Not Started |
| 8 | 8.1 - 8.4 | Archival Function | ⬜ Not Started |
| 9 | 9.1 - 9.3 | Testing & Quality | ⬜ Not Started |
| 10 | 10.1 - 10.4 | Azure Deployment | ⬜ Not Started |
| 11 | 11.1 - 11.3 | Monitoring (Optional) | ⬜ Not Started |

---

## Notes & Decisions

### Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| **Azure Functions over App Service** | Consumption plan is more cost-effective for hourly batch jobs |
| **Table Storage over Cosmos DB** | Table Storage is significantly cheaper; Cosmos DB overkill for simple key-value storage |
| **C# / .NET 8** | Strong Azure SDK support, enterprise-grade, excellent async patterns |
| **Embedded JSON config over Blob Storage** | Simpler deployment, version-controlled, no extra service complexity |
| **Date-based PartitionKey (hot)** | Enables fast "latest news" queries and efficient partition-level cleanup |
| **Inverted timestamp in RowKey** | Ensures newest articles sort first within each partition |
| **Separate hot/cold tables (same schema)** | Hot table stays small (<90 days); cold table uses identical schema for consistent querying |
| **ArticleLookup table for dedup** | O(1) duplicate check without scanning large tables |

### Scaling Considerations

| Feeds | Strategy |
|-------|----------|
| 8-10 feeds | Simple parallel fetch with Promise.all() |
| 50+ feeds | Batch into groups of 10-20, process sequentially |
| 100+ feeds | Use Azure Queue to fan-out, multiple function instances |

### Query Performance Estimates

| Query | Expected Performance |
|-------|---------------------|
| Latest 50 articles (today) | < 100ms (single partition query) |
| Latest 7 days | < 500ms (7 partition queries in parallel) |
| By category (today) | < 150ms (partition query + filter) |
| Dedup check | < 50ms (point query on ArticleLookup) |

### Configuration Summary

| Setting | Value |
|---------|-------|
| Aggregation frequency | Every hour (`0 0 * * * *`) |
| Archival frequency | Daily at 2 AM UTC (`0 0 2 * * *`) |
| Retention period | 90 days |
| Max concurrent feeds | 10 (configurable) |
| Initial feed count | 8-10 |
| Target feed count | 100+ |

### Open Questions
- [x] ~~How many feeds do we anticipate?~~ → 8-10 initially, 100+ later
- [x] ~~Retention policy?~~ → 90 days hot, then archive to cold storage
- [x] ~~Do we need full article content or just metadata?~~ → **Metadata only** (title, description, link)
- [x] ~~Should we store images locally or just reference URLs?~~ → **URLs only** (ImageUrl field stores link)
- [x] ~~Do we need to purge cold storage eventually?~~ → **Yes, after 1 year** (low priority feature)

### Data Storage Policy

| Aspect | Decision |
|--------|----------|
| **Article Content** | Metadata only - title, description/snippet, link to full article |
| **Images** | Store URL reference only (no local storage/blob) |
| **Hot Storage** | 90 days in NewsArticles table |
| **Cold Storage** | 90 days → 1 year in NewsArchive table |
| **Purge** | Delete from NewsArchive after 1 year (Phase 6 - low priority) |

---

## Future Architecture: Tag-Based Personalization

*This section outlines the design for tag-based article categorization and user preference-weighted serving. Implementation is Phase 6 (low priority), but the data model accommodates this from the start.*

### Concept Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    TAG-BASED PERSONALIZATION FLOW                       │
│                                                                         │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────────────┐     │
│   │   Article    │───▶│   Extract    │───▶│   Store in Articles  │     │
│   │   Fetched    │    │   Tags       │    │   (Tags field)       │     │
│   └──────────────┘    └──────────────┘    └──────────────────────┘     │
│                                                      │                  │
│                                                      ▼                  │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────────────┐     │
│   │   Weighted   │◀───│   Match User │◀───│   TagIndex Table     │     │
│   │   Results    │    │   Preferences│    │   (Fast tag lookup)  │     │
│   └──────────────┘    └──────────────┘    └──────────────────────┘     │
│                              ▲                                          │
│                              │                                          │
│                       ┌──────────────┐                                  │
│                       │ UserPrefs    │                                  │
│                       │ Table        │                                  │
│                       └──────────────┘                                  │
└─────────────────────────────────────────────────────────────────────────┘
```

### Tag Sources

| Source | Description |
|--------|-------------|
| **RSS Categories** | Extract `<category>` tags from RSS feed items |
| **Feed-level Tags** | Assign tags in feeds.json (e.g., TechCrunch → "Technology,Startups") |
| **Keyword Extraction** | Future: NLP-based keyword extraction from title/description |

### Additional Tables (Future)

**Table: TagIndex** (Efficient tag-based queries)

| Property | Type | Description |
|----------|------|-------------|
| **PartitionKey** | string | Tag name (e.g., "AI", "Technology") |
| **RowKey** | string | `{InvertedTimestamp}_{ArticleHash}` |
| **ArticlePartition** | string | Points to NewsArticles PartitionKey |
| **ArticleRowKey** | string | Points to NewsArticles RowKey |
| **Title** | string | Denormalized for quick display |

**Table: UserPreferences** (User tag weights)

| Property | Type | Description |
|----------|------|-------------|
| **PartitionKey** | string | UserId |
| **RowKey** | string | "preferences" |
| **TagWeights** | string | JSON: `{"AI": 0.8, "Sports": 0.2, "Technology": 0.9}` |
| **LastUpdated** | DateTime | When preferences were last modified |

### Weighted Serving Algorithm (Conceptual)

```csharp
// Pseudocode for weighted article serving
public async Task<List<Article>> GetPersonalizedFeed(string userId, int count)
{
    var userPrefs = await GetUserPreferences(userId);
    var recentArticles = await GetRecentArticles(days: 7);
    
    var scoredArticles = recentArticles.Select(article => new
    {
        Article = article,
        Score = CalculateScore(article.Tags, userPrefs.TagWeights)
    });
    
    return scoredArticles
        .OrderByDescending(x => x.Score)
        .ThenByDescending(x => x.Article.PublishedDate)
        .Take(count)
        .Select(x => x.Article)
        .ToList();
}

private double CalculateScore(string tags, Dictionary<string, double> weights)
{
    var tagList = tags?.Split(',') ?? Array.Empty<string>();
    return tagList.Sum(tag => weights.GetValueOrDefault(tag.Trim(), 0.5));
}
```

### Design Accommodations (Current Phase)

| Accommodation | Reason |
|---------------|--------|
| **Tags field in ArticleEntity** | Ready to store tags when extraction is implemented |
| **Category in feeds.json** | Can be used as initial tag source |
| **Same schema hot/cold** | Tags preserved during archival |

---

## Future Architecture: Keyword Search

*This section outlines the design for full-text keyword search across articles. Implementation is Phase 6 (low priority), but the current design accommodates future integration.*

### Concept Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      KEYWORD SEARCH ARCHITECTURE                        │
│                                                                         │
│   Option A: Azure Cognitive Search (Recommended for scale)              │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                                                                 │   │
│   │  NewsArticles ──▶ Azure Cognitive Search ◀── Search API        │   │
│   │     Table            (Index)                   Queries          │   │
│   │                                                                 │   │
│   │  • Full-text search with relevance scoring                      │   │
│   │  • Fuzzy matching, synonyms, stemming                           │   │
│   │  • Faceted search (by category, country, priority)              │   │
│   │  • ~$75/month for Basic tier (15M documents)                    │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│   Option B: In-Memory Search (Low cost, small scale)                    │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                                                                 │   │
│   │  NewsArticles ──▶ Load Recent ──▶ LINQ Filter ──▶ Results      │   │
│   │     Table          (7 days)        (Contains)                   │   │
│   │                                                                 │   │
│   │  • Simple string matching on Title + Description                │   │
│   │  • Works for small datasets (< 10K articles)                    │   │
│   │  • No additional Azure cost                                     │   │
│   │  • Limited: no relevance scoring, no fuzzy matching             │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│   Option C: Search Index Table (Medium complexity)                      │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                                                                 │   │
│   │  NewsArticles ──▶ KeywordIndex Table ◀── Keyword Queries       │   │
│   │                   (Inverted Index)                              │   │
│   │                                                                 │   │
│   │  • PartitionKey = keyword (normalized)                          │   │
│   │  • RowKey = articleId                                           │   │
│   │  • Build index during aggregation                               │   │
│   │  • Fast keyword lookup, no extra service cost                   │   │
│   └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

### Searchable Fields

| Field | Searchable | Notes |
|-------|------------|-------|
| **Title** | ✅ Yes | Primary search target, high weight |
| **Description** | ✅ Yes | Secondary search target |
| **FeedName** | ✅ Yes | Filter by source name |
| **Category** | ✅ Yes | Faceted filter |
| **Tags** | ✅ Yes | Faceted filter (future) |
| **Author** | ✅ Optional | Lower priority |

### Option C Detail: KeywordIndex Table (Self-Hosted)

If avoiding Azure Cognitive Search costs, a custom inverted index can be built:

**Table: KeywordIndex**

| Property | Type | Description |
|----------|------|-------------|
| **PartitionKey** | string | Normalized keyword (lowercase, stemmed) |
| **RowKey** | string | `{InvertedTimestamp}_{ArticleHash}` |
| **ArticlePartition** | string | Points to NewsArticles partition |
| **ArticleRowKey** | string | Points to NewsArticles row |
| **Title** | string | Denormalized for quick display |
| **FeedName** | string | Source name |

**Indexing Process (during aggregation):**
```csharp
// Pseudocode for keyword indexing
public async Task IndexArticle(ArticleEntity article)
{
    var keywords = ExtractKeywords(article.Title, article.Description);
    
    foreach (var keyword in keywords.Distinct())
    {
        var indexEntry = new KeywordIndexEntity
        {
            PartitionKey = NormalizeKeyword(keyword),  // "artificial" -> "artifici"
            RowKey = $"{article.InvertedTimestamp}_{article.ArticleHash}",
            ArticlePartition = article.PartitionKey,
            ArticleRowKey = article.RowKey,
            Title = article.Title,
            FeedName = article.FeedName
        };
        
        await _keywordIndexTable.UpsertEntityAsync(indexEntry);
    }
}

private IEnumerable<string> ExtractKeywords(string title, string description)
{
    var text = $"{title} {description}";
    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    
    return words
        .Where(w => w.Length >= 3)           // Skip short words
        .Where(w => !IsStopWord(w))          // Skip "the", "and", etc.
        .Select(w => w.ToLowerInvariant());  // Normalize case
}
```

**Search Query:**
```csharp
public async Task<List<ArticleSummary>> SearchByKeyword(string keyword, int limit = 50)
{
    var normalizedKeyword = NormalizeKeyword(keyword);
    
    // Query KeywordIndex partition
    var results = await _keywordIndexTable
        .QueryAsync<KeywordIndexEntity>(e => e.PartitionKey == normalizedKeyword)
        .Take(limit)
        .ToListAsync();
    
    return results.Select(r => new ArticleSummary
    {
        Title = r.Title,
        FeedName = r.FeedName,
        ArticlePartition = r.ArticlePartition,
        ArticleRowKey = r.ArticleRowKey
    }).ToList();
}
```

### Recommendation Path

| Scale | Recommendation | Monthly Cost |
|-------|----------------|--------------|
| < 5K articles | Option B (In-Memory LINQ) | $0 |
| 5K - 50K articles | Option C (KeywordIndex Table) | ~$1-2 (storage) |
| 50K+ articles | Option A (Azure Cognitive Search) | ~$75+ |

### Design Accommodations (Current Phase)

| Accommodation | Reason |
|---------------|--------|
| **Title/Description stored** | Searchable content already captured |
| **Normalized text fields** | Can add stemming/normalization later |
| **Consistent RowKey format** | Enables cross-referencing from index tables |
| **Date-partitioned data** | Can limit search to recent articles efficiently |

---

*Last Updated: December 22, 2025*
