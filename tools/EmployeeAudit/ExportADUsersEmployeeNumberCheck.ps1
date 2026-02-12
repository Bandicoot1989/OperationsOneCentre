#Requires -Version 5.1
#Requires -Modules ActiveDirectory

<#
.SYNOPSIS
    Checks AD users for missing, empty, or invalid employee numbers.

.DESCRIPTION
    This script processes multiple plant locations and identifies users with:
    - Missing or empty employee numbers
    - Invalid format (non-numeric values like "SHARED", text, etc.)
    No SAP validation required.

.PARAMETER LogLevel
    Logging verbosity: Error, Warning, Info, Debug. Defaults to Info.

.EXAMPLE
    .\ExportADUsersEmployeeNumberCheck.ps1 -LogLevel Debug

.NOTES
    Author: IT Security Team
    Version: 1.0
    Last Modified: 2026-02-12
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Error', 'Warning', 'Info', 'Debug')]
    [string]$LogLevel = 'Info'
)

#region Configuration
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ScriptRoot = $PSScriptRoot
if (-not $ScriptRoot) {
    $ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$Config = @{
    BasePath           = $ScriptRoot
    PlantsFile         = Join-Path $ScriptRoot "Tarea Correccion de Usuarios\Plants.txt"
    OutputFolder       = Join-Path $ScriptRoot "Tarea Correccion de Usuarios"
    LogFolder          = Join-Path $ScriptRoot "Logs"
    DomainSuffix       = "DC=grupoantolin,DC=com"
    MaxRetries         = 3
    RetryDelaySeconds  = 5
    # Invalid patterns to detect - case insensitive
    InvalidPatterns    = @(
        'SHARED',
        'EXTERNAL',
        'SERVICE',
        'ADMIN',
        'TEST',
        'TEMP',
        'GENERIC',
        'N/A',
        'NA',
        'NONE',
        'TBD',
        'PENDING'
    )
}
#endregion

#region Logging Functions
function Write-Log {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Message,
        
        [Parameter()]
        [ValidateSet('Error', 'Warning', 'Info', 'Debug')]
        [string]$Level = 'Info',
        
        [Parameter()]
        [string]$Plant = ''
    )
    
    $LogLevels = @{ 'Error' = 1; 'Warning' = 2; 'Info' = 3; 'Debug' = 4 }
    
    if ($LogLevels[$Level] -le $LogLevels[$script:LogLevel]) {
        $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $PlantInfo = if ($Plant) { "[$Plant] " } else { "" }
        $LogEntry = "[$Timestamp] [$Level] $PlantInfo$Message"
        
        switch ($Level) {
            'Error'   { Write-Host $LogEntry -ForegroundColor Red }
            'Warning' { Write-Host $LogEntry -ForegroundColor Yellow }
            'Info'    { Write-Host $LogEntry -ForegroundColor White }
            'Debug'   { Write-Host $LogEntry -ForegroundColor Gray }
        }
        
        $LogFile = Join-Path $Config.LogFolder "EmployeeNumberCheck_$(Get-Date -Format 'yyyyMMdd').log"
        Add-Content -Path $LogFile -Value $LogEntry -Encoding UTF8
    }
}

function Initialize-Logging {
    if (-not (Test-Path $Config.LogFolder)) {
        New-Item -Path $Config.LogFolder -ItemType Directory -Force | Out-Null
    }
    Write-Log "========== Employee Number Check Started ==========" -Level Info
    Write-Log "Running as: $($env:USERNAME)@$($env:USERDOMAIN)" -Level Info
}
#endregion

