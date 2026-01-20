# SmartDocManager

A full-stack application for uploading, managing, and querying PDF documents using **AI-powered chat** with **RAG (Retrieval-Augmented Generation)** and **streaming responses**. Built with a .NET 10 backend and a React + Vite frontend.

---

## Features

### Document Management
- **Upload PDFs** — Drag-and-drop or select PDFs; text is extracted with iText7 and stored with metadata.
- **List & delete documents** — View all uploaded files with original name, size, and processing status; delete when no longer needed.
- **RAG indexing** — On upload, documents are chunked, embedded, and stored in a SQLite vector store for semantic search.

### AI Document Chat
- **Streaming chat** — Ask questions about a selected document; answers are generated in real time via **Server-Sent Events (SSE)** and rendered incrementally.
- **RAG-backed answers** — The AI receives only the **top-k relevant chunks** (from vector similarity), not the full document, for faster and more focused responses.
- **Stop generation** — Cancel an in-flight response with a Stop button; the frontend uses `AbortController` and the backend respects `CancellationToken`.
- **Copy to clipboard** — One-click copy of assistant replies.

### RAG Pipeline
- **Chunking** — Text is split with overlap (e.g. 1000 chars, 200 overlap) and stored per document.
- **Embeddings** — Configurable providers: **OpenAI** (`text-embedding-3-small`) or **Hugging Face** (e.g. `sentence-transformers/all-MiniLM-L6-v2`).
- **Vector store** — SQLite-backed store; cosine similarity is used to retrieve the most relevant chunks for each query.

### AI / LLM
- **Microsoft Semantic Kernel** — Used for prompt construction and streaming.
- **Groq (OpenAI-compatible API)** — LLM is configured to use Groq (e.g. `llama-3.3-70b-versatile`) via `OpenAIChatCompletionService` and a custom `GroqRouteHandler` that redirects requests to `api.groq.com`.

### Frontend
- **Dashboard layout** — Collapsible sidebar with document list, main chat area, and input.
- **Light/dark theme** — `ThemeContext` plus `ThemeToggle`; Tailwind `dark:` classes for styling.
- **Markdown rendering** — Assistant messages use `react-markdown` and `remark-gfm`.
- **Auto-resize textarea** — Grows with text up to a max height; scroll when beyond.
- **Suggested questions** — Shown when the conversation has only the welcome message.

---

## Project Structure

```
SmartDocManager/
├── backend/
│   ├── SmartDocManager.API/           # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   │   ├── ChatController.cs      # /api/Chat (ask, ask-stream)
│   │   │   ├── DocumentsController.cs # /api/Documents (upload, list, get, delete)
│   │   │   └── HelloWorldController.cs
│   │   ├── Program.cs
│   │   └── appsettings*.json
│   ├── SmartDocManager.Application/   # DTOs, interfaces
│   ├── SmartDocManager.Domain/        # Entities (Document, DocumentChunk, ChatMessage)
│   └── SmartDocManager.Infrastructure/
│       ├── Data/ApplicationDbContext.cs
│       ├── DependencyInjection.cs     # DI, Kernel + Groq, RAG, EF, VectorStore
│       └── Services/
│           ├── ChatService.cs         # AskQuestionAsync, AskQuestionStreamAsync
│           ├── DocumentService.cs     # Upload, CRUD, PDF extraction, RAG trigger
│           ├── RAGService.cs          # Chunking, embeddings (OpenAI/HF), retrieval
│           ├── SemanticKernelAIService.cs  # ProcessQuestionWithRAGAsync (streaming)
│           └── VectorStoreService.cs  # SQLite vector storage, cosine search
└── frontend/
    └── src/
        ├── App.jsx                    # Documents state, upload/delete/select, DashboardChatWindow
        ├── components/
        │   ├── DashboardChatWindow.jsx   # Streaming chat UI, SSE consumption
        │   ├── DashboardSidebar.jsx      # File list, upload, delete, theme
        │   └── ...
        └── contexts/ThemeContext.jsx
```

---

## Setup Instructions

### Prerequisites
- **.NET 10 SDK**
- **Node.js 18+** and npm
- **Groq API key** (for LLM)
- **Embedding provider** — either:
  - **OpenAI API key** (for `text-embedding-3-small`), or
  - **Hugging Face API key** (for `sentence-transformers/all-MiniLM-L6-v2` or similar)

