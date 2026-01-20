import React, { useCallback, useState } from 'react'
import { FileText, Upload, Trash2, CheckCircle, Loader2, AlertCircle, ChevronLeft, Search } from 'lucide-react'

import { Button } from './ui/Button'
import { ScrollArea } from './ui/ScrollArea'
import { Input } from './ui/Input'
import { ThemeToggle } from './ThemeToggle'
import { cn } from '../lib/utils'

/**
 * files: Array of { id, name, size, status }
 * selectedFile: same shape or null
 * onSelectFile(file)
 * onFileUpload(FileList)
 * onDeleteFile(id)
 * isOpen: boolean
 * onToggle(): void
 */
export function DashboardSidebar({
  files,
  selectedFile,
  onSelectFile,
  onFileUpload,
  onDeleteFile,
  isOpen,
  onToggle,
}) {
  const [isDragOver, setIsDragOver] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')

  const handleDragOver = useCallback((e) => {
    e.preventDefault()
    setIsDragOver(true)
  }, [])

  const handleDragLeave = useCallback((e) => {
    e.preventDefault()
    setIsDragOver(false)
  }, [])

  const handleDrop = useCallback(
    (e) => {
      e.preventDefault()
      setIsDragOver(false)
      if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
        onFileUpload(e.dataTransfer.files)
      }
    },
    [onFileUpload],
  )

  const handleFileInput = (e) => {
    if (e.target.files && e.target.files.length > 0) {
      onFileUpload(e.target.files)
    }
  }

  const filteredFiles = (files || []).filter((file) =>
    (file.name || '').toLowerCase().includes(searchQuery.toLowerCase()),
  )

  const getStatusIcon = (status) => {
    switch (status) {
      case 'ready':
        return <CheckCircle className="h-3.5 w-3.5 text-primary" />
      case 'processing':
        return <Loader2 className="h-3.5 w-3.5 text-muted-foreground animate-spin" />
      case 'error':
        return <AlertCircle className="h-3.5 w-3.5 text-destructive" />
      default:
        return null
    }
  }

  return (
    <aside
      className={cn(
        'relative h-full flex flex-col bg-zinc-950 border-r border-zinc-800 transition-all duration-300 ease-in-out',
        isOpen ? 'w-80' : 'w-0 overflow-hidden',
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between p-4 border-b border-zinc-800">
        <div className="flex items-center gap-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-500">
            <FileText className="h-4 w-4 text-white" />
          </div>
          <span className="font-semibold text-zinc-100">DocuMind AI</span>
        </div>
        <div className="flex items-center gap-1">
          <ThemeToggle />
          <Button
            variant="ghost"
            size="icon"
            onClick={onToggle}
            className="h-8 w-8 text-zinc-400 hover:bg-zinc-800"
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Upload Zone */}
      <div className="p-4">
        <div
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          className={cn(
            'relative flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-6 transition-colors cursor-pointer',
            isDragOver
              ? 'border-emerald-500 bg-emerald-500/10'
              : 'border-zinc-700 hover:border-emerald-500/50 hover:bg-zinc-900',
          )}
        >
          <input
            type="file"
            accept=".pdf"
            multiple
            onChange={handleFileInput}
            className="absolute inset-0 opacity-0 cursor-pointer"
          />
          <Upload className={cn('h-8 w-8 mb-2', isDragOver ? 'text-emerald-500' : 'text-zinc-400')} />
          <p className="text-sm font-medium text-zinc-100">Drop PDFs here</p>
          <p className="text-xs text-zinc-400 mt-1">or click to browse</p>
        </div>
      </div>

      {/* Search */}
      <div className="px-4 pb-2">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search documents..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-9 bg-zinc-900 border-zinc-700 text-zinc-100 placeholder:text-zinc-500"
          />
        </div>
      </div>

      {/* File List */}
      <div className="px-4 py-2">
        <p className="text-xs font-medium text-zinc-400 uppercase tracking-wider">
          Documents ({filteredFiles.length})
        </p>
      </div>

      <ScrollArea className="flex-1 min-h-0 overflow-y-auto px-2">
        <div className="space-y-1 pb-4">
          {filteredFiles.map((file) => (
           <div
  key={file.id}
  onClick={() => file.status === 'ready' && onSelectFile(file)}
  className={cn(
    'group flex items-center gap-2 rounded-lg px-3 py-2.5 transition-colors cursor-pointer',
    selectedFile && selectedFile.id === file.id
      ? 'bg-zinc-800 text-zinc-100'
      : 'hover:bg-zinc-900 text-zinc-100',
    file.status !== 'ready' && 'opacity-60 cursor-default',
  )}
>
  <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-zinc-800">
    <FileText className="h-4 w-4 text-zinc-300" />
  </div>

  <div className="flex-1 min-w-0 mr-2">
    <p className="text-sm font-medium break-words pr-1">{file.name}</p>
    <div className="flex items-center gap-2 mt-0.5">
      <span className="text-xs text-zinc-400 dark:text-zinc-500">{file.size}</span>
      {getStatusIcon(file.status)}
    </div>
  </div>

  <Button
    variant="ghost"
    size="icon"
    onClick={(e) => {
      e.stopPropagation()
      onDeleteFile(file.id)
    }}
    className="h-7 w-7 shrink-0 opacity-0 group-hover:opacity-100 text-zinc-400 dark:text-zinc-500 hover:text-red-400 dark:hover:text-red-400 hover:bg-zinc-100 dark:hover:bg-zinc-800"
  >
    <Trash2 className="h-3.5 w-3.5" />
  </Button>
</div>
          ))}

          {filteredFiles.length === 0 && (
            <div className="flex flex-col items-center justify-center py-8 text-center">
              <FileText className="h-10 w-10 text-zinc-600 mb-2" />
              <p className="text-sm text-zinc-400">No documents found</p>
            </div>
          )}
        </div>
      </ScrollArea>
    </aside>
  )
}

