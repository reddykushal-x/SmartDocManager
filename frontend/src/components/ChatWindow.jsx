import { useState, useRef, useEffect } from 'react'
import { Send, Square, Sparkles, FileText, Copy, Check } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { Button } from './ui/Button'
import { cn } from '../lib/utils'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL
  ? `${import.meta.env.VITE_API_BASE_URL}/api`
  : 'http://localhost:5000/api';

function ChatWindow({ document, onDocumentChange }) {
  const [messages, setMessages] = useState([])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [copiedId, setCopiedId] = useState(null)
  const scrollRef = useRef(null)
  const textareaRef = useRef(null)
  const abortControllerRef = useRef(null)

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight
    }
  }, [messages])

  useEffect(() => {
    if (document) {
      setMessages([
        {
          id: 'welcome',
          type: 'assistant',
          text: `I've loaded **${document.originalFileName}**. I can help you analyze this document, answer questions about its contents, summarize sections, or extract specific information. What would you like to know?`,
        },
      ])
    } else {
      setMessages([])
    }
  }, [document])

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto'
      textareaRef.current.style.height = `${Math.min(textareaRef.current.scrollHeight, 150)}px`
    }
  }, [input])

  const handleSend = async () => {
    if (!input.trim() || !document || loading) return

    // Cancel any ongoing request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort()
    }

    const userMessage = {
      id: Date.now().toString(),
      type: 'user',
      text: input.trim(),
    }
    setMessages((prev) => [...prev, userMessage])
    const question = input.trim()
    setInput('')
    setLoading(true)

    // Create a placeholder message for streaming
    const assistantMessageId = (Date.now() + 1).toString()
    const assistantMessage = {
      id: assistantMessageId,
      type: 'assistant',
      text: '',
    }
    setMessages((prev) => [...prev, assistantMessage])

    // Create new AbortController for this request
    abortControllerRef.current = new AbortController()

    try {
      const response = await fetch(
        `${API_BASE_URL}/Chat/documents/${document.id}/ask-stream`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ question }),
          signal: abortControllerRef.current.signal,
        }
      )

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }

      const reader = response.body.getReader()
      const decoder = new TextDecoder()
      let buffer = ''

      while (true) {
        const { done, value } = await reader.read()
        if (done) break

        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() || ''

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6).trim()
            if (!data) continue
            
            // Try to parse as JSON first (for errors)
            let parsed = null
            try {
              parsed = JSON.parse(data)
              if (parsed.error) {
                throw new Error(parsed.error)
              }
            } catch (e) {
              if (e instanceof Error && parsed?.error) {
                // This was a JSON error object
                throw e
              }
              // Not JSON, try to decode as base64, fallback to plain text
              try {
                const chunk = atob(data)
                // Update the assistant message incrementally
                setMessages((prev) =>
                  prev.map((msg) =>
                    msg.id === assistantMessageId
                      ? { ...msg, text: msg.text + chunk }
                      : msg
                  )
                )
              } catch (decodeError) {
                // Not base64 either, treat as plain text
                setMessages((prev) =>
                  prev.map((msg) =>
                    msg.id === assistantMessageId
                      ? { ...msg, text: msg.text + data }
                      : msg
                  )
                )
              }
            }
          }
        }
      }
    } catch (error) {
      if (error.name === 'AbortError') {
        // Request was cancelled, remove the incomplete message
        setMessages((prev) => prev.filter((msg) => msg.id !== assistantMessageId))
        setLoading(false)
        abortControllerRef.current = null
        return
      }
      console.error('Chat error:', error)
      const errorMessage = {
        id: assistantMessageId,
        type: 'assistant',
        text: error.message || 'Sorry, I encountered an error. Please try again.',
        error: true,
      }
      setMessages((prev) =>
        prev.map((msg) =>
          msg.id === assistantMessageId ? errorMessage : msg
        )
      )
    } finally {
      // Always set loading to false when done (idempotent if already set)
      setLoading(false)
      abortControllerRef.current = null
    }
  }

  const handleStop = () => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort()
      abortControllerRef.current = null
      setLoading(false)
    }
  }

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  const handleCopy = async (content, id) => {
    await navigator.clipboard.writeText(content)
    setCopiedId(id)
    setTimeout(() => setCopiedId(null), 2000)
  }

  const suggestedQuestions = [
    'Summarize the key findings',
    'What are the main recommendations?',
    'Extract all numerical data',
    'List the key stakeholders mentioned',
  ]

  return (
    <main className="flex flex-col h-full min-w-0 overflow-hidden">
      <header className="shrink-0 flex items-center gap-3 px-4 py-3 border-b border-zinc-800 bg-zinc-900">
        <div className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-emerald-500" />
          <h1 className="font-semibold text-zinc-100">Document Chat</h1>
        </div>
        {document && (
          <div className="ml-auto flex items-center gap-2 px-3 py-1.5 rounded-full bg-zinc-800 text-zinc-300">
            <FileText className="h-3.5 w-3.5" />
            <span className="text-xs font-medium truncate max-w-[200px]">{document.originalFileName}</span>
          </div>
        )}
      </header>

      {document ? (
        <>
          <div ref={scrollRef} className="flex-1 min-h-0 overflow-y-auto bg-zinc-900 p-4">
            <div className="max-w-3xl mx-auto space-y-6">
            {messages.map((message) => (
  <div
    key={message.id || message.type}
    className={cn('flex gap-3', message.type === 'user' ? 'justify-end' : 'justify-start')}
  >
    {message.type === 'assistant' && (
      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-zinc-800">
        <Sparkles className="h-4 w-4 text-emerald-500" />
      </div>
    )}
    <div
      className={cn(
        /* ADDED 'relative' HERE: This anchors the button to the bubble */
        'group relative max-w-[85%] rounded-2xl px-4 py-3',
        message.type === 'user'
          ? 'bg-red-900/30 border border-red-800 text-red-300'
          : message.error
          ? 'bg-red-900/30 border border-red-800 text-red-300'
          : 'bg-red-900/30 border border-red-800 text-red-300'
      )}
    >
      {message.type === 'assistant' && !message.error ? (
        <>
          {/* THE BUTTON: Changed to absolute to float inside the corner */}
          <Button
            variant="ghost"
            size="icon"
            onClick={() => handleCopy(message.text, message.id)}
            className={cn(
              /* 'absolute right-2 top-2' moves it exactly where you wanted */
              "absolute right-2 top-2 z-10 h-7 w-7", 
              "opacity-0 group-hover:opacity-100",
              "bg-zinc-900/90 border border-zinc-700 shadow-xl",
              "text-zinc-300 hover:text-zinc-100",
              "transition-all"
            )}
          >
            {copiedId === message.id ? (
              <Check className="h-3 w-3 text-emerald-500" />
            ) : (
              <Copy className="h-3 w-3" />
            )}
          </Button>

          {/* THE TEXT: pr-8 so text doesn't overlap the absolute-positioned Copy button */}
          <div className="min-w-0 w-full overflow-hidden whitespace-normal break-words pr-8">
            <div className="text-sm leading-7 prose prose-sm prose-invert prose-p:my-1 prose-ul:my-1 prose-li:my-0.5 max-w-none">
              <ReactMarkdown remarkPlugins={[remarkGfm]}>
                {message.text || ''}
              </ReactMarkdown>
            </div>
          </div>
        </>
      ) : (
        <div className="text-sm leading-7 prose prose-sm prose-invert max-w-none">
          <div className="whitespace-pre-wrap">{message.text}</div>
        </div>
      )}
    </div>
  </div>
))}

              {loading && (
                <div className="flex gap-3">
                  <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-zinc-800">
                    <Sparkles className="h-4 w-4 text-emerald-500" />
                  </div>
                  <div className="bg-zinc-800 border border-zinc-700 rounded-2xl px-4 py-3">
                    <div className="flex gap-1">
                      <span className="h-2 w-2 rounded-full bg-zinc-500 animate-bounce [animation-delay:-0.3s]" />
                      <span className="h-2 w-2 rounded-full bg-zinc-500 animate-bounce [animation-delay:-0.15s]" />
                      <span className="h-2 w-2 rounded-full bg-zinc-500 animate-bounce" />
                    </div>
                  </div>
                </div>
              )}

              {messages.length <= 1 && (
                <div className="pt-4">
                  <p className="text-xs text-zinc-400 mb-2">Suggested questions</p>
                  <div className="flex flex-wrap gap-2">
                    {suggestedQuestions.map((question) => (
                      <button
                        key={question}
                        onClick={() => setInput(question)}
                        className="px-3 py-1.5 text-xs rounded-full border border-zinc-700 bg-zinc-800 text-zinc-300 hover:bg-zinc-700 hover:border-emerald-500 transition-colors"
                      >
                        {question}
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className="shrink-0 p-4 border-t border-zinc-800 bg-zinc-900">
            <div className="max-w-3xl mx-auto">
              <div className="flex items-end gap-3">
                <div className="flex-1 relative">
                <textarea
  id="chat-input"
  name="chat-message"
  ref={textareaRef}
  value={input}
  onChange={(e) => setInput(e.target.value)}
  onKeyDown={handleKeyDown}
  placeholder="Ask a question about your document..."
  className="w-full min-h-[48px] max-h-[150px] px-4 py-3 rounded-xl bg-zinc-800 text-zinc-100 placeholder:text-zinc-500 resize-none 
             border-none focus:border-none 
             outline-none focus:outline-none 
             ring-0 focus:ring-0 focus:ring-offset-0 focus:ring-transparent"
  rows={1}
/>
                </div>
                <Button
                  onClick={loading ? handleStop : handleSend}
                  disabled={!loading && !input.trim()}
                  size="icon"
                  className={cn(
                    'h-12 w-12 shrink-0 rounded-xl transition-colors',
                    loading
                      ? 'bg-red-600 hover:bg-red-700 text-white'
                      : 'bg-emerald-500 hover:bg-emerald-600 text-white disabled:bg-zinc-700 disabled:text-zinc-500',
                  )}
                >
                  {loading ? <Square className="h-4 w-4 fill-current" /> : <Send className="h-4 w-4" />}
                </Button>
              </div>
              <p className="text-xs text-zinc-400 text-center mt-2">
                AI responses are generated based on document analysis
              </p>
            </div>
          </div>
        </>
      ) : (
        <div className="flex-1 flex items-center justify-center p-8 bg-zinc-900">
          <div className="text-center max-w-md">
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-zinc-800 mx-auto mb-4">
              <FileText className="h-8 w-8 text-emerald-500" />
            </div>
            <h2 className="text-xl font-semibold text-zinc-100 mb-2">No document selected</h2>
            <p className="text-zinc-400">
              Upload a PDF document or select one from the sidebar to start analyzing with AI.
            </p>
          </div>
        </div>
      )}
    </main>
  )
}

export default ChatWindow
