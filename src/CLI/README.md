# FS FileShare CLI

Command-line tool for deploying and managing FS FileShare on Cloudflare.

## Features

- 🚀 Deploy Worker and Pages in one command
- 👤 Manage user credentials via Cloudflare Secrets
- 📊 View deployment status
- 🔧 Wraps Fable and Wrangler for seamless deployment

## Installation

### Local Tool (Recommended)

```bash
# From project root
dotnet tool install --global --add-source ./src/CLI/bin/Release FS.FileShare.CLI
```

### Or run directly

```bash
cd src/CLI
dotnet run -- [command]
```

## Configuration

Set these environment variables:

```bash
# Required
export CLOUDFLARE_ACCOUNT_ID="your-account-id"
export CLOUDFLARE_API_TOKEN="your-api-token"

# Optional (with defaults)
export R2_BUCKET_NAME="fs-fileshare"              # Default R2 bucket name
export WORKER_NAME="fs-fileshare-worker"          # Default worker name
export PAGES_PROJECT_NAME="fs-fileshare"          # Default pages project name
```

### Getting Your Credentials

1. **Account ID**: Found in Cloudflare Dashboard → Workers & Pages → right sidebar
2. **API Token**:
   - Go to Cloudflare Dashboard → My Profile → API Tokens
   - Create Token → "Edit Cloudflare Workers" template
   - Add permissions:
     - Account / Cloudflare Pages / Edit
     - Account / Workers Scripts / Edit
     - Account / Workers R2 Storage / Edit

## Commands

### Deploy Everything

Builds and deploys both Worker and Pages:

```bash
fs-cli deploy
```

Skip the build step (if already built):

```bash
fs-cli deploy --skip-build
```

**What it does:**
1. Runs `npm run build` (Worker + Pages)
2. Compiles Worker with Fable
3. Uploads Worker to Cloudflare via API
4. Creates R2 bucket binding
5. Deploys Pages using `wrangler pages deploy`

### Add User

Add a user with credentials:

```bash
fs-cli add-user --username alice --password secret123
```

This creates a Cloudflare Secret: `USER_ALICE_PASSWORD`

### Check Status

View deployment information:

```bash
fs-cli status
```

Shows:
- Worker deployment status and URL
- Pages deployment status and URL
- R2 bucket configuration

### Version

```bash
fs-cli --version
```

## Workflow

### First Time Setup

```bash
# 1. Set environment variables
export CLOUDFLARE_ACCOUNT_ID="..."
export CLOUDFLARE_API_TOKEN="..."

# 2. Create R2 bucket (via Cloudflare Dashboard for now)
#    Name: fs-fileshare

# 3. Deploy everything
fs-cli deploy

# 4. Add users
fs-cli add-user --username alice --password secret123
fs-cli add-user --username bob --password hunter2

# 5. Check status
fs-cli status
```

### Regular Updates

```bash
# Make code changes...

# Deploy
fs-cli deploy
```

## Development

Build the CLI:

```bash
cd src/CLI
dotnet build
```

Run locally:

```bash
dotnet run -- deploy
dotnet run -- add-user --username test --password test123
dotnet run -- status
```

Pack as tool:

```bash
dotnet pack -c Release
dotnet tool install --global --add-source ./bin/Release FS.FileShare.CLI
```

## Architecture

The CLI wraps:
- **Fable**: Compiles F# Worker to JavaScript
- **Cloudflare Workers API**: Direct upload of Worker scripts
- **Wrangler**: Pages deployment via `wrangler pages deploy`
- **Cloudflare Secrets API**: User password management

## Troubleshooting

### "Wrangler not found"

Install Wrangler globally:

```bash
npm install -g wrangler
```

### "CLOUDFLARE_ACCOUNT_ID not set"

Make sure environment variables are exported in your current shell.

### "Worker deployment failed"

Check that:
1. R2 bucket exists in your account
2. API token has correct permissions
3. Worker name doesn't conflict with existing worker

### "Pages deployment failed"

Ensure:
1. `npm run build:pages` completed successfully
2. `dist/pages` directory exists
3. Wrangler is installed and authenticated
