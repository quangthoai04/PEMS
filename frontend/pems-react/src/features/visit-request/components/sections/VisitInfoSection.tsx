import React from 'react';
import { Controller, type UseFormReturn, type UseFieldArrayReturn } from 'react-hook-form';
import { Calendar, Clock, Plus, X, ChevronDown } from 'lucide-react';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { findCampusTimeOverlaps } from '../../schema/visitRequest.schema';
import { FormField, inputCls, selectCls, textareaCls } from '../shared/FormField';
import { FormSection } from '../shared/FormSection';
import { useTranslation } from 'react-i18next';

// Campus options are now defined inside the component to access t()

function findDuplicateCampusIndexes(visits: Array<{ campus?: string }>) {
  const seen = new Map<string, number>();
  const duplicated = new Set<number>();

  visits.forEach((visit, index) => {
    const campus = visit.campus?.trim();
    if (!campus) return;

    if (seen.has(campus)) {
      duplicated.add(seen.get(campus)!);
      duplicated.add(index);
    } else {
      seen.set(campus, index);
    }
  });

  return duplicated;
}

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  visitFields: UseFieldArrayReturn<VisitRequestSchema, 'visits'>;
  showErrors?: boolean;
}

const dateTimeCls = (hasError?: boolean) =>
  [
    'h-11 w-full rounded-xl border bg-white pl-10 pr-3 text-sm font-medium text-slate-800 outline-none transition-colors',
    hasError
      ? 'border-red-400 focus:border-red-500 focus:ring-2 focus:ring-red-500/10'
      : 'border-slate-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10',
  ].join(' ');

