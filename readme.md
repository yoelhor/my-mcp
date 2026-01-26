# My MCP demo (anonymous)

## MCP settings

The [mcp.json](./.vscode/mcp.json) file defines and configures one or more MCP servers that a client application (the AI host), like GitHub Copilot connects to for testing purposes.

## Code

This project is based on the official [ASP.NET Core extensions for the MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/src/ModelContextProtocol.AspNetCore) sample.

### .NET SDK for the Model Context Protocol

This demo is build using the [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/). This package builds on the [base package](https://www.nuget.org/packages/ModelContextProtocol/) by adding the necessary components to run an MCP server within a full ASP.NET Core web application. It enables remote communication using **Streamable HTTP Transport**. For more information, check the [API reference](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.html)

## Add the MCP tool to an agent

The easiest current method to add the Microsoft Copilot (MCP) service to an agent is by editing the Azure AI agent directly within the [designer view](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/vs-code-agents?context=%2Fazure%2Fai-services%2Fagents%2Fcontext%2Fcontext&tabs=windows-powershell&pivots=python) of the VS Code extension. This approach eliminates the need to run any code.

