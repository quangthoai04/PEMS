import { useState, useEffect } from 'react';
import { documentsApi } from '../api/documentsApi';
import { DocumentFilterParams, DocumentListItem, PaginatedResponse } from '../types/documents.types';

export const useDocuments = (params: DocumentFilterParams) => {
  const [data, setData] = useState<PaginatedResponse<DocumentListItem> | undefined>(undefined);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isError, setIsError] = useState<boolean>(false);

  useEffect(() => {
    let isMounted = true;
    setIsLoading(true);
    setIsError(false);

    documentsApi.getDocuments(params)
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
  }, [
    params.q,
    params.status,
    params.ownerType,
    params.documentCategory,
    params.storageProvider,
    params.uploadedFrom,
    params.uploadedTo,
    params.page,
    params.pageSize,
    params.sortBy,
    params.sortDir
  ]);

  return { data, isLoading, isError };
};
