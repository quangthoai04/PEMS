export const errorHandler = (error: any) => {
  console.error('API Error:', error);
  return Promise.reject(error);
};
