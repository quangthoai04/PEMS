import { useState, useCallback } from 'react';
import { accountManagementApi } from '../api/accountManagementApi';
import { getAuthErrorMessage } from '../../authentication/api/authError';
import type {
  AccountDetails,
  CreateAccountRequest,
  CreateAccountResponse,
  ManageAccountStatusRequest,
  ManageAccountStatusResponse,
  UpdateAccountRoleRequest,
  UpdateAccountRoleResponse,
} from '../types/accountManagement.types';

/**
 * Hook exposing account-management mutations (create account, update role) with
 * loading/error state, ready to wire into the Account Management UI.
 */
export const useAccountManagement = () => {
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const createAccount = useCallback(async (payload: CreateAccountRequest): Promise<CreateAccountResponse | null> => {
    setSubmitting(true);
    setError(null);
    try {
      return await accountManagementApi.createAccount(payload);
    } catch (err) {
      setError(getAuthErrorMessage(err, 'Không thể tạo tài khoản. Vui lòng thử lại.'));
      return null;
    } finally {
      setSubmitting(false);
    }
  }, []);

  const updateAccountRole = useCallback(async (payload: UpdateAccountRoleRequest): Promise<UpdateAccountRoleResponse | null> => {
    setSubmitting(true);
    setError(null);
    try {
      return await accountManagementApi.updateAccountRole(payload);
    } catch (err) {
      setError(getAuthErrorMessage(err, 'Không thể cập nhật vai trò. Vui lòng thử lại.'));
      return null;
    } finally {
      setSubmitting(false);
    }
  }, []);

  const manageAccountStatus = useCallback(
    async (payload: ManageAccountStatusRequest): Promise<ManageAccountStatusResponse | null> => {
      setSubmitting(true);
      setError(null);
      try {
        return await accountManagementApi.manageAccountStatus(payload);
      } catch (err) {
        setError(getAuthErrorMessage(err, 'Không thể cập nhật trạng thái tài khoản. Vui lòng thử lại.'));
        return null;
      } finally {
        setSubmitting(false);
      }
    },
    [],
  );

  const getAccountDetails = useCallback(
    async (userId: string | number): Promise<AccountDetails | null> => {
      setError(null);
      try {
        return await accountManagementApi.getAccountDetails(userId);
      } catch (err) {
        setError(getAuthErrorMessage(err, 'Không thể tải chi tiết tài khoản. Vui lòng thử lại.'));
        return null;
      }
    },
    [],
  );

  return { submitting, error, setError, createAccount, updateAccountRole, manageAccountStatus, getAccountDetails };
};
