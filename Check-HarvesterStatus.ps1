# PowerShell Script to Check Jira Harvester Status
# This script checks Azure Blob Storage for harvested solutions

param(
    [string]$ConnectionString = $env:AZURE_STORAGE_CONNECTION_STRING,
    [switch]$Detailed
)

Write-Host "🔍 Checking Jira Solution Harvester Status..." -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrEmpty($ConnectionString)) {
    Write-Host "❌ ERROR: Azure Storage connection string not found!" -ForegroundColor Red
    Write-Host "Set environment variable: AZURE_STORAGE_CONNECTION_STRING" -ForegroundColor Yellow
    Write-Host "Or pass as parameter: -ConnectionString 'your_connection_string'" -ForegroundColor Yellow
    exit 1
}

# Install Azure Storage module if needed
if (-not (Get-Module -ListAvailable -Name Az.Storage)) {
    Write-Host "📦 Installing Az.Storage module..." -ForegroundColor Yellow
    Install-Module -Name Az.Storage -Scope CurrentUser -Force -AllowClobber
}

Import-Module Az.Storage

try {
    # Parse connection string to get account info
    $accountName = [regex]::Match($ConnectionString, "AccountName=([^;]+)").Groups[1].Value
    $accountKey = [regex]::Match($ConnectionString, "AccountKey=([^;]+)").Groups[1].Value
    
    Write-Host "📦 Storage Account: $accountName" -ForegroundColor Green
    Write-Host ""
    
    # Create context
    $ctx = New-AzStorageContext -StorageAccountName $accountName -StorageAccountKey $accountKey
    
    # Check harvested-solutions container
    $containerName = "harvested-solutions"
    $container = Get-AzStorageContainer -Name $containerName -Context $ctx -ErrorAction SilentlyContinue
    
    if ($null -eq $container) {
        Write-Host "⚠️  Container '$containerName' does not exist yet" -ForegroundColor Yellow
        Write-Host "   This is normal if the harvester hasn't run yet" -ForegroundColor Gray
    }
    else {
        Write-Host "✅ Container '$containerName' exists" -ForegroundColor Green
        
        # List blobs in container
        $blobs = Get-AzStorageBlob -Container $containerName -Context $ctx
        
        Write-Host ""
        Write-Host "📄 Files in container:" -ForegroundColor Cyan
        Write-Host ""
        
        foreach ($blob in $blobs) {
            $sizeKB = [math]::Round($blob.Length / 1KB, 2)
            $sizeMB = [math]::Round($blob.Length / 1MB, 2)
            $size = if ($sizeMB -gt 1) { "$sizeMB MB" } else { "$sizeKB KB" }
            
            Write-Host "  📋 $($blob.Name)" -ForegroundColor White
            Write-Host "     Size: $size" -ForegroundColor Gray
            Write-Host "     Last Modified: $($blob.LastModified.LocalDateTime)" -ForegroundColor Gray
            
            # Download and analyze key files
            if ($Detailed -and $blob.Name -match "jira-solutions.*\.json") {
                Write-Host "     Downloading for analysis..." -ForegroundColor Yellow
                
                $tempFile = [System.IO.Path]::GetTempFileName()
                Get-AzStorageBlobContent -Container $containerName -Blob $blob.Name -Destination $tempFile -Context $ctx -Force | Out-Null
                
                $content = Get-Content $tempFile -Raw | ConvertFrom-Json
                
                if ($content -is [Array]) {
                    Write-Host "     ✅ Contains $($content.Count) solutions" -ForegroundColor Green
                    
                    # Show recent solutions
                    $recent = $content | Sort-Object -Property HarvestedDate -Descending | Select-Object -First 5
                    Write-Host ""
                    Write-Host "     📊 Recent Solutions:" -ForegroundColor Cyan
                    foreach ($sol in $recent) {
                        Write-Host "        • $($sol.TicketId): $($sol.TicketTitle)" -ForegroundColor White
                        Write-Host "          System: $($sol.System) | Category: $($sol.Category)" -ForegroundColor Gray
                        Write-Host "          Harvested: $($sol.HarvestedDate)" -ForegroundColor Gray
                    }
                }
                else {
                    Write-Host "     ⚠️  Unexpected format" -ForegroundColor Yellow
                }
                
                Remove-Item $tempFile -Force
            }
            
            Write-Host ""
        }
        
        # Check specific key files
        Write-Host ""
        Write-Host "🔍 Checking key status files:" -ForegroundColor Cyan
        Write-Host ""
        
        $keyFiles = @(
            "jira-solutions-with-embeddings.json",
            "harvested-tickets.json",
            "harvester-run-history.json"
        )
        
        foreach ($fileName in $keyFiles) {
            $blob = $blobs | Where-Object { $_.Name -eq $fileName }
            if ($blob) {
                Write-Host "  ✅ $fileName" -ForegroundColor Green
                Write-Host "     Last updated: $($blob.LastModified.LocalDateTime)" -ForegroundColor Gray
            }
            else {
                Write-Host "  ⚠️  $fileName - Not found (harvester may not have run yet)" -ForegroundColor Yellow
            }
        }
    }
    
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "💡 Tips:" -ForegroundColor Cyan
    Write-Host "  • Harvester runs every 6 hours automatically" -ForegroundColor Gray
    Write-Host "  • Check the Monitoring dashboard at /monitoring" -ForegroundColor Gray
    Write-Host "  • Use -Detailed flag to see solution details" -ForegroundColor Gray
    Write-Host "  • Check Azure App Service logs for detailed execution info" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Verify connection string is correct" -ForegroundColor Gray
    Write-Host "  2. Check network connectivity to Azure" -ForegroundColor Gray
    Write-Host "  3. Ensure storage account exists and is accessible" -ForegroundColor Gray
}
