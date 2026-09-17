# Email Management MCP

An ASP.NET Core service providing a Model Context Protocol (MCP) server for autonomous AI agents and automated workflows to interact with email mailboxes, folders, and messages.

## Features

- **MCP Server**: Powered by the official [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK for Streamable HTTP / SSE transport.
- **IMAP / SMTP Support**: Standardized communication with email providers (Office 365, Gmail, custom email hosts) powered by [MailKit](https://github.com/jstedfast/MailKit).
- **Mailbox & Folder Management**:
  - List configured mailboxes and discover full folder hierarchies with unread/total message counts.
  - Create new folders and subfolders within mailbox namespaces.
- **Message Inspection & Organization**:
  - Fetch message summaries with pagination and unread filters.
  - Retrieve full email details including plain text, HTML bodies, headers, and attachment metadata.
  - Move messages between folders.
  - Safely trash or archive messages (automatically resolves designated Trash and Archive folders and moves them internally).
  - Update message status by marking emails as read/unread and flagged/unflagged.
- **Azure Key Vault Secrets**: Zero hardcoded credentials; sensitive values (API keys, mailbox passwords, feature switches) are loaded dynamically from Azure Key Vault using `DefaultAzureCredential`.
- **Lod Softworks Architecture**: Built on modern .NET 10 following clean architecture, primary constructors, C# record types, and file-scoped namespaces.

---

## MCP Tools Reference

When connected via an MCP client, the following tools are exposed:

| Tool Name | Parameters | Description |
|-----------|------------|-------------|
| `list_mailboxes` | _None_ | Lists all configured mailboxes accessible by the service. |
| `list_folders` | `mailbox_id` | Lists all folders and child folders for a given mailbox. |
| `create_folder` | `mailbox_id`, `folder_name`, `parent_folder_id?` | Creates a new folder or directory in the specified mailbox. |
| `list_folder_items` | `mailbox_id`, `folder_id`, `limit?`, `offset?`, `unread_only?` | Lists email summaries in a folder with pagination. |
| `get_item` | `mailbox_id`, `folder_id`, `item_id`, `include_body_html?` | Retrieves full email content, headers, body, and attachment metadata. |
| `download_attachment` | `mailbox_id`, `folder_id`, `item_id`, `attachment_id` | Downloads an email attachment, returning metadata and base64 encoded content. |
| `move_item` | `mailbox_id`, `source_folder_id`, `target_folder_id`, `item_id` | Moves an email message to a different target folder. |
| `trash_item` | `mailbox_id`, `folder_id`, `item_id` | Moves an email message to the mailbox's designated Trash folder. |
| `archive_item` | `mailbox_id`, `folder_id`, `item_id` | Moves an email message to the mailbox's designated Archive folder. |
| `mark_item_read` | `mailbox_id`, `folder_id`, `item_id` | Marks an email message as read. |
| `mark_item_unread` | `mailbox_id`, `folder_id`, `item_id` | Marks an email message as unread. |
| `mark_item_flagged` | `mailbox_id`, `folder_id`, `item_id` | Marks an email message as flagged (starred/important). |
| `mark_item_unflagged` | `mailbox_id`, `folder_id`, `item_id` | Removes the flagged (starred/important) flag from an email message. |
| `send_email` | `mailbox_id`, `to`, `subject`, `body_text`, `body_html?`, `cc?`, `bcc?` | Sends an email via SMTP (controlled by configuration flag `EmailSending:Enabled`). |

---

## Configuration & Azure Key Vault

The application follows the security practice of storing application settings in configuration files (`appsettings.json`) while delegating all secrets—such as email passwords and MCP client API keys—to **Azure Key Vault** (or User Secrets in local development).

### Application Settings (`appsettings.json`)

Non-sensitive configuration (Key Vault endpoint, logging, feature flags, and mailbox server connection details) is defined in `appsettings.json`. Passwords and API keys are intentionally omitted:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "KeyVault": {
    "VaultUri": "https://<your-key-vault-name>.vault.azure.net/"
  },
  "EmailSending": {
    "Enabled": false
  },
  "Mailboxes": [
    {
      "Id": "personal",
      "DisplayName": "Personal Email",
      "EmailAddress": "personal@example.com",
      "ImapHost": "imap.example.com",
      "ImapPort": 993,
      "ImapUseSsl": true,
      "SmtpHost": "smtp.example.com",
      "SmtpPort": 587,
      "SmtpUseSsl": true,
      "IsActive": true
    },
    {
      "Id": "work",
      "DisplayName": "Work Email",
      "EmailAddress": "work@example.com",
      "ImapHost": "imap.example.com",
      "ImapPort": 993,
      "ImapUseSsl": true,
      "ImapUserName": "work-custom-imap-user",
      "SmtpHost": "smtp.example.com",
      "SmtpPort": 587,
      "SmtpUseSsl": true,
      "SmtpUserName": "work-custom-smtp-user",
      "IsActive": true
    }
  ]
}
```

> [!NOTE]
> **Optional IMAP & SMTP Usernames**:
> - `ImapUserName` *(optional)*: The login username for IMAP. When omitted or left blank, the service automatically uses `EmailAddress`.
> - `SmtpUserName` *(optional)*: The login username for SMTP. When omitted or left blank, the service automatically falls back to `ImapUserName` (if provided), or `EmailAddress`.
> In most email configurations (e.g. Microsoft 365, Google Workspace, standard email hosts), credentials use the email address directly, so `ImapUserName` and `SmtpUserName` only need to be configured when the host requires a distinct username.

The Key Vault URI can be specified in `appsettings.json` under `KeyVault:VaultUri` (or `AzureKeyVault:VaultUri`), or via environment variables:
```bash
export KeyVault__VaultUri="https://<your-key-vault-name>.vault.azure.net/"
# or
export AzureKeyVault__VaultUri="https://<your-key-vault-name>.vault.azure.net/"
```

The service automatically discovers and wires up Azure Key Vault when a valid URI is configured. When running locally without Azure Key Vault, leaving `VaultUri` blank or unset safely skips Key Vault initialization and falls back to User Secrets or local settings.

### Secrets in Azure Key Vault

All sensitive values—specifically **client API keys** and **mailbox passwords**—are stored securely in Azure Key Vault. In ASP.NET Core, double-dash delimiters (`--`) in Key Vault secret names automatically map to hierarchical configuration keys (`:`).

#### Key Vault Secret Naming & Examples

| Secret Type | Key Vault Secret Name Pattern | Example Secret Name | Example Value | Description |
|-------------|-------------------------------|---------------------|---------------|-------------|
| **Client API Key** | `Authentication--ApiKeys--<index>` | `Authentication--ApiKeys--0` | `lod-agent-key-abcdef123456` | Authorized API key for MCP clients connecting to `/mcp`. |
| **Email Sending Switch** | `EmailSending--Enabled` | `EmailSending--Enabled` | `true` | Global safety switch controlling whether MCP clients can send outgoing emails via SMTP. |
| **Mailbox Password** | `Passwords--<mailbox-id>` | `Passwords--personal` | `my-secure-email-password` | Password matching the mailbox's `Id` in `appsettings.json`. |

### Email Sending Configuration

Outgoing email delivery via SMTP is controlled by a dedicated configuration safety flag:

```json
"EmailSending": {
  "Enabled": false
}
```

- **Default Safety Mode**: Defaults to `false`. When disabled, any invocation of the `send_email` MCP tool will log a warning and return an MCP execution error (`isError: true`), preventing unintended email transmission.
- **Enabling Sending**: Set `"EmailSending:Enabled": true` in `appsettings.json`, via environment variable (`EmailSending__Enabled=true`), or in Azure Key Vault (`EmailSending--Enabled="true"`).
- **Delivery**: When enabled, outgoing emails are composed and transmitted via MailKit's SMTP client using the server parameters (`SmtpHost`, `SmtpPort`, `SmtpUseSsl`, and credentials) defined for the specified mailbox. Recipients in `to`, `cc`, and `bcc` are validated, and plain text with optional HTML body formatting is supported.

#### Azure CLI Examples

Use the Azure CLI to provision your secrets directly into your vault:

```bash
# Store client API keys for AI agent authentication
az keyvault secret set --vault-name "<your-key-vault-name>" \
  --name "Authentication--ApiKeys--0" \
  --value "lod-agent-key-abcdef123456"

# Store mailbox passwords mapped to mailbox Ids defined in appsettings.json
az keyvault secret set --vault-name "<your-key-vault-name>" \
  --name "Passwords--personal" \
  --value "app-specific-password-for-personal"

az keyvault secret set --vault-name "<your-key-vault-name>" \
  --name "Passwords--work" \
  --value "app-specific-password-for-work"
```

Authentication to Azure Key Vault is handled via `Azure.Identity.DefaultAzureCredential`, supporting Azure CLI (`az login`), environment credentials, Visual Studio credentials, and Azure Managed Identity in production.

#### Local Development (User Secrets)

For local development without an active Azure Key Vault connection, you can store passwords and API keys safely in [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) without committing them to git:

```bash
dotnet user-secrets set "Authentication:ApiKeys:0" "dev-api-key-12345" --project src/Lod.EmailManagement.Mcp
dotnet user-secrets set "Passwords:personal" "local-dev-personal-password" --project src/Lod.EmailManagement.Mcp
dotnet user-secrets set "Passwords:work" "local-dev-work-password" --project src/Lod.EmailManagement.Mcp
```

---

## Client Authentication

All MCP endpoints require an authorized API key. You can pass the key in three ways:

1. **Header**: `X-API-Key: <your-api-key>`
2. **Authorization Header**: `Authorization: Bearer <your-api-key>`
3. **Query Parameter**: `?apiKey=<your-api-key>` or `?api_key=<your-api-key>` (ideal for SSE `EventSource` clients)

### Direct File Attachment Download (HTTP)

In addition to the `download_attachment` MCP tool, attachments can be downloaded directly as binary files via HTTP:

```http
GET /api/attachments/download?mailboxId={mailboxId}&folderId={folderId}&itemId={itemId}&attachmentId={attachmentId}
```

- **Authentication**: Supports the same authentication mechanisms as the MCP endpoint (`X-API-Key` header, `Authorization: Bearer <key>`, or `?apiKey=<key>`).
- **Response**: Streams the file binary directly with appropriate `Content-Type` and `Content-Disposition: attachment; filename="<filename>"` headers.
- **Attachment Identification**: `attachmentId` can be the zero-based index (`"0"`, `"1"`), `ContentId`, or file name (e.g. `"report.pdf"`).

---

## Connecting AI Agents (MCP)

To connect an MCP client (such as Cursor or Claude Desktop) using Streamable HTTP / SSE:

```json
{
  "mcpServers": {
    "email-management": {
      "url": "http://localhost:5077/mcp?apiKey=your-api-key"
    }
  }
}
```

Or using custom headers if supported by the MCP client:

```json
{
  "mcpServers": {
    "email-management": {
      "url": "http://localhost:5077/mcp",
      "headers": {
        "X-API-Key": "your-api-key"
      }
    }
  }
}
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Azure Key Vault instance (or Azure CLI logged in with permissions to a vault)

### Running Locally

```bash
# Clone the repository
git clone https://github.com/lod-softworks/mcp-email-management.git
cd mcp-email-management

# Set your Azure Key Vault URI (optional in local development)
export KeyVault__VaultUri="https://<your-vault-name>.vault.azure.net/"

# Run the API (defaults to http://localhost:5077 and https://localhost:7021)
dotnet run --project src/Lod.EmailManagement.Mcp
```

### Running Tests

```bash
dotnet test
```

---

## Repository Documentation

- **[REQUIREMENTS.md](REQUIREMENTS.md)**: Product Requirements Document (PRD) detailing architecture, data models, error handling, and implementation specifics.
- **[AGENTS.md](AGENTS.md)**: Index and conventions for AI agents working in this repository.
- **[.agents/skills/dotnet/](.agents/skills/dotnet/)**: C# and .NET coding standards from Lod Softworks.

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
