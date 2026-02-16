using OpenAI.Embeddings;
using OperationsOneCentre.Domain.Common;
using OperationsOneCentre.Interfaces;
using OperationsOneCentre.Models;
using ClosedXML.Excel;

namespace OperationsOneCentre.Services;

/// <summary>
/// Service for searching context documents using vector embeddings
/// Includes Re-Ranking with Reciprocal Rank Fusion (RRF)
/// </summary>
public class ContextSearchService : IContextService
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly ContextStorageService _storageService;
    private readonly ILogger<ContextSearchService> _logger;
    
    private List<ContextDocument> _documents = new();
    private List<ContextFile> _files = new();
    private readonly AsyncInitializer _initializer = new();
    
    // RRF constant
    private const int RRF_K = AppConstants.Search.RrfConstant;

    public ContextSearchService(
        EmbeddingClient embeddingClient,
        ContextStorageService storageService,
        ILogger<ContextSearchService> logger)
    {
        _embeddingClient = embeddingClient;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the service by loading documents from storage
    /// </summary>
    public async Task InitializeAsync()
    {
        await _initializer.InitializeOnceAsync(async () =>
        {
            try
            {
                await _storageService.InitializeAsync();
                _documents = await _storageService.LoadDocumentsAsync();
                _files = await _storageService.LoadFilesAsync();
                
                // Log detailed statistics by category
                var categoryCounts = _documents.GroupBy(d => d.Category).Select(g => $"{g.Key}: {g.Count()}");
                _logger.LogInformation("Context search service initialized with {Count} documents from {FileCount} files. Categories: [{Categories}]", 
                    _documents.Count, _files.Count, string.Join(", ", categoryCounts));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize context search service");
                _documents = new List<ContextDocument>();
                _files = new List<ContextFile>();
            }
        });
    }

    /// <summary>
    /// Import an Excel file into the context
    /// </summary>
    public async Task<(int imported, string message)> ImportExcelAsync(Stream fileStream, string fileName, string category, string uploadedBy)
    {
        await InitializeAsync();

        try
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.First();
            
            // Get headers from first row
            var headers = new Dictionary<int, string>();
            var headerRow = worksheet.Row(1);
            var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;
            
            for (int col = 1; col <= lastColumn; col++)
            {
                var headerValue = headerRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(headerValue))
                {
                    headers[col] = headerValue;
                }
            }

            if (!headers.Any())
            {
                return (0, "No headers found in Excel file");
            }

            _logger.LogInformation("Found headers: {Headers}", string.Join(", ", headers.Values));

            // Map common column names
            var nameColumn = FindColumn(headers, "Name", "Title", "Nombre", "Titulo");
            var descColumn = FindColumn(headers, "Description", "Descripcion", "Desc", "Summary");
            var keywordsColumn = FindColumn(headers, "Keywords", "Tags", "Palabras clave", "Keywords/Tags");
            var linkColumn = FindColumn(headers, "Link", "URL", "Enlace", "Uri");

            // Parse rows
            var newDocuments = new List<ContextDocument>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            
            for (int row = 2; row <= lastRow; row++) // Start from row 2 (skip headers)
            {
                var rowData = worksheet.Row(row);
                
                // Skip empty rows
                if (rowData.IsEmpty()) continue;

                var doc = new ContextDocument
                {
                    SourceFile = fileName,
                    Category = category,
                    Name = GetCellValue(rowData, nameColumn),
                    Description = GetCellValue(rowData, descColumn),
                    Keywords = GetCellValue(rowData, keywordsColumn),
                    Link = GetCellValue(rowData, linkColumn),
                    ImportedAt = DateTime.UtcNow
                };

                // Store additional columns
                foreach (var kvp in headers)
                {
                    if (kvp.Key != nameColumn && kvp.Key != descColumn && 
                        kvp.Key != keywordsColumn && kvp.Key != linkColumn)
                    {
                        var value = GetCellValue(rowData, kvp.Key);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            doc.AdditionalData[kvp.Value] = value;
                        }
                    }
                }

                // Only add if there's meaningful content
                if (!string.IsNullOrWhiteSpace(doc.Name) || !string.IsNullOrWhiteSpace(doc.Description))
                {
                    newDocuments.Add(doc);
                }
            }

            if (!newDocuments.Any())
            {
                return (0, "No valid rows found in Excel file");
            }

            // Generate embeddings for new documents
            _logger.LogInformation("Generating embeddings for {Count} documents...", newDocuments.Count);
            
            foreach (var doc in newDocuments)
            {
                var searchText = doc.GetSearchableText();
                var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(searchText);
                doc.Embedding = embeddingResult.Value.ToFloats();
            }

            // Remove old documents from this file and add new ones
            _documents = _documents.Where(d => d.SourceFile != fileName).ToList();
            _documents.AddRange(newDocuments);

            // Update file metadata
            _files = _files.Where(f => f.FileName != fileName).ToList();
            _files.Add(new ContextFile
            {
                FileName = fileName,
                Category = category,
                EntryCount = newDocuments.Count,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = uploadedBy
            });

            // Save to storage
            await _storageService.SaveDocumentsAsync(_documents);
            await _storageService.SaveFilesAsync(_files);

            _logger.LogInformation("Successfully imported {Count} documents from {FileName}", newDocuments.Count, fileName);
            return (newDocuments.Count, $"Successfully imported {newDocuments.Count} entries from {fileName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Excel file {FileName}", fileName);
            return (0, $"Error importing file: {ex.Message}");
        }
    }

    /// <summary>
    /// Search context documents using hybrid search with Re-Ranking RRF
    /// (Retrieves more results and combines keyword+semantic rankings)
    /// </summary>
    public async Task<List<ContextDocument>> SearchAsync(string query, int topResults = 5)
    {
        await InitializeAsync();

        if (!_documents.Any())
        {
            _logger.LogWarning("No context documents loaded");
            return new List<ContextDocument>();
        }

        try
        {
            // === RE-RANKING RRF IMPLEMENTATION ===
            // Retrieve more results from each method, then combine with RRF
            const int retrieveCount = 20; // Retrieve more candidates
            
            // STEP 1: Keyword/exact match search with ranking
            var keywordRanked = SearchByKeywordRanked(query, retrieveCount);
            _logger.LogInformation("RRF - Keyword search for '{Query}' found {Count} matches", query, keywordRanked.Count);

            // STEP 2: Semantic search with ranking
            var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.Value.ToFloats();

            var semanticRanked = _documents
                .Select((doc, index) => new
                {
                    Document = doc,
                    Score = CosineSimilarity(queryVector, doc.Embedding),
                    OriginalIndex = index
                })
                .Where(x => x.Score > 0.15) // Lower threshold to get more candidates
                .OrderByDescending(x => x.Score)
                .Take(retrieveCount)
                .Select((x, rank) => new RankedResult { Document = x.Document, Rank = rank + 1, Score = x.Score })
                .ToList();
            
            _logger.LogInformation("RRF - Semantic search found {Count} matches", semanticRanked.Count);

            // STEP 3: Calculate RRF scores
            var rrfScores = new Dictionary<string, (ContextDocument Doc, double RrfScore, double KeywordScore, double SemanticScore)>();
            
            // Add keyword results with RRF score
            foreach (var item in keywordRanked)
            {
                var rrfScore = 1.0 / (RRF_K + item.Rank);
                if (!rrfScores.ContainsKey(item.Document.Id))
                {
                    rrfScores[item.Document.Id] = (item.Document, rrfScore, item.Score, 0.0);
                }
                else
                {
                    var existing = rrfScores[item.Document.Id];
                    rrfScores[item.Document.Id] = (existing.Doc, existing.RrfScore + rrfScore, item.Score, existing.SemanticScore);
                }
            }
            
            // Add semantic results with RRF score
            foreach (var item in semanticRanked)
            {
                var rrfScore = 1.0 / (RRF_K + item.Rank);
                if (!rrfScores.ContainsKey(item.Document.Id))
                {
                    rrfScores[item.Document.Id] = (item.Document, rrfScore, 0.0, item.Score);
                }
                else
                {
                    var existing = rrfScores[item.Document.Id];
                    rrfScores[item.Document.Id] = (existing.Doc, existing.RrfScore + rrfScore, existing.KeywordScore, item.Score);
                }
            }
            
            // STEP 4: Sort by combined RRF score
            var finalResults = rrfScores.Values
                .OrderByDescending(x => x.RrfScore)
                .Take(topResults)
                .Select(x =>
                {
                    // Normalize score to 0-1 range based on RRF
                    // Max possible RRF score is 2/(RRF_K+1) when ranked #1 in both
                    var maxRrf = 2.0 / (RRF_K + 1);
                    x.Doc.SearchScore = Math.Min(x.RrfScore / maxRrf, 1.0);
                    return x.Doc;
                })
                .ToList();

            // Log RRF results for debugging
            _logger.LogInformation("RRF combined search for '{Query}': Top results: {Results}", 
                query, 
                string.Join(", ", finalResults.Take(5).Select(r => $"{r.Name}:{r.SearchScore:F3}")));

            return finalResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching context documents");
            return new List<ContextDocument>();
        }
    }
    
    /// <summary>
    /// Helper class for ranked results
    /// </summary>
    private class RankedResult
    {
        public ContextDocument Document { get; set; } = null!;
        public int Rank { get; set; }
        public double Score { get; set; }
    }
    
    /// <summary>
    /// Search by keyword with ranking
    /// </summary>
    private List<RankedResult> SearchByKeywordRanked(string query, int maxResults)
    {
        var terms = query.Split(new[] { ' ', '?', '¿', '!', '¡', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2) // At least 2 characters
            .Select(t => t.ToLowerInvariant())
            .Where(t => !TextAnalysis.StopWords.Contains(t)) // Filter out stop words
            .ToList();

        _logger.LogInformation("SearchByKeywordRanked: Query='{Query}', Filtered terms: [{Terms}], Total docs: {DocCount}", 
            query, string.Join(", ", terms), _documents.Count);

        if (!terms.Any()) return new List<RankedResult>();

        // Score documents by keyword match quality
        var scoredMatches = _documents.Select(doc =>
        {
            var searchableText = $"{doc.Name} {doc.Description} {doc.Keywords}".ToLowerInvariant();
            var additionalText = string.Join(" ", doc.AdditionalData.Values).ToLowerInvariant();
            var fullText = $"{searchableText} {additionalText}";
            var nameText = doc.Name.ToLowerInvariant();
            
            // Calculate match score
            double score = 0;
            int matchedTerms = 0;
            
            foreach (var term in terms)
            {
                // Exact match in name gets highest score
                if (nameText.Contains(term))
                {
                    score += 2.0;
                    matchedTerms++;
                }
                // Match in keywords gets high score
                else if (doc.Keywords?.ToLowerInvariant().Contains(term) == true)
                {
                    score += 1.5;
                    matchedTerms++;
                }
                // Match in description or other fields
                else if (fullText.Contains(term))
                {
                    score += 1.0;
                    matchedTerms++;
                }
            }
            
            // Boost score based on percentage of terms matched
            if (terms.Count > 0 && matchedTerms > 0)
            {
                score *= (1.0 + (double)matchedTerms / terms.Count);
            }
            
            return new { Document = doc, Score = score };
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(maxResults)
        .Select((x, rank) => new RankedResult { Document = x.Document, Rank = rank + 1, Score = x.Score })
        .ToList();
        
        _logger.LogInformation("SearchByKeywordRanked: Found {Count} matches for terms [{Terms}]", 
            scoredMatches.Count, string.Join(", ", terms));
        
        return scoredMatches;
    }

    /// <summary>
    /// Get all context files
    /// </summary>
    public async Task<List<ContextFile>> GetFilesAsync()
    {
        await InitializeAsync();
        return _files;
    }

    /// <summary>
    /// Get documents by source file
    /// </summary>
    public async Task<List<ContextDocument>> GetDocumentsByFileAsync(string fileName)
    {
        await InitializeAsync();
        return _documents.Where(d => d.SourceFile == fileName).ToList();
    }

    /// <summary>
    /// Delete a context file and its documents
    /// </summary>
    public async Task DeleteFileAsync(string fileName)
    {
        await InitializeAsync();
        
        _documents = _documents.Where(d => d.SourceFile != fileName).ToList();
        _files = _files.Where(f => f.FileName != fileName).ToList();
        
        await _storageService.SaveDocumentsAsync(_documents);
        await _storageService.SaveFilesAsync(_files);
        
        _logger.LogInformation("Deleted context file {FileName}", fileName);
    }

    /// <summary>
    /// Get total document count
    /// </summary>
    public int GetDocumentCount() => _documents.Count;

    /// <summary>
    /// Get all unique categories
    /// </summary>
    public async Task<List<string>> GetCategoriesAsync()
    {
        await InitializeAsync();
        return _files.Select(f => f.Category).Distinct().ToList();
    }

    #region Helper Methods

    private int FindColumn(Dictionary<int, string> headers, params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var match = headers.FirstOrDefault(h => 
                h.Value.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                h.Value.Contains(name, StringComparison.OrdinalIgnoreCase));
            
            if (match.Key > 0)
                return match.Key;
        }
        return 0;
    }

    private string GetCellValue(IXLRow row, int column)
    {
        if (column <= 0) return string.Empty;
        return row.Cell(column).GetString().Trim();
    }

    // CosineSimilarity delegated to shared VectorMath (SIMD-accelerated)
    private static double CosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
        => VectorMath.CosineSimilarity(a, b);

    #endregion
}