### Backend

1. **Navigate to the API project:**
   ```bash
   cd backend/SmartDocManager.API
   ```

2. **Configure `appsettings.Development.json` (or `appsettings.json`):**

   - **LLM (Groq):**
     ```json
     "Groq": {
       "ApiKey": "your-groq-api-key",
       "ModelId": "llama-3.3-70b-versatile",
       "Endpoint": "https://api.groq.com/openai/v1"
     }
     ```

   - **RAG embeddings** — choose one:
     - **OpenAI:**
       ```json
       "RAG": {
         "EmbeddingProvider": "openai",
         "EmbeddingModel": "text-embedding-3-small"
       },
       "OpenAI": { "ApiKey": "your-openai-api-key" }
       ```
     - **Hugging Face:**
       ```json
       "RAG": {
         "EmbeddingProvider": "huggingface",
         "EmbeddingModel": "sentence-transformers/all-MiniLM-L6-v2"
       },
       "HuggingFace": { "ApiKey": "your-huggingface-api-key" }
       ```

   - **Database (default):**
     ```json
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=SmartDocManager.db"
     }
     ```

3. **Restore and run:**
   ```bash
   dotnet restore
   dotnet run
   ```
   API: `http://localhost:5000` (see `launchSettings.json`).

### Frontend

1. **Navigate to the frontend:**
   ```bash
   cd frontend
   ```

2. **Install and run:**
   ```bash
   npm install
   npm run dev
   ```
   App: `http://localhost:5173`. Vite can proxy `/api` to `http://localhost:5000` if configured.

### CORS
The API allows `http://localhost:5173` and `http://localhost:3000` in `Program.cs`. Adjust if you use another origin.

---

## Streaming Chat Implementation

### Overview
The chat uses **Server-Sent Events (SSE)** to stream the model output token-by-token. The backend yields text chunks from the LLM; the controller encodes them as `data: <base64>\n\n` and flushes after each chunk. The frontend consumes the `ReadableStream`, decodes base64 (or handles JSON errors), and appends each chunk to the current assistant message.

---

### Backend

#### 1. `ChatController` — `POST /api/Chat/documents/{documentId}/ask-stream`

- **Request:** `{ "question": "..." }`
- **Response:** `Content-Type: text/event-stream`, `Cache-Control: no-cache`, `Connection: keep-alive`
- **Streaming:**
  - Calls `_chatService.AskQuestionStreamAsync(documentId, question, cancellationToken)`.
  - Each `string` chunk is **Base64-encoded** to avoid newlines/special characters breaking SSE:
    - `data: <Base64(chunk)>\n\n`
  - Writes to `Response.Body` and **flushes** after each chunk so the client sees tokens as they are produced.
- **Errors:**
  - If the response has already started, errors are sent as SSE: `data: {"error":"..."}\n\n` (JSON, not base64).
  - If not started, status is set (404, 400, 500) and the same JSON `data` line is sent.
- **Cancellation:** `OperationCanceledException` is caught and the handler returns without sending an error.

#### 2. `ChatService` — `AskQuestionStreamAsync`

- **RAG retrieval:**
  - `_ragService.GetRelevantChunksAsync(question, topK: 3)` returns chunks by vector similarity.
  - If none are found, throws `InvalidOperationException` (e.g. document not yet indexed).
- **Context:** `context = string.Join("\n\n", relevantChunks.Select(c => c.Text))`
- **LLM stream:**
  - `await foreach (var chunk in _aiService.ProcessQuestionWithRAGAsync(context, question, cancellationToken))`
  - `yield return chunk` — each token or sub-token is yielded as it is produced.

#### 3. `SemanticKernelAIService` — `ProcessQuestionWithRAGAsync`

- **Prompt:** System-style instructions plus:
  - `Context:\n{context}`
  - `Question: {question}`
  - Instructions to answer only from context and to say "I don't have enough information..." when not found.
- **Streaming:** `_kernel.InvokePromptStreamingAsync(prompt, cancellationToken)` returns `IAsyncEnumerable<StreamingKernelContent>`.
- **Chunks:** `await foreach (var chunk in stream.WithCancellation(cancellationToken))` → `content = chunk.ToString()`; non-empty strings are yielded.

---

