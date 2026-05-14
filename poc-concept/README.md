# Chatbot PoC - Concept Document

## Problem Statement
_Describe the business problem the chatbot addresses._

## Target Users
_Who will use this chatbot? What is their role and technical level?_

## Expected Value
_What outcome does the chatbot deliver? Time saved, errors reduced, faster access to information?_

## Data Sources
| Source | Type | Description |
|--------|------|-------------|
| _e.g. Internal knowledge base_ | PDF / Unstructured | _Brief description_ |
| _e.g. Product database_ | SQL / Structured | _Brief description_ |

## AI Capabilities Required
- Retrieval-Augmented Generation (RAG) over documents
- Structured data querying (Text-to-SQL or CosmosDB queries)
- Conversational memory (multi-turn)

## Tooling & Architecture
| Component | Technology |
|-----------|-----------|
| LLM | Azure OpenAI / EPAM DIAL |
| AI Platform | Azure AI Foundry |
| App Framework | .NET 8 |
| Database | SQL Server / Azure CosmosDB |
| Document Store | Azure AI Search |

## Risks & Assumptions
- _List key assumptions and potential blockers_
