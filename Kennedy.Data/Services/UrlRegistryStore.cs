using System;
using System.Threading;
using System.Threading.Tasks;
using Gemini.Net;
using Kennedy.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kennedy.Data.Services
{
    /// <summary>
    /// URL Registry persistence + rules.
    /// Internally batches SaveChanges/transactions so callers don't need to know.
    /// </summary>
    public sealed class UrlRegistryStore : IAsyncDisposable
    {
        private readonly IDbContextFactory<KennedyDbContext> _dbFactory;


        // Batch configuration
        public int BatchSize { get; }

        // Batch state
        private KennedyDbContext? _db;
        private IDbContextTransaction? _tx;
        private int _pendingChanges;
        private Dictionary<string, UrlRecord>? _batchUrlCache;


        // Prevent concurrent calls from interleaving batch state
        private readonly SemaphoreSlim _gate = new(1, 1);

        public UrlRegistryStore(IDbContextFactory<KennedyDbContext> dbFactory, int batchSize = 1000)
        {
            _dbFactory = dbFactory;
            BatchSize = batchSize <= 0 ? 1 : batchSize;
        }

        public async Task<UrlRecord> AddOrUpdateAsync(
            string normalizedUrl,
            int? lastStatusCode,
            string? contentHash,
            DateTime visitTimeUtc,
            string meta,
            CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                await EnsureBatchAsync(ct);

                // Fast O(1) in-batch lookup
                if (_batchUrlCache!.TryGetValue(normalizedUrl, out var url))
                {
                    // already tracked this batch
                }
                else
                {
                    // DB lookup only if not seen in batch
                    url = await _db!.UrlRegistry
                        .SingleOrDefaultAsync(u => u.NormalizedUrl == normalizedUrl, ct);

                    if (url == null)
                    {
                        url = new UrlRecord(normalizedUrl)
                        {
                            FirstSeen = visitTimeUtc,
                            Status = UrlStatus.New
                        };

                        _db.UrlRegistry.Add(url);
                    }

                    _batchUrlCache[normalizedUrl] = url;
                }

                // Always update last visit info
                url.LastVisit = visitTimeUtc;
                url.LastStatusCode = lastStatusCode;

                if (lastStatusCode.HasValue)
                {
                    if (GeminiParser.IsSuccessStatus(lastStatusCode.Value))
                    {
                        url.LastSuccess = visitTimeUtc;
                        url.SuccessCount++;
                        url.Meta = "";

                        if (contentHash != null)
                        {
                            if (url.LastContentHash == null ||
                                !String.Equals(url.LastContentHash, contentHash, StringComparison.Ordinal))
                            {
                                url.LastContentHash = contentHash;
                                url.LastContentChange = visitTimeUtc;
                            }
                        }

                        url.Status = UrlStatus.Active;
                    }
                    else if (GeminiParser.ConnectionErrorStatusCode == lastStatusCode.Value)
                    {
                        url.FailureCount++;
                        url.Status = UrlStatus.ConnectionError;
                        url.Meta = "";
                    }
                    else if (GeminiParser.IsTempFailStatus(lastStatusCode.Value) || GeminiParser.IsPermFailStatus(lastStatusCode.Value))
                    {
                        url.FailureCount++;
                        url.Status = UrlStatus.TemporaryError;
                        url.Meta = "";
                    }
                    else if (GeminiParser.IsRedirectStatus(lastStatusCode.Value))
                    {
                        url.LastSuccess = visitTimeUtc;
                        url.SuccessCount++;
                        url.Meta = meta;
                        url.Status = UrlStatus.Redirect;
                    }
                    else if (GeminiParser.IsInputStatus(lastStatusCode.Value))
                    {
                        url.LastSuccess = visitTimeUtc;
                        url.SuccessCount++;
                        url.Meta = meta;
                        url.Status = UrlStatus.Interactive;
                    }
                    else
                    {
                        url.Status = UrlStatus.UNKNOWN;
                        url.Meta = "";
                    }
                }
                else
                {
                    url.Status = UrlStatus.UNKNOWN;
                }

                _pendingChanges++;

                if (_pendingChanges >= BatchSize)
                {
                    await FlushInternalAsync(ct);
                }

                return url;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task EnsureBatchAsync(CancellationToken ct)
        {
            if (_db != null && _tx != null)
                return;

            _db = await _dbFactory.CreateDbContextAsync(ct);

            // For bulk-ish ingestion, WAL generally performs better.
            // Safe to run each time; SQLite will just return current mode.
            await _db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);

            // Keep default durability unless you decide to relax it during ingest.
            // await _db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", ct);

            _tx = await _db.Database.BeginTransactionAsync(ct);
            _pendingChanges = 0;
            _batchUrlCache = new Dictionary<string, UrlRecord>();
        }

        private async Task FlushInternalAsync(CancellationToken ct)
        {
            if (_db == null || _tx == null)
                return;

            await _db.SaveChangesAsync(ct);
            await _tx.CommitAsync(ct);

            await _tx.DisposeAsync();
            _tx = null;

            // Important: don't let the change tracker grow forever in a long ingest.
            _db.ChangeTracker.Clear();

            await _db.DisposeAsync();
            _db = null;

            _pendingChanges = 0;
            _batchUrlCache = null;
        }

        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync(CancellationToken.None);
            try
            {
                // Flush any partial batch on shutdown
                await FlushInternalAsync(CancellationToken.None);
            }
            finally
            {
                _gate.Release();
                _gate.Dispose();
            }
        }
    }
}
