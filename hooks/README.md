# Git Hooks for Automatic Documentation Generation

This directory contains example git hooks that automatically generate LXR-style HTML documentation when you commit or push code.

## Available Hooks

### 1. `post-commit.example` - Local Documentation After Commit

**When it runs:** After you `git commit` locally
**What it does:** Generates HTML docs immediately after commit
**Best for:** Personal projects, quick local reference

**Installation:**
```bash
cp hooks/post-commit.example .git/hooks/post-commit
chmod +x .git/hooks/post-commit

# Edit the hook to set your paths
nano .git/hooks/post-commit
```

**Configuration:**
```bash
SOLUTION_FILE="YourSolution.sln"           # Your solution file
HTML_OUTPUT_DIR="docs/html"                # Where to put HTML
ANALYZER_PATH="path/to/csharp-analyzer"    # Built analyzer
```

### 2. `pre-push.example` - Local Documentation Before Push

**When it runs:** Before you `git push`
**What it does:** Generates HTML docs and optionally commits them
**Best for:** Keeping docs in sync with code in the repository

**Installation:**
```bash
cp hooks/pre-push.example .git/hooks/pre-push
chmod +x .git/hooks/pre-push

# Edit configuration
nano .git/hooks/pre-push
```

**Features:**
- Generates docs before push
- Optionally auto-commits updated docs
- Won't block your push if generation fails

### 3. `post-receive.example` - Server-Side for Gogs/Gitea

**When it runs:** After someone pushes to the Gogs/Gitea server
**What it does:** Builds analyzer, generates docs, publishes to web server
**Best for:** Team projects with self-hosted Gogs/Gitea

**Installation on Gogs Server:**
```bash
# SSH to your Gogs server
ssh user@gogs-server

# Navigate to repository hooks
cd /home/git/gogs-repositories/username/reponame.git/hooks/

# Create post-receive hook
nano post-receive
# (paste content of post-receive.example)

# Make executable
chmod +x post-receive
```

**Server Requirements:**
- .NET SDK 8+ installed
- Write permission to HTML output directory
- Web server (nginx/apache) to serve generated HTML

**Configuration:**
```bash
PROJECT_NAME="YourProject"
SOLUTION_FILE="YourSolution.sln"
WORK_DIR="/tmp/csharp-analyzer-builds/$PROJECT_NAME"
HTML_OUTPUT_DIR="/var/www/code-docs/$PROJECT_NAME"
WEB_URL="http://your-server.com/code-docs/$PROJECT_NAME"
```

## Server-Side Web Server Setup

### Option 1: Nginx

```nginx
# /etc/nginx/sites-available/code-docs
server {
    listen 80;
    server_name docs.your-company.com;

    root /var/www/code-docs;
    index index.html;

    location / {
        try_files $uri $uri/ =404;
        autoindex on;  # Enable directory listing
    }

    # Cache static assets
    location ~* \.(css|js|png|jpg|svg)$ {
        expires 1h;
        add_header Cache-Control "public, immutable";
    }
}
```

Enable:
```bash
sudo ln -s /etc/nginx/sites-available/code-docs /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### Option 2: Apache

```apache
# /etc/apache2/sites-available/code-docs.conf
<VirtualHost *:80>
    ServerName docs.your-company.com
    DocumentRoot /var/www/code-docs

    <Directory /var/www/code-docs>
        Options Indexes FollowSymLinks
        AllowOverride None
        Require all granted
    </Directory>

    # Enable directory browsing
    <Location />
        Options +Indexes
    </Location>
</VirtualHost>
```

Enable:
```bash
sudo a2ensite code-docs
sudo systemctl reload apache2
```

### Option 3: Gogs Built-in Static Server

Gogs can serve static files directly:

```ini
# In Gogs app.ini
[server]
STATIC_ROOT_PATH = /var/www/code-docs
```

Then access at: `http://gogs-server:3000/code-docs/`

