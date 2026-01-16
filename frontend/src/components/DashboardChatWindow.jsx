import React, { useState, useRef, useEffect } from 'react'
import { Send, Sparkles, Menu, FileText, Copy, Check } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

import { Button } from './ui/Button'
import { Textarea } from './ui/Textarea'
import { ScrollArea } from './ui/ScrollArea'
import { cn } from '../lib/utils'

const API_BASE_URL = 'http://localhost:5000/api'

function DashboardChatWindow({ selectedFile, sidebarOpen, onToggleSidebar }) {
  const [messages, setMessages] = useState([])
  const [input, setInput] = useState('')
  const [isLoading, setIsLoading] = useState(false)
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
    if (selectedFile) {
      setMessages([
        {
          id: 'welcome',
          role: 'assistant',
          content: `I've loaded **${selectedFile.name}**. I can help you analyze this document, answer questions about its contents, summarize sections, or extract specific information. What would you like to know?`,
          timestamp: new Date(),
        },
      ])
    } else {
      setMessages([])
    }
  }, [selectedFile])

  const handleSend = async () => {
    if (!input.trim() || !selectedFile || isLoading) return

    // Cancel any ongoing request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort()
    }

    const userMessage = {
      id: Date.now().toString(),
      role: 'user',
      content: input.trim(),
      timestamp: new Date(),
    }

    setMessages((prev) => [...prev, userMessage])
    const question = input.trim()
    setInput('')
    setIsLoading(true)

    // Create a placeholder message for streaming
    const assistantMessageId = (Date.now() + 1).toString()
    const assistantMessage = {
      id: assistantMessageId,
      role: 'assistant',
      content: '',
      timestamp: new Date(),
    }
    setMessages((prev) => [...prev, assistantMessage])

    // Create new AbortController for this request
    abortControllerRef.current = new AbortController()

    try {
      const response = await fetch(
        `${API_BASE_URL}/Chat/documents/${selectedFile.id}/ask-stream`,
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
                      ? { ...msg, content: msg.content + chunk }
                      : msg
                  )
                )
              } catch (decodeError) {
                // Not base64 either, treat as plain text
                setMessages((prev) =>
                  prev.map((msg) =>
                    msg.id === assistantMessageId
                      ? { ...msg, content: msg.content + data }
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
        setIsLoading(false)
        abortControllerRef.current = null
        return
      }
      console.error('Chat error:', error)
      const errorMessage = {
        id: assistantMessageId,
        role: 'assistant',
        content: error.message || 'Sorry, I encountered an error. Please try again.',
        timestamp: new Date(),
        error: true,
      }
      setMessages((prev) =>
        prev.map((msg) =>
          msg.id === assistantMessageId ? errorMessage : msg
        )
      )
    } finally {
      // Always set loading to false when done (idempotent if already set)
      setIsLoading(false)
      abortControllerRef.current = null
    }
  }

  const handleStop = () => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort()
      abortControllerRef.current = null
      setIsLoading(false)
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
    <main className="flex flex-col h-full min-w-0 overflow-hidden bg-zinc-900">
      {/* Header */}
      <header className="flex items-center gap-3 px-4 py-3 border-b border-zinc-800 bg-zinc-900">
        {!sidebarOpen && (
          <Button variant="ghost" size="icon" onClick={onToggleSidebar} className="h-8 w-8 text-zinc-400 hover:bg-zinc-800">
            <Menu className="h-4 w-4" />
          </Button>
        )}
        <div className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-emerald-500" />
          <h1 className="font-semibold text-zinc-100">AI Document Chat</h1>
        </div>
        {selectedFile && (
          <div className="ml-auto flex items-center gap-2 px-3 py-1.5 rounded-full bg-zinc-800 text-zinc-300">
            <FileText className="h-3.5 w-3.5" />
            <span className="text-xs font-medium truncate max-w-[200px]">{selectedFile.name}</span>
          </div>
        )}
      </header>

      {/* Chat Area */}
      {selectedFile ? (
        <>
          <ScrollArea className="flex-1 p-4 min-h-0 bg-zinc-900" ref={scrollRef}>
            <div className="max-w-3xl mx-auto space-y-6">
              {messages.map((message) => (
                <div
                  key={message.id}
                  className={cn('flex gap-3', message.role === 'user' ? 'justify-end' : 'justify-start')}
                >
                  {message.role === 'assistant' && (
                    <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-zinc-800">
                      <Sparkles className="h-4 w-4 text-emerald-500" />
                    </div>
                  )}
                  <div
                    className={cn(
                      'group relative max-w-[85%] rounded-2xl px-4 py-3',
                      message.role === 'user'
                        ? 'bg-zinc-700 text-zinc-100'
                        : message.error
                        ? 'bg-red-900/30 border border-red-800 text-red-300'
                        : 'bg-zinc-800 border border-zinc-700 text-zinc-100',
                    )}
                  >
                    <div className="text-sm leading-7 prose prose-sm prose-invert prose-p:my-1 prose-ul:my-1 prose-li:my-0.5 max-w-none">
                      {message.error ? (
                        <div className="whitespace-pre-wrap">{message.content}</div>
                      ) : (
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>
                          {message.content || ''}
                        </ReactMarkdown>
                      )}
                    </div>
                    {message.role === 'assistant' && (
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => handleCopy(message.content, message.id)}
                        className="absolute -right-2 -top-2 h-7 w-7 opacity-0 group-hover:opacity-100 bg-zinc-700 border border-zinc-600 text-zinc-300 hover:text-zinc-100 hover:bg-zinc-600"
                      >
                        {copiedId === message.id ? (
                          <Check className="h-3 w-3 text-emerald-500" />
                        ) : (
                          <Copy className="h-3 w-3" />
                        )}
                      </Button>
                    )}
                  </div>
                </div>
              ))}

              {isLoading && (
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
            </div>
          </ScrollArea>

          {/* Suggested Questions */}
          {messages.length <= 1 && (
            <div className="px-4 pb-2">
              <div className="max-w-3xl mx-auto">
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
            </div>
          )}

          {/* Input Area */}
          <div className="p-4 border-t border-zinc-800 bg-zinc-900 shrink-0">
            <div className="max-w-3xl mx-auto">
              <div className="relative flex items-end gap-2 rounded-xl border border-zinc-700 bg-zinc-800 p-2 focus-within:border-emerald-500/50 transition-colors">
                <Textarea
                  ref={textareaRef}
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={handleKeyDown}
                  placeholder="Ask a question about your document..."
                  className="min-h-[44px] max-h-[200px] resize-none border-0 bg-transparent p-2 focus-visible:ring-0 focus-visible:ring-offset-0 flex-1 text-zinc-100 placeholder:text-zinc-500"
                  rows={1}
                />
                {isLoading ? (
                  <Button
                    onClick={handleStop}
                    size="icon"
                    className="h-10 w-10 shrink-0 rounded-lg bg-red-600 hover:bg-red-700"
                  >
                    <span className="text-xs">Stop</span>
                  </Button>
                ) : (
                  <Button
                    onClick={handleSend}
                    disabled={!input.trim()}
                    size="icon"
                    className="h-10 w-10 shrink-0 rounded-lg bg-emerald-500 hover:bg-emerald-600 disabled:bg-zinc-700 disabled:text-zinc-500"
                  >
                    <Send className="h-4 w-4" />
                  </Button>
                )}
              </div>
              <p className="text-xs text-zinc-400 text-center mt-2">
                AI responses are generated based on document analysis
              </p>
            </div>
          </div>
        </>
      ) : (
        /* Empty State */
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

export default DashboardChatWindow