#region Validation Functions
function Test-EmployeeNumber {
    <#
    .SYNOPSIS
        Validates an employee number format.
    .RETURNS
        Hashtable with IsValid, IsMissing, IsInvalidFormat, and Reason
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$EmployeeNumber
    )
    
    $result = @{
        IsValid = $false
        IsMissing = $false
        IsInvalidFormat = $false
        Reason = ''
    }
    
    # Check for missing/empty
    if ([string]::IsNullOrWhiteSpace($EmployeeNumber)) {
        $result.IsMissing = $true
        $result.Reason = 'Empty or null'
        return $result
    }
    
    $cleaned = $EmployeeNumber.Trim().Trim('"')
    
    # Check for empty after cleanup
    if ([string]::IsNullOrWhiteSpace($cleaned) -or $cleaned -eq '0' -or $cleaned -eq '""') {
        $result.IsMissing = $true
        $result.Reason = 'Empty or zero'
        return $result
    }
    
    # Check for known invalid patterns (SHARED, EXTERNAL, etc.)
    foreach ($pattern in $Config.InvalidPatterns) {
        if ($cleaned -match "(?i)^$pattern$|(?i)$pattern") {
            $result.IsInvalidFormat = $true
            $result.Reason = "Contains invalid keyword: $pattern"
            return $result
        }
    }
    
    # Check if numeric (valid employee number should be numeric)
    if ($cleaned -notmatch '^\d+$') {
        $result.IsInvalidFormat = $true
        $result.Reason = "Non-numeric value: $cleaned"
        return $result
    }
    
    # Valid
    $result.IsValid = $true
    return $result
}

function Test-PlantName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$PlantName
    )
    
    if ($PlantName -notmatch '^[a-zA-Z0-9_-]+$') {
        return $false
    }
    if ($PlantName -match '\.\.') {
        return $false
    }
    return $true
}
#endregion

#region AD Query Functions
function Get-ADUsersFromOU {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SearchBase,
        
        [Parameter(Mandatory)]
        [string]$Plant
    )
    
    $Users = @()
    $Attempt = 0
    
    while ($Attempt -lt $Config.MaxRetries) {
        $Attempt++
        try {
            if (-not (Get-ADOrganizationalUnit -Identity $SearchBase -ErrorAction SilentlyContinue)) {
                return @()
            }
            
            $Users = @(Get-ADUser -Filter { Enabled -eq $true -and Surname -like '*' } `
                -SearchBase $SearchBase `
                -Properties EmployeeNumber, SamAccountName, EmailAddress, DisplayName, WhenCreated `
                -ErrorAction Stop |
                Select-Object @{N='EmployeeNumber';E={$_.EmployeeNumber}},
                              @{N='SamAccountName';E={$_.SamAccountName}},
                              @{N='EmailAddress';E={$_.EmailAddress}},
                              @{N='DisplayName';E={$_.DisplayName}},
                              @{N='WhenCreated';E={$_.WhenCreated}})
            
            Write-Log "Retrieved $($Users.Count) users from $SearchBase" -Level Debug -Plant $Plant
            return ,$Users
        }
        catch [Microsoft.ActiveDirectory.Management.ADIdentityNotFoundException] {
            return @()
        }
        catch {
            Write-Log "Attempt $Attempt failed for $SearchBase`: $($_.Exception.Message)" -Level Warning -Plant $Plant
            if ($Attempt -lt $Config.MaxRetries) {
                Start-Sleep -Seconds $Config.RetryDelaySeconds
            }
        }
    }
    
    return @()
}
#endregion

