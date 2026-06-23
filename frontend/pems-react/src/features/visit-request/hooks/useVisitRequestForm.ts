import { useState, useEffect, useRef, useCallback } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { visitRequestSchema, type VisitRequestSchema } from '../schema/visitRequest.schema';
import { visitRequestApi, type VerifyResponse } from '../api/visitRequestApi';

const DEFAULT_VISITOR = {
  fullName: '',
  jobTitle: '',
  organization: '',
  nationality: '',
};

const DEFAULT_SUPPORT = {
  fullName: '',
  jobTitle: '',
  organization: '',
  nationality: '',
};

export const DEFAULT_VISIT_REQUEST_VALUES: VisitRequestSchema = {
  registerInfo: { fullName: '', organization: '', jobTitle: '', phone: '', email: '', nationality: '' },
  delegationName: '',
  visitMode: 'single',
  visitType: 'CAMPUS_TOUR',
  visitTypeOther: '',
  visits: [{ campus: 'HN', startDatetime: '', endDatetime: '' }],
  purpose: '',
  workingContent: '',
  visitors: [{ ...DEFAULT_VISITOR }],
  supportTeam: [],
  contactPoint: { fullName: '', organization: '', phone: '', email: '' },
  workingLanguage: 'VI',
  transportationType: 'SELF_ARRANGED',
  transportationDetail: '',
  mediaConsentStatus: 'DECLINED',
  mediaConsentNote: '',
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  notes: '',
  timeOverlapConfirmed: false,
};

import axios from 'axios';

function getApiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as any;

    if (typeof data?.message === 'string' && data.message.trim()) {
      return data.message;
    }

    if (data?.errors) {
      const values = Object.values(data.errors);
      const first = values.flat?.()[0];
      if (typeof first === 'string') return first;
    }

    if (typeof data?.errorCode === 'string') {
      return data.errorCode;
    }
  }

  return 'Có lỗi xảy ra khi gửi đơn. Vui lòng thử lại.';
}

/** Machine-readable backend error code (response.errorCode), if present. */
function getApiErrorCode(error: unknown): string | null {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as any;
    if (typeof data?.errorCode === 'string' && data.errorCode.trim()) {
      return data.errorCode;
    }
  }
  return null;
}

const CONTACT_EMAIL_CONFLICT = 'CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT';
const VISITOR_ACCOUNT_INACTIVE = 'VISITOR_ACCOUNT_INACTIVE';

import { saveVisitRequestDraft, loadVisitRequestDraft, clearVisitRequestDraft } from '../utils/visitRequestDraftStorage';

function debounce<T extends (...args: any[]) => void>(fn: T, delay = 700) {
  let timer: ReturnType<typeof setTimeout> | null = null;
  return (...args: Parameters<T>) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => {
      fn(...args);
    }, delay);
  };
}

