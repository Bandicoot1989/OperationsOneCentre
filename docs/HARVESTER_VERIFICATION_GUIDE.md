# 🔍 Jira Solution Harvester - Verification Guide

## Overview

This guide explains how to verify that the **JiraSolutionHarvesterService** is working correctly in your Operations One Centre application.

## What Does the Harvester Do?

The Jira Solution Harvester is a background service that:
1. ✅ Runs every **6 hours** automatically
2. ✅ Fetches resolved Jira tickets from the last **7 days**
3. ✅ Extracts solutions from ticket comments using **keywords** and **AI**
4. ✅ Generates **embeddings** for semantic search
5. ✅ Stores solutions in **Azure Blob Storage** (`harvested-solutions` container)
6. ✅ Enriches your **Knowledge Base** automatically

---

## ✅ Method 1: Monitoring Dashboard (Recommended)

### Access the Dashboard

**Production:**
```
https://your-app.azurewebsites.net/monitoring
```

**Local Development:**
```
https://localhost:5001/monitoring
```

### What to Check

Navigate to the **"Harvester" tab** and verify:

#### 🟢 Service Status
| Indicator | What It Means | Expected Value |
|-----------|---------------|----------------|
| **Is Running** | Service is active | ✅ `true` |
| **Is Configured** | Jira credentials valid | ✅ `true` |
| **Last Harvest Time** | When it last executed | Recent timestamp |
| **Next Scheduled** | When it will run next | ~6 hours from last run |

#### 📊 Statistics
| Metric | Description |
|--------|-------------|
| **Total Tickets Processed** | All tickets scanned |
| **Total Solutions Harvested** | Solutions successfully extracted |
| **Total Skipped** | Already processed tickets |
| **Total No Solution** | Tickets without useful solutions |

#### 🎯 Last Run Details
- Tickets found
- New solutions added
- Duration
- Success status
- Error message (if any)

#### 📋 Recent Solutions
- Last 10 harvested solutions
- Ticket IDs with Jira links
- Systems detected (SAP, Network, etc.)
- Harvest dates

---

## ✅ Method 2: PowerShell Script Check

Use the provided PowerShell script to check Azure Blob Storage directly:

### Basic Check
```powershell
.\Check-HarvesterStatus.ps1
```

### Detailed Check with Solution Details
```powershell
.\Check-HarvesterStatus.ps1 -Detailed
```

### With Connection String
```powershell
.\Check-HarvesterStatus.ps1 -ConnectionString "your_connection_string" -Detailed
```

### What the Script Checks
- ✅ Container exists (`harvested-solutions`)
- ✅ Files present:
  - `jira-solutions-with-embeddings.json` (main storage)
  - `harvested-tickets.json` (processed ticket IDs)
  - `harvester-run-history.json` (execution history)
- ✅ File sizes and last modified dates
- ✅ Number of solutions stored
- ✅ Recent solution details

---

## ✅ Method 3: Azure Portal Check

### Navigate to Storage Account

1. Go to **Azure Portal**
2. Find your storage account (e.g., `oocstorage`)
3. Click **Containers**
4. Look for **`harvested-solutions`** container

### Files to Check

| File Name | Purpose | When Created |
|-----------|---------|--------------|
| `jira-solutions-with-embeddings.json` | Main storage for harvested solutions | After first successful run |
| `harvested-tickets.json` | List of processed ticket IDs (prevents duplicates) | After first run |
| `harvester-run-history.json` | Execution history logs | After each run |

### Verify File Contents

**Download and inspect `jira-solutions-with-embeddings.json`:**

```json
[
  {
    "TicketId": "MT-12345",
    "TicketTitle": "Usuario no puede acceder a SAP",
    "Problem": "Error de autenticación en SAP",
    "Solution": "Se resolvió asignando el rol correcto...",
    "Steps": [
      "Verificar usuario en SU01",
      "Asignar rol faltante",
      "Validar acceso"
    ],
    "System": "SAP",
    "Category": "MT",
    "Keywords": ["sap", "acceso", "usuario", "rol"],
    "Priority": "High",
    "ResolvedDate": "2026-01-15T10:30:00Z",
    "HarvestedDate": "2026-01-15T12:00:00Z",
    "Embedding": [0.123, -0.456, ...],  // 1536 dimensions
    "ValidationCount": 0,
    "IsPromoted": false,
    "JiraUrl": "https://antolin.atlassian.net/browse/MT-12345"
  }
]
```

---

## ✅ Method 4: Application Logs

### Check Azure App Service Logs

1. **Azure Portal** → Your App Service
2. **Monitoring** → **Log stream**
3. Look for log entries:

```
[Information] JiraSolutionHarvesterService started - Fase 4: Full integration with search
[Information] Storage containers initialized
[Information] Loaded 245 processed Jira tickets
[Information] Harvesting Jira solutions...
[Information] Found 12 resolved tickets from Jira
[Information] Harvesting complete: 3 new solutions added (total: 248). 8 skipped, 1 without solution. Duration: 15.2s
```

### Success Indicators in Logs
- ✅ `JiraSolutionHarvesterService started`
- ✅ `Storage containers initialized`
- ✅ `Found X resolved tickets from Jira`
- ✅ `X new solutions added`
- ✅ `Generated embedding for {TicketId}`