#region Processing Functions
function Process-Plant {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Plant
    )
    
    $PlantOutputFolder = Join-Path $Config.OutputFolder $Plant
    $ReportPath = Join-Path $PlantOutputFolder "EmployeeNumberReport.txt"
    
    # Collect users from both OUs
    $AllUsers = @()
    
    $UsersOU = "OU=users,OU=$Plant,$($Config.DomainSuffix)"
    $AllUsers += Get-ADUsersFromOU -SearchBase $UsersOU -Plant $Plant
    
    $ExternalOU = "OU=external,OU=$Plant,$($Config.DomainSuffix)"
    $AllUsers += Get-ADUsersFromOU -SearchBase $ExternalOU -Plant $Plant
    
    if ($AllUsers.Count -eq 0) {
        Write-Log "No users found" -Level Warning -Plant $Plant
        return [PSCustomObject]@{
            Plant = $Plant
            TotalUsers = 0
            MissingCount = 0
            InvalidFormatCount = 0
            Missing = @()
            InvalidFormat = @()
        }
    }
    
    # Validate each user
    $missingList = @()
    $invalidFormatList = @()
    
    foreach ($user in $AllUsers) {
        $validation = Test-EmployeeNumber -EmployeeNumber $user.EmployeeNumber
        
        if ($validation.IsMissing) {
            $missingList += [PSCustomObject]@{
                SamAccountName = $user.SamAccountName
                EmailAddress = $user.EmailAddress
                DisplayName = $user.DisplayName
                EmployeeNumber = $user.EmployeeNumber
                Reason = $validation.Reason
            }
        }
        elseif ($validation.IsInvalidFormat) {
            $invalidFormatList += [PSCustomObject]@{
                SamAccountName = $user.SamAccountName
                EmailAddress = $user.EmailAddress
                DisplayName = $user.DisplayName
                EmployeeNumber = $user.EmployeeNumber
                Reason = $validation.Reason
            }
        }
    }
    
    # Generate report
    if (-not (Test-Path $PlantOutputFolder)) {
        New-Item -Path $PlantOutputFolder -ItemType Directory -Force | Out-Null
    }
    
    $report = Generate-PlantReport -Plant $Plant -TotalUsers $AllUsers.Count `
        -MissingList $missingList -InvalidFormatList $invalidFormatList
    
    $report | Out-File -FilePath $ReportPath -Encoding UTF8
    
    Write-Log "Total: $($AllUsers.Count), Missing: $($missingList.Count), Invalid: $($invalidFormatList.Count)" -Level Info -Plant $Plant
    
    return [PSCustomObject]@{
        Plant = $Plant
        TotalUsers = $AllUsers.Count
        MissingCount = $missingList.Count
        InvalidFormatCount = $invalidFormatList.Count
        Missing = $missingList
        InvalidFormat = $invalidFormatList
    }
}

function Generate-PlantReport {
    [CmdletBinding()]
    param(
        [string]$Plant,
        [int]$TotalUsers,
        [array]$MissingList,
        [array]$InvalidFormatList
    )
    
    $sb = [System.Text.StringBuilder]::new()
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    
    [void]$sb.AppendLine("Employee Number Validation Report")
    [void]$sb.AppendLine("=================================")
    [void]$sb.AppendLine("Plant: $Plant")
    [void]$sb.AppendLine("Generated: $timestamp")
    [void]$sb.AppendLine("Total AD Users Analyzed: $TotalUsers")
    [void]$sb.AppendLine("")
    
    # Section 1: Missing or Empty Employee Numbers
    if ($MissingList.Count -gt 0) {
        [void]$sb.AppendLine("Users with Missing or Empty Employee Numbers:")
        [void]$sb.AppendLine("---------------------------------------------")
        [void]$sb.AppendLine('"SamAccountName", "DisplayName", "EmailAddress", "Reason"')
        
        foreach ($user in $MissingList) {
            [void]$sb.AppendLine("`"$($user.SamAccountName)`", `"$($user.DisplayName)`", `"$($user.EmailAddress)`", `"$($user.Reason)`"")
        }
        [void]$sb.AppendLine("")
    }
    
    # Section 2: Invalid Format (SHARED, text, etc.)
    if ($InvalidFormatList.Count -gt 0) {
        [void]$sb.AppendLine("Users with Invalid Employee Number Format (SHARED, text, etc.):")
        [void]$sb.AppendLine("---------------------------------------------------------------")
        [void]$sb.AppendLine('"EmployeeNumber", "SamAccountName", "DisplayName", "EmailAddress", "Reason"')
        
        foreach ($user in $InvalidFormatList) {
            [void]$sb.AppendLine("`"$($user.EmployeeNumber)`", `"$($user.SamAccountName)`", `"$($user.DisplayName)`", `"$($user.EmailAddress)`", `"$($user.Reason)`"")
        }
        [void]$sb.AppendLine("")
    }
    
    # Summary
    $totalIssues = $MissingList.Count + $InvalidFormatList.Count
    
    if ($totalIssues -eq 0) {
        [void]$sb.AppendLine("All users have valid employee numbers.")
    }
    else {
        [void]$sb.AppendLine("Summary:")
        [void]$sb.AppendLine("--------")
        [void]$sb.AppendLine("- Missing/Empty Employee Numbers: $($MissingList.Count)")
        [void]$sb.AppendLine("- Invalid Format (SHARED, text, etc.): $($InvalidFormatList.Count)")
        [void]$sb.AppendLine("- TOTAL ISSUES: $totalIssues")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("Action Required: Update these employee numbers in Active Directory.")
    }
    
    return $sb.ToString()
}

