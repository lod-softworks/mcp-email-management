# Email Management MCP

An ASP.NET Core API providing a dual Model Context Protocol (MCP) and REST interface for autonomous AI agents and automated workflows to interact with email mailboxes, folders, and messages.

## Features

- **Dual Surface (MCP & REST)**:
  - **MCP Server**: Powered by the official [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK for Streamable HTTP / SSE transport.
  - **REST API**: Standard HTTP endpoints with interactive [Scalar](https://github.com/scalar/scalar) API documentation (`/scalar/v1`) for direct service integration.
- **IMAP / SMTP Support**: Standardized communication with email providers (Office 365, Gmail, custom email hosts) powered by [MailKit](https://github.com/jstedfast/MailKit).
- **Mailbox & Folder Navigation**: List configured mailboxes, discover folder hierarchies, and inspect unread/total message counts.
- **Message Inspection & Organization**:
  - Fetch message summaries with pagination and unread filters.
  - Retrieve full email details including plain text, HTML bodies, headers, and attachment metadata.
  - Move messages between folders.
  - Safely trash messages (automatically resolves designated Trash folders and moves them internally).
- **Azure Key Vault Secrets**: Zero hardcoded credentials; IMAP/SMTP hosts, ports, usernames, and passwords/tokens are retrieved dynamically from Azure Key Vault using `DefaultAzureCredential`.
- **Lod Softworks Architecture**: Built on modern .NET LTS following clean architecture, primary constructors, C# record types, and file-scoped namespaces.

---

## MCP Tools Reference

When connected via an MCP client, the following tools are exposed:

| Tool Name | Parameters | Description |
|-----------|------------|-------------|
| `list_mailboxes` | _None_ | Lists all configured mailboxes accessible by the service. |
| `list_folders` | `mailbox_id` | Lists all folders and child folders for a given mailbox. |
| `list_folder_items` | `mailbox_id`, `folder_id`, `limit?`, `offset?`, `unread_only?` | Lists email summaries in a folder with pagination. |
| `get_item` | `mailbox_id`, `folder_id`, `item_id`, `include_body_html?` | Retrieves full email content, headers, body, and attachment metadata. |
| `move_item` | `mailbox_id`, `source_folder_id`, `target_folder_id`, `item_id` | Moves an email message to a different target folder. |
| `trash_item` | `mailbox_id`, `folder_id`, `item_id` | Moves an email message to the mailbox's designated Trash folder. |
| `send_email` | `mailbox_id`, `to`, `subject`, `body_text`, `body_html?`, `cc?`, `bcc?` | Sends an email (currently logs attempt and throws `NotImplementedException`). |

---

## REST Endpoints

The service also exposes standard REST endpoints:

- `GET /api/mailboxes` — List mailboxes
- `GET /api/mailboxes/{mailboxId}/folders` — List folders
- `GET /api/mailboxes/{mailboxId}/folders/{folderId}/items` — List items in a folder
- `GET /api/mailboxes/{mailboxId}/folders/{folderId}/items/{itemId}` — Get email details
- `POST /api/mailboxes/{mailboxId}/folders/{sourceFolderId}/items/{itemId}/move` — Move an item
- `POST /api/mailboxes/{mailboxId}/folders/{folderId}/items/{itemId}/trash` — Trash an item (or `DELETE`)
- `POST /api/mailboxes/{mailboxId}/send` — Send an email (logs attempt and returns 501 / throws `NotImplementedException`)

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
- `Mailboxes--<mailbox-id>--Username` — e.g. `agent@example.com`
- `Mailboxes--<mailbox-id>--Password` — Fallback secret password or app password
- `Mailboxes--<mailbox-id>--TrashFolderName` — Optional explicit trash folder override

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

All API and MCP endpoints require an authorized API key. You can pass the key in three ways:

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
      "url": "http://localhost:5000/mcp?apiKey=your-api-key"
    }
  }
}
```

Or using custom headers if supported by the MCP client:

```json
{
  "mcpServers": {
    "email-management": {
      "url": "http://localhost:5000/mcp",
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

- [.NET 8+ SDK](https://dotnet.microsoft.com/download)
- Azure Key Vault instance (or Azure CLI logged in with permissions to a vault)

### Running Locally

```bash
# Clone the repository
git clone https://github.com/linkofdarkness/email-management-mcp.git
cd email-management-mcp

# Set your Azure Key Vault URI
export AZURE_KEYVAULT_URI="https://<your-vault-name>.vault.azure.net/"

# Run the API
dotnet run --project src/Lod.EmailManagement.Mcp
```

Navigate to `http://localhost:5000/scalar/v1` to inspect and test the REST endpoints via the interactive Scalar API reference (OpenAPI specification available at `/openapi/v1.json`).

---

## Repository Documentation

- **[REQUIREMENTS.md](REQUIREMENTS.md)**: Product Requirements Document (PRD) detailing architecture, data models, error handling, and implementation specifics.
- **[AGENTS.md](AGENTS.md)**: Index and conventions for AI agents working in this repository.
- **[.agents/skills/dotnet/](.agents/skills/dotnet/)**: C# and .NET coding standards from Lod Softworks.
