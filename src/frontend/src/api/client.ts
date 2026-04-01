import axios from 'axios';
import type {
  PagedResult,
  SearchRequest,
  Statistics,
  SubmissionDetail,
  SubmissionSummary,
} from '../types';
import type { ChatMessage, ChatResponse } from '../types/chat';

const api = axios.create({
  baseURL: '',
});

export const submissionsApi = {
  getAll: (page = 1, pageSize = 20) =>
    api.get<PagedResult<SubmissionSummary>>(`/api/submissions?page=${page}&pageSize=${pageSize}`),

  getById: (id: number) =>
    api.get<SubmissionDetail>(`/api/submissions/${id}`),

  search: (request: SearchRequest) =>
    api.post<PagedResult<SubmissionSummary>>('/api/submissions/search', request),

  delete: (id: number) =>
    api.delete(`/api/submissions/${id}`),
};

export const uploadApi = {
  /** Individual mode: each file → its own submission */
  upload: (files: File[], onProgress?: (percent: number) => void) => {
    const formData = new FormData();
    files.forEach(f => formData.append('files', f));
    return api.post<{ fileName: string; submissionId?: number; success?: boolean; error?: string }[]>(
      '/api/upload',
      formData,
      {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: e => {
          if (onProgress && e.total) onProgress(Math.round((e.loaded * 100) / e.total));
        },
      }
    );
  },

  /** Bundle mode: all files → ONE submission; primaryFileName is the email/main doc */
  uploadBundle: (
    files: File[],
    primaryFileName: string,
    onProgress?: (percent: number) => void
  ) => {
    const formData = new FormData();
    files.forEach(f => formData.append('files', f));
    formData.append('primaryFileName', primaryFileName);
    return api.post<{
      submissionId: number;
      success: boolean;
      primaryFile: string;
      attachmentFiles: string[];
      totalFiles: number;
    }>(
      '/api/upload/bundle',
      formData,
      {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: e => {
          if (onProgress && e.total) onProgress(Math.round((e.loaded * 100) / e.total));
        },
      }
    );
  },

  getStatus: (id: number) =>
    api.get(`/api/upload/status/${id}`),
};

export const analyticsApi = {
  getStatistics: () =>
    api.get<Statistics>('/api/analytics/statistics'),
};

export const chatApi = {
  send: (messages: ChatMessage[]) =>
    api.post<ChatResponse>('/api/chat', { messages }),
};