function Export-PlantJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$PlantData
    )
    
    if ($null -eq $PlantData -or $null -eq $PlantData.Plant) {
        return
    }
    
    $Plant = $PlantData.Plant
    $PlantOutputFolder = Join-Path $Config.OutputFolder $Plant
    $jsonPath = Join-Path $PlantOutputFolder "EmployeeNumberReport.json"
    
    if (-not (Test-Path $PlantOutputFolder)) {
        New-Item -Path $PlantOutputFolder -ItemType Directory -Force | Out-Null
    }
    
    $jsonObject = @{
        plant = $Plant
        generatedAt = (Get-Date -Format "yyyy-MM-ddTHH:mm:ss")
        summary = @{
            totalUsers = $PlantData.TotalUsers
            missingCount = $PlantData.MissingCount
            invalidFormatCount = $PlantData.InvalidFormatCount
            totalIssues = $PlantData.MissingCount + $PlantData.InvalidFormatCount
        }
        usersWithMissingEmployeeNumber = @($PlantData.Missing | ForEach-Object {
            @{
                samAccountName = $_.SamAccountName
                displayName = $_.DisplayName
                emailAddress = $_.EmailAddress
                reason = $_.Reason
            }
        })
        usersWithInvalidFormat = @($PlantData.InvalidFormat | ForEach-Object {
            @{
                employeeNumber = $_.EmployeeNumber
                samAccountName = $_.SamAccountName
                displayName = $_.DisplayName
                emailAddress = $_.EmailAddress
                reason = $_.Reason
            }
        })
    }
    
    $jsonObject | ConvertTo-Json -Depth 4 | Out-File -FilePath $jsonPath -Encoding UTF8
    Write-Log "JSON report saved: $jsonPath" -Level Debug -Plant $Plant
}