export const VisitInfoSection: React.FC<Props> = ({ form, visitFields, showErrors }) => {
  const { t } = useTranslation(['visitRequest']);

  const CAMPUS_OPTIONS = [
    { value: 'HN',  label: t('visitRequest:step2Info.campusOptions.HN', 'Hà Nội') },
    { value: 'DN',  label: t('visitRequest:step2Info.campusOptions.DN', 'Đà Nẵng') },
    { value: 'CT',  label: t('visitRequest:step2Info.campusOptions.CT', 'Cần Thơ') },
    { value: 'HCM', label: t('visitRequest:step2Info.campusOptions.HCM', 'Hồ Chí Minh') },
    { value: 'QN',  label: t('visitRequest:step2Info.campusOptions.QN', 'Quy Nhơn') },
  ];

  const { register, control, watch, formState: { errors, touchedFields } } = form;
  const visitMode = watch('visitMode');
  const visits = watch('visits');
  const e = errors;
  const visitsMessage = (e.visits as any)?.root?.message || (e.visits as any)?.message;

  const overlaps = React.useMemo(() => findCampusTimeOverlaps(visits || []), [visits]);
  const duplicateCampusIndexes = React.useMemo(() => findDuplicateCampusIndexes(visits || []), [visits]);

  React.useEffect(() => {
    form.setValue('timeOverlapConfirmed', false, {
      shouldValidate: false,
      shouldDirty: false,
      shouldTouch: false,
    });
  }, [visitMode, JSON.stringify(visits), form]);

  return (
    <FormSection
      id="section-visit"
      title={t('visitRequest:singleForm.sections.visit')}
    >
      <div className="space-y-6">

        <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
          {/* Delegation name */}
          <FormField
            label={t('visitRequest:step2Info.delegationName')}
            required
            error={e.delegationName?.message}
            isValid={touchedFields.delegationName && !e.delegationName}
          >
            <input
              {...register('delegationName')}
              placeholder={t('visitRequest:step2Info.delegationNamePlaceholder')}
              className={inputCls(!!e.delegationName, touchedFields.delegationName && !e.delegationName)}
            />
          </FormField>

          {/* Visit mode */}
          <FormField label={t('visitRequest:step2Info.visitMode')} required showValidIcon={false}>
            <div className="relative">
              <Controller
                name="visitMode"
                control={control}
                render={({ field }) => (
                  <select
                    {...field}
                    onChange={(ev) => {
                      field.onChange(ev);
                      if (ev.target.value === 'single') {
                        visitFields.replace([visitFields.fields[0]]);
                      }
                    }}
                    className={selectCls()}
                  >
                    <option value="single">{t('visitRequest:step2Info.singleCampus')}</option>
                    <option value="multiple">{t('visitRequest:step2Info.multiCampus')}</option>
                  </select>
                )}
              />
              <ChevronDown className="w-4 h-4 text-gray-500 absolute right-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
            </div>
          </FormField>
        </div>

        {/* Schedule — flat rows with a divider, no nested card */}
        <div>
          <h3 className="text-sm font-bold text-slate-800">
            {t('visitRequest:singleForm.sections.schedule')}{' '}
            <span className="text-red-500">*</span>
            <span className="ml-2 text-xs font-medium normal-case text-slate-500">
              {t('visitRequest:step2Info.visitTime')}
            </span>
          </h3>

          <div className="mt-3">
            {visitFields.fields.map((field, index) => {
              const slotErrors = e.visits?.[index];
              const isOverlap = overlaps.some(o => o.firstIndex === index || o.secondIndex === index);
              const isDuplicateCampus = duplicateCampusIndexes.has(index);

              const shouldShowStartError = showErrors || touchedFields.visits?.[index]?.startDatetime;
              const shouldShowEndError = showErrors || touchedFields.visits?.[index]?.endDatetime;
              const startHasError = shouldShowStartError && !!slotErrors?.startDatetime;
              const endHasError = shouldShowEndError && !!slotErrors?.endDatetime;
              const rowHasError = startHasError || endHasError || (showErrors && !!slotErrors?.campus);

              return (
                <div
                  key={field.id}
                  data-field-error={rowHasError ? 'true' : undefined}
                  className={[
                    'relative flex w-full flex-col items-start gap-3 border-b border-slate-200 py-4 first:pt-0 last:border-b-0 last:pb-0 xl:flex-row',
                    isDuplicateCampus
                      ? '-mx-3 rounded-xl border-red-200 bg-red-50/60 px-3'
                      : isOverlap
                        ? '-mx-3 rounded-xl border-amber-200 bg-amber-50/50 px-3'
                        : '',
                  ].join(' ')}
                >
                  {visitMode === 'multiple' && visitFields.fields.length > 1 && (
                    <button
                      type="button"
                      onClick={() => visitFields.remove(index)}
                      aria-label={t('visitRequest:shared.delete')}
                      className="absolute -right-2 -top-2 z-10 flex h-6 w-6 items-center justify-center rounded-full bg-red-50 text-red-500 transition-colors hover:bg-red-500 hover:text-white"
                    >
                      <X className="w-3 h-3" />
                    </button>
                  )}

                  {/* Campus */}
                  <div className="flex-[1.2] w-full xl:w-auto">
                    {index === 0 && (
                      <label className="mb-1 block text-xs font-bold uppercase tracking-wider text-gray-600">{t('visitRequest:step2Info.campusLabel')}</label>
                    )}
                    <div className="relative">
                      <select
                        {...register(`visits.${index}.campus`)}
                        className={selectCls(showErrors && !!slotErrors?.campus)}
                      >
                        {CAMPUS_OPTIONS.map((c) => (
                          <option key={c.value} value={c.value}>{c.label}</option>
                        ))}
                      </select>
                      <ChevronDown className="pointer-events-none absolute right-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
                    </div>
                    {/* Reserved error slot keeps every column the same height → no row shift */}
                    <div className="min-h-[20px] mt-1">
                      {showErrors && slotErrors?.campus && (
                        <p className="text-xs text-red-600 font-medium leading-5">⚠ {slotErrors.campus.message}</p>
                      )}
                    </div>
                  </div>

                  {/* Start */}
                  <div className="flex-[1.5] w-full xl:w-auto">
                    {index === 0 && (
                      <label className="mb-1 block text-xs font-bold uppercase tracking-wider text-gray-600">{t('visitRequest:step2Info.startTime')}</label>
                    )}
                    <div className="relative">
                      <input
                        type="datetime-local"
                        {...register(`visits.${index}.startDatetime`)}
                        className={dateTimeCls(startHasError)}
                      />
                      <Calendar className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[#004c91]" />
                    </div>
                    <div className="min-h-[20px] mt-1">
                      {startHasError && (
                        <p className="text-xs text-red-600 font-medium leading-5">⚠ {slotErrors.startDatetime.message}</p>
                      )}
                    </div>
                  </div>

                  {/* End */}
                  <div className="flex-[1.5] w-full xl:w-auto">
                    {index === 0 && (
                      <label className="mb-1 block text-xs font-bold uppercase tracking-wider text-gray-600">{t('visitRequest:step2Info.endTime')}</label>
                    )}
                    <div className="relative">
                      <input
                        type="datetime-local"
                        {...register(`visits.${index}.endDatetime`)}
                        className={dateTimeCls(endHasError)}
                      />
                      <Clock className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[#004c91]" />
                    </div>
                    <div className="min-h-[20px] mt-1">
                      {endHasError && (
                        <p className="text-xs text-red-600 font-medium leading-5">⚠ {slotErrors.endDatetime.message}</p>
                      )}
                    </div>
                  </div>

                  {/* Timezone — plain text badge aligned with the inputs on the first row */}
                  <div className="flex-[0.8] w-full xl:w-auto">
                    {index === 0 && (
                      <label className="mb-1 block text-xs font-bold uppercase tracking-wider text-gray-600">{t('visitRequest:step2Info.timezone')}</label>
                    )}
                    <div className="flex h-11 select-none items-center justify-center px-3">
                      <span className="whitespace-nowrap text-sm font-bold text-[#004c91]">VN (GMT+7)</span>
                    </div>
                    <div className="min-h-[20px] mt-1" />
                  </div>
                </div>
              );
            })}
          </div>

          {overlaps.length > 0 && (
            <p className="mt-3 flex items-center gap-1 rounded-lg border border-amber-200 bg-amber-50 p-2.5 text-xs font-medium text-amber-600">
              <span className="shrink-0">⚠</span>
              {t('visitRequest:step2Info.overlapError')}
            </p>
          )}

          {showErrors && visitsMessage && visitsMessage !== 'OVERLAP_UNCONFIRMED' && (
            <p className="error-scroll-target mt-3 flex items-center gap-1 text-xs font-medium text-red-600">
              <span className="shrink-0">⚠</span>{visitsMessage}
            </p>
          )}

          {visitMode === 'multiple' && (
            <button
              type="button"
              onClick={() =>
                visitFields.append({ campus: 'HN', startDatetime: '', endDatetime: '' })
              }
              className="mt-4 flex w-full items-center justify-center gap-2 rounded-xl border-2 border-dashed border-[#f37021]/30 py-2.5 text-sm font-bold text-[#f37021] transition-colors hover:border-[#f37021] hover:bg-orange-50/50"
            >
              <Plus className="w-4 h-4" /> {t('visitRequest:step2Info.addCampus')}
            </button>
          )}
        </div>

        {/* Visit Type */}
        <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <FormField
            label={t('visitRequest:step2Info.visitType')}
            required
            error={e.visitType?.message}
            isValid={touchedFields.visitType && !e.visitType}
            showValidIcon={false}
          >
            <div className="relative">
              <select
                {...register('visitType')}
                className={selectCls(!!e.visitType)}
              >
                <option value="CAMPUS_TOUR">{t('visitRequest:step2Info.visitTypes.CAMPUS_TOUR', 'Campus Tour')}</option>
                <option value="MEETING">{t('visitRequest:step2Info.visitTypes.MEETING', 'Họp trao đổi')}</option>
                <option value="WORKSHOP">{t('visitRequest:step2Info.visitTypes.WORKSHOP', 'Workshop')}</option>
                <option value="SIGNING_CEREMONY">{t('visitRequest:step2Info.visitTypes.SIGNING_CEREMONY', 'Lễ ký kết')}</option>
                <option value="EXCHANGE">{t('visitRequest:step2Info.visitTypes.EXCHANGE', 'Giao lưu')}</option>
                <option value="OTHER">{t('visitRequest:step2Info.typeOther')}</option>
              </select>
              <ChevronDown className="w-4 h-4 text-gray-500 absolute right-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
            </div>
          </FormField>

          {watch('visitType') === 'OTHER' && (
            <FormField
              label={t('visitRequest:step2Info.visitTypeOther')}
              required
              error={e.visitTypeOther?.message}
              isValid={touchedFields.visitTypeOther && !e.visitTypeOther}
            >
              <input
                {...register('visitTypeOther')}
                placeholder={t('visitRequest:step2Info.visitTypeOtherPlaceholder')}
                className={inputCls(!!e.visitTypeOther, touchedFields.visitTypeOther && !e.visitTypeOther)}
              />
            </FormField>
          )}
        </div>

        {/* Purpose */}
        <FormField
          label={t('visitRequest:step2Info.purpose')}
          required
          error={e.purpose?.message}
          isValid={touchedFields.purpose && !e.purpose}
          showValidIcon={false}
        >
          <textarea
            {...register('purpose')}
            rows={3}
            placeholder={t('visitRequest:step2Info.purposePlaceholder')}
            className={textareaCls(!!e.purpose)}
          />
        </FormField>

        {/* Working content */}
        <FormField
          label={t('visitRequest:step2Info.workingContent')}
          required
          error={e.workingContent?.message}
          isValid={touchedFields.workingContent && !e.workingContent}
          showValidIcon={false}
        >
          <textarea
            {...register('workingContent')}
            rows={3}
            placeholder={t('visitRequest:step2Info.workingContentPlaceholder')}
            className={textareaCls(!!e.workingContent)}
          />
        </FormField>

      </div>
    </FormSection>
  );
};
