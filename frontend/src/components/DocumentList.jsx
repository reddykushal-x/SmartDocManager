function DocumentList({ documents, loading, onSelect, selectedDocument }) {
  if (loading) {
    return (
      <div className="bg-white rounded-lg shadow-md p-6">
        <h2 className="text-2xl font-semibold text-gray-800 mb-4">
          Documents
        </h2>
        <div className="text-center py-8">
          <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
          <p className="mt-2 text-gray-600">Loading documents...</p>
        </div>
      </div>
    )
  }

  return (
    <div className="bg-white rounded-lg shadow-md p-6">
      <h2 className="text-2xl font-semibold text-gray-800 mb-4">
        Documents ({documents.length})
      </h2>
      {documents.length === 0 ? (
        <div className="text-center py-8 text-gray-500">
          <p>No documents uploaded yet.</p>
          <p className="text-sm mt-2">Upload a PDF to get started.</p>
        </div>
      ) : (
        <div className="space-y-2 max-h-96 overflow-y-auto">
          {documents.map((doc) => (
            <div
              key={doc.id}
              onClick={() => onSelect(doc)}
              className={`p-4 rounded-lg border-2 cursor-pointer transition-all ${
                selectedDocument?.id === doc.id
                  ? 'border-indigo-600 bg-indigo-50'
                  : 'border-gray-200 hover:border-indigo-300 hover:bg-gray-50'
              }`}
            >
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <h3 className="font-semibold text-gray-800 truncate">
                    {doc.originalFileName}
                  </h3>
                  <div className="text-sm text-gray-500 mt-1">
                    <span>{(doc.fileSize / 1024).toFixed(2)} KB</span>
                    <span className="mx-2">•</span>
                    <span>
                      {new Date(doc.uploadedAt).toLocaleDateString()}
                    </span>
                  </div>
                </div>
                {doc.processedAt && (
                  <span className="ml-2 px-2 py-1 text-xs bg-green-100 text-green-700 rounded-full">
                    Processed
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

export default DocumentList