### Frontend (`DashboardChatWindow.jsx` and `ChatWindow.jsx`)

#### 1. Request and stream consumption

- **Endpoint:** `POST /api/Chat/documents/{documentId}/ask-stream` with `body: JSON.stringify({ question })`.
- **AbortController:** `signal: abortControllerRef.current.signal` — Stop button calls `abortControllerRef.current.abort()`.
- **Stream:** `const reader = response.body.getReader()` and `const decoder = new TextDecoder()`.
- **Loop:**
  - `const { done, value } = await reader.read()`
  - `buffer += decoder.decode(value, { stream: true })`
  - Split by `\n`, keep the last incomplete line in `buffer`, and process full lines.

#### 2. Parsing SSE lines

- Only lines starting with `data: ` are used; the payload is `data.slice(6).trim()`.
- **Error:** If `JSON.parse(data)` succeeds and `parsed.error` exists → `throw new Error(parsed.error)` (handled in `catch` as an error message in the UI).
- **Content:** If not JSON:
  - **Try:** `atob(data)` (base64) → treat as a text chunk.
  - **Fallback:** use `data` as plain text (for backward compatibility or malformed SSE).

#### 3. Updating the assistant message

- Before the request, a placeholder assistant message is appended: `{ id: assistantMessageId, role: 'assistant', content: '' }`.
- For each chunk:
  - `setMessages(prev => prev.map(msg => msg.id === assistantMessageId ? { ...msg, content: msg.content + chunk } : msg))`
- So the UI grows the assistant bubble incrementally as chunks arrive.

#### 4. Cleanup and errors

- **Abort:** On `error.name === 'AbortError'`, the placeholder message is removed, `isLoading` is set to `false`, and the abort controller is cleared.
- **Other errors:** The placeholder is replaced with `{ ..., content: error.message, error: true }` and shown with error styling.
- **`finally`:** `isLoading = false` and `abortControllerRef.current = null`.

#### 5. Auto-scroll and textarea

- `useEffect` on `messages`: `scrollRef.current.scrollTop = scrollRef.current.scrollHeight` so the latest content stays in view.
- `scrollRef` is attached to a scrollable `div` (`overflow-y-auto`), not a custom `ScrollArea`, to avoid ref issues.
- `textareaRef` is on a native `<textarea>`; a `useEffect` on `input` adjusts `height` and `overflowY` for auto-resize.

---

### Data flow (streaming)

```
User submits question
    → Frontend: POST /ask-stream, body { question }
    → Backend:  ChatController.AskQuestionStream
        → ChatService.AskQuestionStreamAsync
            → RAGService.GetRelevantChunksAsync(question, 3)
            → context = join(chunks)
        → SemanticKernelAIService.ProcessQuestionWithRAGAsync(context, question)
            → Kernel.InvokePromptStreamingAsync(prompt)
            → yield return chunk (per token)
    → ChatController: for each chunk
        → data: Base64(chunk)\n\n
        → Response.Body.WriteAsync + Flush
    → Frontend: response.body.getReader()
        → for each "data: ..." line: atob(payload) or payload
        → setMessages(prev => ... msg.content + chunk)
    → React re-renders; ReactMarkdown shows new content
```

---

## Configuration Summary

| Area           | Key / Section          | Purpose |
|----------------|------------------------|---------|
| **LLM**        | `Groq:ApiKey`, `ModelId`, `Endpoint` | Groq for chat (OpenAI-compatible) |
| **RAG**        | `RAG:EmbeddingProvider` | `openai` or `huggingface` |
| **RAG**        | `RAG:EmbeddingModel`   | e.g. `text-embedding-3-small` or `sentence-transformers/all-MiniLM-L6-v2` |
| **OpenAI**     | `OpenAI:ApiKey`        | Required if `EmbeddingProvider` is `openai` |
| **HuggingFace**| `HuggingFace:ApiKey`   | Required if `EmbeddingProvider` is `huggingface` |
| **DB**         | `ConnectionStrings:DefaultConnection` | SQLite file (also used for vector table) |

---

## Scripts

**Backend**
```bash
cd backend/SmartDocManager.API
dotnet run
```

**Frontend**
```bash
cd frontend
npm run dev   # dev server
npm run build # production build
npm run preview
```

---

## License

See repository license or project files.
