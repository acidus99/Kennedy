# Kennedy
Kennedy: Crawler and Search Engine for Gemini space. Leverages techniques and architecture from early WWW crawlers like Mercator, Archive.org, and GoogleBot

- **Kennedy.Crawler** - Crawler logic (Url Frontiers, Queues, etc)
- **Kennedy.CrawlData** - Models and storage systems for documents, meta data, and full text search
- **Kennedy.Server** - Gemini Server to handle queries and search results. Built on top of [RocketForce](https://github.com/acidus99/RocketForce), a .NET Gemini server and application framework
- **Kennedy.SearchConsole** - Console app for running FTS queries. Used for testing