function Export-GlobalCsv {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$PlantResults
    )
    
    # Filter valid results
    $validResults = @($PlantResults | Where-Object {
        $null -ne $_ -and $null -ne $_.Plant -and $null -ne $_.TotalUsers
    })
    
    if ($validResults.Count -eq 0) {
        Write-Log "No valid results to export to CSV" -Level Warning
        return
    }
    
    # Export summary CSV for dashboard
    $summaryPath = Join-Path $Config.OutputFolder "DashboardSummary.csv"
    $validResults | ForEach-Object {
        [PSCustomObject]@{
            Plant = $_.Plant
            TotalUsers = $_.TotalUsers
            MissingEmployeeNumbers = $_.MissingCount
            InvalidFormat = $_.InvalidFormatCount
            TotalIssues = $_.MissingCount + $_.InvalidFormatCount
            IssuePercentage = if ($_.TotalUsers -gt 0) { [math]::Round(($_.MissingCount + $_.InvalidFormatCount) / $_.TotalUsers * 100, 2) } else { 0 }
        }
    } | Export-Csv -Path $summaryPath -NoTypeInformation -Encoding UTF8
    
    Write-Log "Dashboard CSV saved: $summaryPath" -Level Info
    
    # Export all issues CSV
    $allIssuesPath = Join-Path $Config.OutputFolder "AllIssues.csv"
    $allIssues = [System.Collections.ArrayList]::new()
    
    foreach ($plant in $validResults) {
        if ($plant.Missing) {
            foreach ($user in $plant.Missing) {
                [void]$allIssues.Add([PSCustomObject]@{
                    Plant = $plant.Plant
                    IssueType = "Missing/Empty"
                    EmployeeNumber = ""
                    SamAccountName = $user.SamAccountName
                    DisplayName = $user.DisplayName
                    EmailAddress = $user.EmailAddress
                    Reason = $user.Reason
                })
            }
        }
        if ($plant.InvalidFormat) {
            foreach ($user in $plant.InvalidFormat) {
                [void]$allIssues.Add([PSCustomObject]@{
                    Plant = $plant.Plant
                    IssueType = "InvalidFormat"
                    EmployeeNumber = $user.EmployeeNumber
                    SamAccountName = $user.SamAccountName
                    DisplayName = $user.DisplayName
                    EmailAddress = $user.EmailAddress
                    Reason = $user.Reason
                })
            }
        }
    }
    
    if ($allIssues.Count -gt 0) {
        $allIssues | Export-Csv -Path $allIssuesPath -NoTypeInformation -Encoding UTF8
        Write-Log "All issues CSV saved: $allIssuesPath ($($allIssues.Count) issues)" -Level Info
    }
}

function Export-GlobalJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$PlantResults
    )
    
    $globalJsonPath = Join-Path $Config.OutputFolder "GlobalEmployeeNumberReport.json"
    $timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ss"
    
    $totalUsers = ($PlantResults | Measure-Object -Property TotalUsers -Sum).Sum
    $totalMissing = ($PlantResults | Measure-Object -Property MissingCount -Sum).Sum
    $totalInvalid = ($PlantResults | Measure-Object -Property InvalidFormatCount -Sum).Sum
    
    $globalJson = @{
        reportMetadata = @{
            generatedAt = $timestamp
            totalPlantsProcessed = $PlantResults.Count
            reportType = "Employee Number Validation"
        }
        globalSummary = @{
            totalUsers = $totalUsers
            totalMissingEmployeeNumbers = $totalMissing
            totalInvalidFormat = $totalInvalid
            totalIssues = $totalMissing + $totalInvalid
            issuePercentage = if ($totalUsers -gt 0) { [math]::Round(($totalMissing + $totalInvalid) / $totalUsers * 100, 2) } else { 0 }
        }
        plants = @($PlantResults | ForEach-Object {
            @{
                plant = $_.Plant
                summary = @{
                    totalUsers = $_.TotalUsers
                    missingCount = $_.MissingCount
                    invalidFormatCount = $_.InvalidFormatCount
                    totalIssues = $_.MissingCount + $_.InvalidFormatCount
                }
                usersWithMissingEmployeeNumber = @($_.Missing | ForEach-Object {
                    @{
                        samAccountName = $_.SamAccountName
                        displayName = $_.DisplayName
                        emailAddress = $_.EmailAddress
                    }
                })
                usersWithInvalidFormat = @($_.InvalidFormat | ForEach-Object {
                    @{
                        employeeNumber = $_.EmployeeNumber
                        samAccountName = $_.SamAccountName
                        displayName = $_.DisplayName
                        reason = $_.Reason
                    }
                })
            }
        })
    }
    
    $globalJson | ConvertTo-Json -Depth 5 | Out-File -FilePath $globalJsonPath -Encoding UTF8
    Write-Log "Global JSON report saved: $globalJsonPath" -Level Info
}

