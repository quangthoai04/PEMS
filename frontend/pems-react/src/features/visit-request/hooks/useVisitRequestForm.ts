import { useState } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { visitRequestSchema, type VisitRequestSchema } from '../schema/visitRequest.schema';
import { visitRequestApi, type VerifyResponse } from '../api/visitRequestApi';

const DEFAULT_VISITOR = {
  fullName: '',
  jobTitle: '',
  organization: '',
  nationality: '',
  passportId: '',
  email: '',
};

const DEFAULT_SUPPORT = {
  fullName: '',
  jobTitle: '',
  organization: '',
  nationality: '',
};

export const useVisitRequestForm = (onSuccess: (result: VerifyResponse) => void) => {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // OTP phase state
  const [sessionToken, setSessionToken] = useState<string | null>(null);
  const [maskedEmail, setMaskedEmail] = useState<string>('');
  const [otpError, setOtpError] = useState<string | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isResending, setIsResending] = useState(false);

  const form = useForm<VisitRequestSchema>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(visitRequestSchema) as any,
    mode: 'onBlur',
    reValidateMode: 'onChange',
    defaultValues: {
      registerInfo: { fullName: '', organization: '', jobTitle: '', phone: '', email: '', nationality: '' },
      delegationName: '',
      visitMode: 'single',
      visits: [{ campus: 'HN', startDatetime: '', endDatetime: '' }],
      purpose: '',
      workingContent: '',
      visitors: [{ ...DEFAULT_VISITOR }],
      supportTeam: [{ ...DEFAULT_SUPPORT }],
      contactPoint: { fullName: '', organization: '', phone: '', email: '' },
      language: 'english',
      vehicle: '',
      notes: '',
    },
  });

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

  const clearSupportFirstRow = () => {
    const currentTeam = form.getValues('supportTeam');
    form.setValue('supportTeam', [{ ...DEFAULT_SUPPORT }, ...currentTeam.slice(1)]);
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
    } catch (err: any) {
      setSubmitError(
        err?.response?.data?.message ?? 'Có lỗi xảy ra khi gửi đơn. Vui lòng thử lại.'
      );
    } finally {
      setIsSubmitting(false);
    }
  });

  // Step 2: Verify OTP → create visit request
  const verifyOtp = async (otpCode: string) => {
    if (!sessionToken) return;
    setIsVerifying(true);
    setOtpError(null);
    try {
      // SQL v8.3: resubmit the full form (kept in the form state) together with the OTP.
      const result = await visitRequestApi.verify(form.getValues(), otpCode);
      setSessionToken(null);
      onSuccess(result);
    } catch (err: any) {
      setOtpError(
        err?.response?.data?.message ?? 'Mã xác thực không đúng. Vui lòng thử lại.'
      );
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
  };
};