### Error Indicators
- ❌ `JiraClient not configured`
- ❌ `Failed to initialize storage containers`
- ❌ `Failed to process solution from ticket`

---

## 🔧 Troubleshooting

### Issue: "Is Configured" shows False

**Cause**: Jira credentials not set or invalid

**Fix**:
```bash
# Check Azure App Service Configuration
Jira__BaseUrl = https://antolin.atlassian.net
Jira__Email = your.email@company.com
Jira__ApiToken = your_api_token_here
```

Restart the app after setting environment variables.

---

### Issue: "Is Running" shows False

**Possible Causes**:
1. App just started (waits 30 seconds to initialize)
2. Storage initialization failed
3. Service crashed on startup

**Fix**:
1. Check App Service logs for errors
2. Verify Azure Storage connection string is valid
3. Restart the App Service

---

### Issue: No New Solutions Being Harvested

**Possible Causes**:
1. No new resolved tickets in Jira (last 7 days)
2. All tickets already processed
3. Tickets don't have solution keywords in comments

**What's Normal**:
- If no tickets were resolved in last 7 days → 0 new solutions ✅
- If all tickets already processed → Skipped count increases ✅

**To Verify**:
```bash
# Check Jira directly
https://antolin.atlassian.net/issues/?jql=resolved >= -7d

# Or use test endpoint
GET /api/jiratest/tickets?days=7
```

---

### Issue: Solutions Not Appearing in Chatbot

**Possible Causes**:
1. Solutions harvested but not integrated into search
2. Embeddings not generated
3. Search service not initialized

**Fix**:
1. Check that `Embedding` field has 1536 dimensions
2. Verify `IJiraSolutionService` is registered in DI
3. Check that Knowledge Agent uses harvested solutions

---

## 📊 Expected Behavior

### First Run (Cold Start)
- **Duration**: 1-3 minutes (downloading tickets, generating embeddings)
- **Solutions**: 10-50 solutions (depending on resolved tickets)
- **Files Created**: All 3 files in blob storage

### Subsequent Runs
- **Duration**: 10-30 seconds (most tickets already processed)
- **New Solutions**: 0-5 per run (only new resolved tickets)
- **Files Updated**: All files get timestamp updates

### Performance Metrics
| Metric | Expected Range |
|--------|----------------|
| Tickets scanned per run | 10-50 |
| New solutions per run | 0-5 (after initial harvest) |
| Processing time | 10-60 seconds |
| Embedding generation | ~100ms per solution |
| Skipped tickets | Increases over time (normal) |

---

## 🎯 Health Check Checklist

Use this checklist to verify everything is working:

- [ ] **Harvester tab shows "Is Running: true"**
- [ ] **Harvester tab shows "Is Configured: true"**
- [ ] **Last Harvest Time is recent (within 6 hours)**
- [ ] **Total Solutions > 0**
- [ ] **Recent Solutions list shows entries**
- [ ] **Azure Blob Storage has `harvested-solutions` container**
- [ ] **File `jira-solutions-with-embeddings.json` exists and has content**
- [ ] **File size increases over time (as new solutions added)**
- [ ] **App Service logs show "Harvesting complete" messages**
- [ ] **No error messages in logs**
- [ ] **Chatbot can use harvested solutions (ask about a known resolved ticket)**

---

## 💡 Pro Tips

### Force a Manual Run
Restart the App Service - the harvester runs 30 seconds after startup.

### Speed Up Testing
Temporarily change harvest interval in code (not recommended for production):
```csharp
_interval = TimeSpan.FromMinutes(5); // Instead of FromHours(6)
```

### Monitor in Real-Time
Keep the Monitoring dashboard open and refresh every few minutes during a harvest cycle.

### Validate Solutions Quality
Download the JSON file and spot-check:
- Are solutions meaningful?
- Do they have proper system detection?
- Are embeddings present (1536 dimensions)?

### Check Integration with Chatbot
Ask the bot:
> "¿Tienes soluciones para problemas de SAP?"

It should reference harvested solutions if integration is working.

---

## 📞 Need Help?

If the harvester is not working after checking all the above:

1. **Collect diagnostics**:
   - Screenshot of Monitoring dashboard (Harvester tab)
   - Recent App Service logs (last 1000 lines)
   - Output from `Check-HarvesterStatus.ps1 -Detailed`

2. **Check configuration**:
   - Jira credentials valid
   - Azure Storage connection string correct
   - App Service has necessary permissions

3. **Review documentation**:
   - [JIRA_SOLUTION_HARVESTER.md](./JIRA_SOLUTION_HARVESTER.md)
   - [JIRA_INTEGRATION_TROUBLESHOOTING.md](./JIRA_INTEGRATION_TROUBLESHOOTING.md)

---

## 🚀 Success Criteria

Your harvester is working correctly if:

✅ Service runs automatically every 6 hours  
✅ New resolved tickets are processed  
✅ Solutions are extracted and stored  
✅ Embeddings are generated (1536 dimensions)  
✅ Blob storage files are updated  
✅ Monitoring dashboard shows accurate stats  
✅ No errors in logs  
✅ Chatbot can reference harvested solutions  

---

**Last Updated**: January 20, 2026  
**Version**: 1.0
