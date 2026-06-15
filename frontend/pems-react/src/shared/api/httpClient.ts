import axios from 'axios';
import { authInterceptor } from './authInterceptor';
import { errorHandler } from './errorHandler';

const httpClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

httpClient.interceptors.request.use(authInterceptor);
httpClient.interceptors.response.use((response) => response, errorHandler);

export default httpClient;
