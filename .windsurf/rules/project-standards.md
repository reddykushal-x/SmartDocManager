---
trigger: always_on
---

# Project Standards & Planned Features

## 1. Theme Management (Planned)
- Standard: We will use Tailwind's `class` strategy for dark mode.
- Logic: The 'dark' class will be toggled on the `html` or `body` element.
- Requirement: All new UI components MUST include both light and dark variants (e.g., `bg-white dark:bg-slate-950`).
- Transitions: Use `transition-colors duration-200` for all themeable elements.

## 2. RAG Implementation (Planned)
- Current Strategy: We are moving AWAY from sending the full document text to the AI.
- Goal: Implement a chunking and retrieval system.
- Constraint: Never suggest a solution that sends more than 2,000 tokens of raw text in a single prompt. 
- Backend: All AI service calls must support `CancellationToken` to allow the user to stop long responses.

## 3. Tech Stack Consistency
- Frontend: React (Functional Components) + Tailwind CSS.
- Backend: .NET 8 / C# with Semantic Kernel.
- AI Provider: Groq (Current model: gemma2-9b-it to save on Llama-3 limits).