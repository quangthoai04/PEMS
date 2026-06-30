export type DocumentStatus = 'DRAFT' | 'PUBLISHED' | 'ARCHIVED';
export type DocumentOwnerType = 'GENERAL' | 'VISIT' | 'PARTNER' | 'MINUTES' | 'NEWS' | 'LOGISTICS' | 'REPORT';
export type StorageProvider = 'LOCAL' | 'S3' | 'AZURE' | 'GCS' | 'GOOGLE_DRIVE' | 'OTHER';

export interface DocumentListItem {
  documentId: number;
  title: string;
  description?: string;
  documentCategory?: string;
  status: DocumentStatus;
  ownerType: DocumentOwnerType;
  ownerId?: number;
  ownerDisplayName?: string;
  campusId?: number;
  createdAt: string;
  updatedAt?: string;

  // File details
  fileId?: number;
  originalFilename?: string;
  mimeType?: string;
  fileSize?: number;
  storageProvider?: StorageProvider;
  downloadUrl?: string;
  webViewUrl?: string;
  thumbnailUrl?: string;
}

export interface DocumentFilterParams {
  q?: string;
  status?: DocumentStatus | 'All';
  ownerType?: DocumentOwnerType | 'All';
  documentCategory?: string;
  uploadedFrom?: string;
  uploadedTo?: string;
  storageProvider?: StorageProvider | 'All';
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface UserSummary {
  userId: number;
  fullName: string;
  email?: string;
  avatarUrl?: string;
}

export interface CampusSummary {
  campusId: number;
  campusCode: string;
  name: string;
}

export interface DocumentDetailResponse {
  document: {
    documentId: number;
    fileId: number;
    ownerType: DocumentOwnerType;
    ownerId?: number;
    campusId?: number;
    title: string;
    description?: string;
    documentCategory?: string;
    status: DocumentStatus;
    createdAt: string;
    createdBy?: number;
    updatedAt?: string;
    updatedBy?: number;
  };
  file: {
    fileId: number;
    storageProvider: StorageProvider;
    bucketName?: string;
    objectKey?: string;
    originalFilename: string;
    mimeType?: string;
    fileSize?: number;
    checksumSha256?: string;
    uploadedBy?: number;
    uploadedAt?: string;
    externalFileId?: string;
    webViewUrl?: string;
    downloadUrl?: string;
    thumbnailUrl?: string;
    filePurpose?: string;
  };
  campus?: CampusSummary;
  createdByUser?: UserSummary;
  updatedByUser?: UserSummary;
  uploadedByUser?: UserSummary;
  ownerContext: any;
}