## Permissions Setup for Server-Side Hooks

The git user needs write access to the HTML output directory:

```bash
# Create docs directory
sudo mkdir -p /var/www/code-docs

# Give git user ownership
sudo chown -R git:git /var/www/code-docs

# Set proper permissions
sudo chmod -R 755 /var/www/code-docs

# If using nginx/apache, add them to git group
sudo usermod -a -G git www-data  # For nginx/apache
```

## Workflow Examples

### Workflow 1: Local Development Only

```bash
# Install post-commit hook
cp hooks/post-commit.example .git/hooks/post-commit
chmod +x .git/hooks/post-commit

# Normal workflow - docs auto-generate
git add .
git commit -m "Add new feature"
# → Hook runs, generates docs/html/

# Browse locally
open docs/html/index.html
```

### Workflow 2: Commit Docs to Repository

```bash
# Install pre-push hook with auto-commit
cp hooks/pre-push.example .git/hooks/pre-push
chmod +x .git/hooks/pre-push

# Edit: Set AUTO_COMMIT_DOCS=true

# Normal workflow
git add feature.cs
git commit -m "Add feature"
git push
# → Hook generates docs, commits them, then pushes both
```

### Workflow 3: Server-Side Generation (Recommended for Teams)

```bash
# On Gogs server: Install post-receive hook
# (see installation instructions above)

# Developer workflow (no hooks needed locally)
git add feature.cs
git commit -m "Add feature"
git push
# → Server receives push, builds code, generates docs, publishes

# Everyone views latest docs at:
# http://gogs-server/code-docs/project-name/
```

## Troubleshooting

### Hook not running?

```bash
# Check if executable
ls -la .git/hooks/

# Make executable if needed
chmod +x .git/hooks/post-commit
```

### Analyzer not found?

```bash
# Build the analyzer first
dotnet build -c Release CSharpCallGraphAnalyzer.sln

# Verify path in hook matches actual location
find . -name "csharp-analyzer" -o -name "csharp-analyzer.exe"
```

### Server-side: Permission denied?

```bash
# Check git user can write to output dir
sudo -u git touch /var/www/code-docs/test.txt

# Fix permissions
sudo chown -R git:git /var/www/code-docs
sudo chmod -R 755 /var/www/code-docs
```

### Docs not updating?

```bash
# Clear browser cache
# Or check file timestamps:
ls -lart /var/www/code-docs/
```

## Best Practices

1. **Don't commit generated HTML to git** (unless using Workflow 2)
   - Add to `.gitignore`: `docs/html/`
   - Let server generate fresh on each push

2. **Keep hooks simple and fast**
   - Use `--exclude-namespace "*.Tests"` to speed up analysis
   - Enable caching in `.csharp-analyzer.json`

3. **Always exit 0 in hooks**
   - Hooks should never block commits/pushes
   - Log errors but don't fail

4. **Monitor server disk usage**
   - Old builds in `/tmp/csharp-analyzer-builds/`
   - Hook includes cleanup (keeps last 3)

5. **Branch-specific generation**
   - Server-side hook only runs for main/master
   - Customize `if [[ "$refname" =~ ... ]]` for other branches

## Advanced: Notifications

Add to post-receive hook to notify team:

```bash
# Slack
curl -X POST "https://hooks.slack.com/services/YOUR/WEBHOOK/URL" \
    -H "Content-Type: application/json" \
    -d "{\"text\": \"📚 Code docs updated for $PROJECT_NAME: $WEB_URL\"}"

# Email
echo "Docs updated: $WEB_URL" | mail -s "Code Documentation Updated" team@company.com

# IRC/Matrix
# Use your notification tool of choice
```

## See Also

- [Git Hooks Documentation](https://git-scm.com/book/en/v2/Customizing-Git-Git-Hooks)
- [Gogs Documentation](https://gogs.io/docs)
- Main README for analyzer usage
