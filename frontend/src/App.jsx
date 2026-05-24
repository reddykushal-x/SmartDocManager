import { useState, useEffect, useMemo } from 'react'
import axios from 'axios'

import { DashboardSidebar } from './components/DashboardSidebar'
import DashboardChatWindow from './components/DashboardChatWindow'

const config = {
  apiUrl: import.meta.env.VITE_API_BASE_URL
    ? `${import.meta.env.VITE_API_BASE_URL}/api`
    : 'http://localhost:5000/api'
};

const API_BASE_URL = config.apiUrl;

function App() {
  const [message, setMessage] = useState('')
  const [documents, setDocuments] = useState([])
  const [selectedDocument, setSelectedDocument] = useState(null)
  const [loading, setLoading] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(true)

  useEffect(() => {
    checkConnection()
    loadDocuments()
  }, [])

  const checkConnection = async () => {
    try {
      const response = await axios.get(`${API_BASE_URL}/HelloWorld`)
      setMessage(response.data.message)
    } catch (error) {
      console.error('Error connecting to API:', error)
      setMessage('Failed to connect to API. Make sure the backend is running.')
    }
  }

  const loadDocuments = async () => {
    try {
      setLoading(true)
      const response = await axios.get(`${API_BASE_URL}/Documents`)
      setDocuments(response.data || [])
    } catch (error) {
      console.error('Error loading documents:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleFileUpload = async (fileList) => {
    if (!fileList || fileList.length === 0) return

    const files = Array.from(fileList)
    try {
      setLoading(true)

      await Promise.all(
        files.map(async (file) => {
          const formData = new FormData()
          formData.append('file', file)

          await axios.post(`${API_BASE_URL}/Documents/upload`, formData, {
            headers: {
              'Content-Type': 'multipart/form-data',
            },
          })
        }),
      )

      await loadDocuments()
    } catch (error) {
      console.error('Upload error:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleDeleteFile = async (id) => {
    try {
      await axios.delete(`${API_BASE_URL}/Documents/${id}`)
      if (selectedDocument && selectedDocument.id === id) {
        setSelectedDocument(null)
      }
      await loadDocuments()
    } catch (error) {
      console.error('Error deleting document:', error)
    }
  }

  const handleDocumentSelect = (file) => {
    if (!file) {
      setSelectedDocument(null)
      return
    }

    const doc = documents.find((d) => d.id === file.id)
    setSelectedDocument(doc || null)
  }

  const sidebarFiles = useMemo(
    () =>
      (documents || []).map((doc) => ({
        id: doc.id,
        name: doc.originalFileName || `Document ${doc.id}`,
        size:
          typeof doc.fileSize === 'number'
            ? `${(doc.fileSize / 1024 / 1024).toFixed(1)} MB`
            : 'Unknown size',
        status: doc.processedAt ? 'ready' : loading ? 'processing' : 'ready',
      })),
    [documents, loading],
  )

  const selectedFile = useMemo(() => {
    if (!selectedDocument) return null
    return (
      sidebarFiles.find((f) => f.id === selectedDocument.id) || {
        id: selectedDocument.id,
        name: selectedDocument.originalFileName || `Document ${selectedDocument.id}`,
        size:
          typeof selectedDocument.fileSize === 'number'
            ? `${(selectedDocument.fileSize / 1024 / 1024).toFixed(1)} MB`
            : 'Unknown size',
        status: selectedDocument.processedAt ? 'ready' : 'ready',
      }
    )
  }, [selectedDocument, sidebarFiles])

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-white dark:bg-zinc-950 transition-colors duration-200">
      <div className="h-full">
        <DashboardSidebar
          files={sidebarFiles}
          selectedFile={selectedFile}
          onSelectFile={handleDocumentSelect}
          onFileUpload={handleFileUpload}
          onDeleteFile={handleDeleteFile}
          isOpen={sidebarOpen}
          onToggle={() => setSidebarOpen((prev) => !prev)}
        />
      </div>
      <div className="flex-1 h-full overflow-hidden">
        <DashboardChatWindow
          selectedFile={selectedFile}
          sidebarOpen={sidebarOpen}
          onToggleSidebar={() => setSidebarOpen((prev) => !prev)}
        />
      </div>
    </div>
  )
}

export default App