function Generate-GlobalReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$PlantResults
    )
    
    # Filter to only valid results
    $validResults = @($PlantResults | Where-Object {
        $null -ne $_ -and $null -ne $_.Plant -and $null -ne $_.TotalUsers
    })
    
    if ($validResults.Count -eq 0) {
        Write-Log "No valid plant results to generate reports" -Level Warning
        return
    }
    
    Write-Log "Generating reports for $($validResults.Count) valid plants..." -Level Info
    
    # Export JSON and CSV
    Export-GlobalJson -PlantResults $validResults
    Export-GlobalCsv -PlantResults $validResults
    
    # Export individual plant JSONs
    foreach ($plant in $validResults) {
        Export-PlantJson -PlantData $plant
    }
    
    # Generate text report
    $globalReportPath = Join-Path $Config.OutputFolder "GlobalEmployeeNumberReport.txt"
    $sb = [System.Text.StringBuilder]::new()
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    
    [void]$sb.AppendLine("========================================")
    [void]$sb.AppendLine("  GLOBAL Employee Number Validation Report")
    [void]$sb.AppendLine("========================================")
    [void]$sb.AppendLine("Generated: $timestamp")
    [void]$sb.AppendLine("")
    
    $totalUsers = ($validResults | Measure-Object -Property TotalUsers -Sum).Sum
    $totalMissing = ($validResults | Measure-Object -Property MissingCount -Sum).Sum
    $totalInvalid = ($validResults | Measure-Object -Property InvalidFormatCount -Sum).Sum
    $plantsWithIssues = $validResults | Where-Object { $_.MissingCount -gt 0 -or $_.InvalidFormatCount -gt 0 }
    
    [void]$sb.AppendLine("GLOBAL SUMMARY:")
    [void]$sb.AppendLine("---------------")
    [void]$sb.AppendLine("Total Plants Processed: $($validResults.Count)")
    [void]$sb.AppendLine("Total AD Users Analyzed: $totalUsers")
    [void]$sb.AppendLine("Total Missing/Empty Employee Numbers: $totalMissing")
    [void]$sb.AppendLine("Total Invalid Format (SHARED, etc.): $totalInvalid")
    [void]$sb.AppendLine("TOTAL ISSUES: $($totalMissing + $totalInvalid)")
    [void]$sb.AppendLine("")
    
    if ($plantsWithIssues.Count -gt 0) {
        [void]$sb.AppendLine("PLANTS WITH ISSUES:")
        [void]$sb.AppendLine("-------------------")
        [void]$sb.AppendLine('"Plant", "TotalUsers", "Missing", "InvalidFormat", "TotalIssues"')
        
        foreach ($plant in ($plantsWithIssues | Sort-Object { $_.MissingCount + $_.InvalidFormatCount } -Descending)) {
            $issues = $plant.MissingCount + $plant.InvalidFormatCount
            [void]$sb.AppendLine("`"$($plant.Plant)`", $($plant.TotalUsers), $($plant.MissingCount), $($plant.InvalidFormatCount), $issues")
        }
        [void]$sb.AppendLine("")
    }
    
    $allMissing = $validResults | ForEach-Object { $_.Missing } | Where-Object { $_ }
    if ($allMissing.Count -gt 0) {
        [void]$sb.AppendLine("ALL USERS WITH MISSING/EMPTY EMPLOYEE NUMBERS:")
        [void]$sb.AppendLine("----------------------------------------------")
        [void]$sb.AppendLine('"SamAccountName", "DisplayName", "EmailAddress"')
        
        foreach ($user in $allMissing) {
            [void]$sb.AppendLine("`"$($user.SamAccountName)`", `"$($user.DisplayName)`", `"$($user.EmailAddress)`"")
        }
        [void]$sb.AppendLine("")
    }
    
    $allInvalid = $validResults | ForEach-Object { $_.InvalidFormat } | Where-Object { $_ }
    if ($allInvalid.Count -gt 0) {
        [void]$sb.AppendLine("ALL USERS WITH INVALID FORMAT (SHARED, text, etc.):")
        [void]$sb.AppendLine("---------------------------------------------------")
        [void]$sb.AppendLine('"EmployeeNumber", "SamAccountName", "DisplayName", "EmailAddress", "Reason"')
        
        foreach ($user in $allInvalid) {
            [void]$sb.AppendLine("`"$($user.EmployeeNumber)`", `"$($user.SamAccountName)`", `"$($user.DisplayName)`", `"$($user.EmailAddress)`", `"$($user.Reason)`"")
        }
        [void]$sb.AppendLine("")
    }
    
    [void]$sb.AppendLine("End of Report")
    
    $sb.ToString() | Out-File -FilePath $globalReportPath -Encoding UTF8
    Write-Log "Global text report saved: $globalReportPath" -Level Info
}
#endregion