export const useVisitRequestForm = (onSuccess: (result: VerifyResponse) => void) => {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // OTP phase state
  const [sessionToken, setSessionToken] = useState<string | null>(null);
  const [maskedEmail, setMaskedEmail] = useState<string>('');
  const [otpError, setOtpError] = useState<string | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isResending, setIsResending] = useState(false);

  const [draftHydrated, setDraftHydrated] = useState(false);
  const isRestoringDraftRef = useRef(false);

  const form = useForm<VisitRequestSchema>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(visitRequestSchema) as any,
    mode: 'onBlur',
    reValidateMode: 'onChange',
    defaultValues: DEFAULT_VISIT_REQUEST_VALUES,
  });

  useEffect(() => {
    const draft = loadVisitRequestDraft();
    if (draft?.data) {
      isRestoringDraftRef.current = true;
      form.reset({
        ...DEFAULT_VISIT_REQUEST_VALUES,
        ...draft.data,
      });
      setTimeout(() => {
        isRestoringDraftRef.current = false;
        setDraftHydrated(true);
      }, 100);
    } else {
      setDraftHydrated(true);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!draftHydrated) return;

    const debouncedSave = debounce((value: Partial<VisitRequestSchema>) => {
      if (isRestoringDraftRef.current) return;
      saveVisitRequestDraft(value);
    }, 700);

    const subscription = form.watch((value) => {
      debouncedSave(value as Partial<VisitRequestSchema>);
    });

    return () => subscription.unsubscribe();
  }, [form, draftHydrated]);

  const visitFields = useFieldArray({ control: form.control, name: 'visits' });
  const visitorFields = useFieldArray({ control: form.control, name: 'visitors' });
  const supportTeamFields = useFieldArray({ control: form.control, name: 'supportTeam' });

  const syncSupportFromRegister = () => {
    const reg = form.getValues('registerInfo');
    const currentTeam = form.getValues('supportTeam');
    form.setValue('supportTeam', [
      { fullName: reg.fullName, jobTitle: reg.jobTitle, organization: reg.organization, nationality: reg.nationality },
      ...currentTeam.slice(1),
    ]);
  };

  const addSupportMember = useCallback(() => {
    const currentTeam = form.getValues('supportTeam') || [];
    form.setValue('supportTeam', [...currentTeam, { ...DEFAULT_SUPPORT }]);
  }, [form]);

  const clearSupportFirstRow = () => {
    const currentTeam = form.getValues('supportTeam');
    form.setValue('supportTeam', currentTeam.slice(1));
  };

  const syncContactFromRegister = () => {
    const reg = form.getValues('registerInfo');
    form.setValue('contactPoint', {
      fullName: reg.fullName,
      organization: reg.organization,
      phone: reg.phone,
      email: reg.email,
    });
  };

  const clearContactPoint = () => {
    form.setValue('contactPoint', { fullName: '', organization: '', phone: '', email: '' });
  };


  // Step 1: Validate form → call /initiate → open OTP popup
  const onSubmit = form.handleSubmit(async (data) => {
    setIsSubmitting(true);
    setSubmitError(null);
    try {
      const res = await visitRequestApi.initiate(data);
      setSessionToken(res.sessionToken);
      setMaskedEmail(res.maskedEmail);
    } catch (error) {
      console.error('UC-17 submit/initiate failed', error);
      const message = getApiErrorMessage(error);
      setSubmitError(message);
      mapContactEmailError(error, message);
    } finally {
      setIsSubmitting(false);
    }
  });

  // Surface contact-email business conflicts on the specific field so the user knows
  // exactly which input to change (not just a generic submit banner).
  const mapContactEmailError = (error: unknown, message: string) => {
    const code = getApiErrorCode(error);
    if (code === CONTACT_EMAIL_CONFLICT || code === VISITOR_ACCOUNT_INACTIVE) {
      form.setError('contactPoint.email', { type: 'server', message });
    }
  };

  // Step 2: Verify OTP → create visit request
  const verifyOtp = async (otpCode: string) => {
    if (!sessionToken) return;
    setIsVerifying(true);
    setOtpError(null);
    try {
      // SQL v8.3: resubmit the full form (kept in the form state) together with the OTP.
      const result = await visitRequestApi.verify(form.getValues(), otpCode);
      setSessionToken(null);
      clearVisitRequestDraft();
      onSuccess(result);
    } catch (err: any) {
      const code = getApiErrorCode(err);
      // A contact-email business conflict is not an OTP problem — close the OTP modal,
      // return to the form and surface the message on the contact email field.
      if (code === CONTACT_EMAIL_CONFLICT || code === VISITOR_ACCOUNT_INACTIVE) {
        const message = getApiErrorMessage(err);
        setSessionToken(null);
        setSubmitError(message);
        mapContactEmailError(err, message);
      } else {
        console.error('UC-17 OTP verify failed:', err?.response?.status, err?.response?.data);
        const msg = err?.response?.data?.message
          || err?.response?.data?.error
          || 'Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.';
        setOtpError(msg);
      }
    } finally {
      setIsVerifying(false);
    }
  };

  const resendOtp = async () => {
    if (!sessionToken) return;
    setIsResending(true);
    setOtpError(null);
    try {
      const data = form.getValues();
      await visitRequestApi.resendOtp(data.registerInfo.email, data.registerInfo.fullName);
    } catch (err: any) {
      setOtpError(
        err?.response?.data?.message ?? 'Không thể gửi lại mã. Vui lòng thử lại.'
      );
    } finally {
      setIsResending(false);
    }
  };

  const cancelOtp = () => {
    setSessionToken(null);
    setOtpError(null);
  };

  return {
    form,
    visitFields,
    visitorFields,
    supportTeamFields,
    syncSupportFromRegister,
    clearSupportFirstRow,
    syncContactFromRegister,
    clearContactPoint,
    onSubmit,
    isSubmitting,
    submitError,
    // OTP phase
    sessionToken,
    maskedEmail,
    otpError,
    isVerifying,
    isResending,
    verifyOtp,
    resendOtp,
    cancelOtp,
    draftHydrated,
    setDraftHydrated,
    isRestoringDraftRef,
  };
};
