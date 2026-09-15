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
- **Azure Key Vault Secrets**: Zero hardcoded credentials; IMAP/SMTP hosts, ports, usernames, and passwords/tokens are retrieved dynamically from Azure Key Vault using `DefaultAzureCredential`.
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
| `move_item` | `mailbox_id`, `source_folder_id`, `target_folder_id`, `item_id` | Moves an email message to a different target folder. |
| `trash_item` | `mailbox_id`, `folder_id`, `item_id` | Moves an email message to the mailbox's designated Trash folder. |
| `archive_item` | `mailbox_id`, `folder_id`, `item_id` | Moves an email message to the mailbox's designated Archive folder. |
| `mark_item_read` | `mailbox_id`, `folder_id`, `item_id` | Marks an email message as read. |
| `mark_item_unread` | `mailbox_id`, `folder_id`, `item_id` | Marks an email message as unread. |
| `mark_item_flagged` | `mailbox_id`, `folder_id`, `item_id` | Marks an email message as flagged (starred/important). |
| `mark_item_unflagged` | `mailbox_id`, `folder_id`, `item_id` | Removes the flagged (starred/important) flag from an email message. |
| `send_email` | `mailbox_id`, `to`, `subject`, `body_text`, `body_html?`, `cc?`, `bcc?` | Sends an email (currently logs attempt and throws `NotImplementedException`). |

---

## Configuration & Azure Key Vault

The service reads sensitive configuration from Azure Key Vault. Set the Key Vault URI in your environment or `appsettings.json`:

```json
{
  "KeyVault": {
    "VaultUri": "https://<your-key-vault-name>.vault.azure.net/"
  }
}
```

Or set the environment variable:
```bash
AZURE_KEYVAULT_URI=https://<your-key-vault-name>.vault.azure.net/
```

### Key Vault Secrets Layout & Email Passwords

Store mailbox connection credentials, email passwords, and client API keys with the hierarchical format:

- `Passwords--<sanitized-email>` or `<sanitized-email>` — Dedicated email password independently named in Key Vault (e.g. `Passwords--email1-firstdomain-net` or `email1-firstdomain-net`). Key Vault only allows alphanumeric characters and hyphens, so characters like `@` and `.` are converted to `-`.
- `ApiKeys` — JSON array (`["key1", "key2"]`) or comma-separated list of authorized client API keys
- `ApiKeys--<index>` — Individual indexed authorized API key (e.g. `ApiKeys--0`)
- `Mailboxes--<mailbox-id>--ImapHost` — e.g. `imap.example.com`
- `Mailboxes--<mailbox-id>--ImapPort` — e.g. `993`
- `Mailboxes--<mailbox-id>--ImapSsl` — e.g. `SslOnConnect`
- `Mailboxes--<mailbox-id>--SmtpHost` — e.g. `smtp.example.com`
- `Mailboxes--<mailbox-id>--SmtpPort` — e.g. `587`
- `Mailboxes--<mailbox-id>--SmtpSsl` — e.g. `true` or `Auto`
- `Mailboxes--<mailbox-id>--Username` — e.g. `agent@example.com`
- `Mailboxes--<mailbox-id>--Password` — Fallback secret password or app password

In local settings (`appsettings.json` or User Secrets), email passwords can be independently declared in their own `Passwords` section:

```json
{
  "Passwords": {
    "email1@firstdomain.net": "asdfasdf",
    "second-email@diffdomain.com": "woah"
  }
}
```

Authentication to Azure Key Vault is handled via `Azure.Identity.DefaultAzureCredential`, supporting Azure CLI (`az login`), environment credentials, Visual Studio credentials, and Azure Managed Identity in production.

---

## Client Authentication

All MCP endpoints require an authorized API key. You can pass the key in three ways:

1. **Header**: `X-API-Key: <your-api-key>`
2. **Authorization Header**: `Authorization: Bearer <your-api-key>`
3. **Query Parameter**: `?apiKey=<your-api-key>` or `?api_key=<your-api-key>` (ideal for SSE `EventSource` clients)

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
git clone https://github.com/linkofdarkness/email-management-mcp.git
cd email-management-mcp

# Set your Azure Key Vault URI
export AZURE_KEYVAULT_URI="https://<your-vault-name>.vault.azure.net/"

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