#region Main
function Start-EmployeeNumberCheck {
    [CmdletBinding()]
    param()
    
    $StartTime = Get-Date
    $PlantResults = [System.Collections.ArrayList]::new()
    $SkippedPlants = [System.Collections.ArrayList]::new()
    
    try {
        Initialize-Logging
        
        if (-not (Test-Path $Config.PlantsFile)) {
            throw "Plants file not found: $($Config.PlantsFile)"
        }
        
        if (-not (Get-Module -ListAvailable -Name ActiveDirectory)) {
            throw "ActiveDirectory PowerShell module is not installed"
        }
        
        $Plants = Get-Content -Path $Config.PlantsFile | 
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { $_.Trim() }
        
        $TotalPlants = $Plants.Count
        Write-Log "Processing $TotalPlants plants..." -Level Info
        
        $ProcessedCount = 0
        
        foreach ($Plant in $Plants) {
            $ProcessedCount++
            Write-Progress -Activity "Checking Employee Numbers" -Status "$Plant ($ProcessedCount of $TotalPlants)" -PercentComplete (($ProcessedCount / $TotalPlants) * 100)
            
            if (-not (Test-PlantName -PlantName $Plant)) {
                Write-Log "Invalid plant name, skipping: $Plant" -Level Error
                [void]$SkippedPlants.Add($Plant)
                continue
            }
            
            Write-Log "Processing plant $ProcessedCount of $TotalPlants" -Level Info -Plant $Plant
            
            try {
                $result = Process-Plant -Plant $Plant
                
                if ($null -ne $result -and $null -ne $result.Plant -and $null -ne $result.TotalUsers) {
                    [void]$PlantResults.Add($result)
                }
                else {
                    Write-Log "Invalid result returned, skipping" -Level Warning -Plant $Plant
                    [void]$SkippedPlants.Add($Plant)
                }
            }
            catch {
                Write-Log "Failed to process, skipping: $($_.Exception.Message)" -Level Error -Plant $Plant
                [void]$SkippedPlants.Add($Plant)
            }
        }
        
        if ($PlantResults.Count -gt 0) {
            Generate-GlobalReport -PlantResults @($PlantResults)
            Write-Log "Reports generated for $($PlantResults.Count) plants" -Level Info
        }
        else {
            Write-Log "No valid plant results to report" -Level Warning
        }
        
        if ($SkippedPlants.Count -gt 0) {
            Write-Log "Skipped $($SkippedPlants.Count) plants: $($SkippedPlants -join ', ')" -Level Warning
        }
    }
    catch {
        Write-Log "Critical error: $($_.Exception.Message)" -Level Error
        throw
    }
    finally {
        $Duration = (Get-Date) - $StartTime
        Write-Log "========== Execution Complete ==========" -Level Info
        Write-Log "Plants processed: $($PlantResults.Count), Skipped: $($SkippedPlants.Count)" -Level Info
        Write-Log "Duration: $($Duration.ToString('hh\:mm\:ss'))" -Level Info
        Write-Progress -Activity "Checking Employee Numbers" -Completed
    }
}

# Execute
Start-EmployeeNumberCheck
