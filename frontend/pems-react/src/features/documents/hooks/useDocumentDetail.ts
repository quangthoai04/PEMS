import { useState, useEffect } from 'react';
import { documentsApi } from '../api/documentsApi';
import { DocumentDetailResponse } from '../types/documents.types';

export const useDocumentDetail = (documentId: number | null) => {
  const [data, setData] = useState<DocumentDetailResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isError, setIsError] = useState<boolean>(false);

  useEffect(() => {
    let isMounted = true;
    
    if (!documentId) {
      setData(null);
      return;
    }

    setIsLoading(true);
    setIsError(false);

    documentsApi.getDocument(documentId)
      .then((response) => {
        if (isMounted) {
          setData(response);
          setIsLoading(false);
        }
      })
      .catch(() => {
        if (isMounted) {
          setIsError(true);
          setIsLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [documentId]);

  return { data, isLoading, isError };
};
