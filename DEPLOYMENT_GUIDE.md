# Deploy Recipe Search Web to Azure (Free Tier)

## Quick Deployment Steps

### Option 1: Using VS Code Azure Extension (Recommended)

1. **Sign in to Azure**
   - Open Command Palette (Ctrl+Shift+P)
   - Type: `Azure: Sign In`
   - Follow the browser login prompts

2. **Deploy to Azure App Service**
   - Open Command Palette (Ctrl+Shift+P)
   - Type: `Azure App Service: Deploy to Web App`
   - Select: `RecipeSearchWeb/publish` folder
   - Choose: `+ Create new Web App... (Advanced)`
   - Enter a unique name (e.g., `recipe-search-yourname`)
   - Select: `Windows` as OS
   - Select: `.NET 8` runtime (closest to .NET 10)
   - Select: `F1 (Free)` pricing tier
   - Create new Resource Group: `recipe-search-rg`
   - Create new App Service Plan: `recipe-search-plan` (Free tier)
   - Wait for deployment to complete

3. **Configure Application Settings (Important!)**
   - In Azure extension sidebar, expand your subscription
   - Right-click your web app → `Open in Portal`
   - Go to: Configuration → Application settings
   - Add these settings:
     - `AZURE_OPENAI_ENDPOINT`: Your Azure OpenAI endpoint
     - `AZURE_OPENAI_API_KEY`: Your API key
     - `AZURE_OPENAI_DEPLOYMENT_NAME`: Your deployment name
   - Click `Save`

4. **Access Your Site**
   - URL will be: `https://your-app-name.azurewebsites.net`

---

### Option 2: Alternative Free Hosting - Railway.app

1. **Install Railway CLI**
   ```powershell
   npm install -g @railway/cli
   ```

2. **Login and Deploy**
   ```powershell
   cd RecipeSearchWeb
   railway login
   railway init
   railway up
   ```

3. **Add Environment Variables**
   - Go to Railway dashboard
   - Add: AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, AZURE_OPENAI_DEPLOYMENT_NAME

---

### Option 3: Render.com (Free Tier)

1. **Go to** https://render.com
2. **Sign up** with GitHub
3. **Connect Repository**
4. **Create New Web Service**
   - Select your repository
   - Build Command: `dotnet publish -c Release -o publish`
   - Start Command: `dotnet publish/RecipeSearchWeb.dll`
5. **Add Environment Variables** in Render dashboard

---

## Environment Variables Needed

You need to add these three variables in whatever hosting service you choose:

- `AZURE_OPENAI_ENDPOINT`: e.g., `https://your-resource.openai.azure.com/`
- `AZURE_OPENAI_API_KEY`: Your API key from Azure
- `AZURE_OPENAI_DEPLOYMENT_NAME`: e.g., `text-embedding-3-small`

---

## Current Status

✅ App built successfully in `/publish` folder
✅ Ready for deployment
✅ Azure App Service extension installed in VS Code

## Share the URL

Once deployed, share the URL with your friend:
- Azure: `https://your-app-name.azurewebsites.net`
- Railway: `https://your-app.up.railway.app`
- Render: `https://your-app.onrender.com`

## Troubleshooting

If the site doesn't load:
1. Check that environment variables are set correctly
2. View logs in the hosting platform dashboard
3. Ensure .NET runtime is compatible
4. Restart the web app after setting environment variables
