import React from 'react';
import { Controller, type UseFormReturn, type UseFieldArrayReturn } from 'react-hook-form';
import { Calendar, Clock, Plus, X, ChevronDown } from 'lucide-react';
import { motion } from 'motion/react';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { findCampusTimeOverlaps } from '../../schema/visitRequest.schema';
import { FormField, inputCls } from '../shared/FormField';
import { SectionTitle } from './RegisterInfoSection';

const CAMPUS_OPTIONS = [
  { value: 'HN',  label: 'Hà Nội' },
  { value: 'DN',  label: 'Đà Nẵng' },
  { value: 'CT',  label: 'Cần Thơ' },
  { value: 'HCM', label: 'Hồ Chí Minh' },
  { value: 'QN',  label: 'Quy Nhơn' },
];

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

export const VisitInfoSection: React.FC<Props> = ({ form, visitFields, showErrors }) => {
  const { register, control, watch, trigger, formState: { errors, touchedFields } } = form;
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
    <section>
      <SectionTitle index={2} title="THÔNG TIN ĐOÀN KHÁCH" />
      <div className="space-y-8">

        {/* Block I: Visit Info */}
        <div className="bg-slate-50/50 rounded-2xl border-l-4 border-l-[#f37021] border border-gray-100 p-5 sm:p-7 shadow-sm">
          <h4 className="text-gray-800 font-bold text-base mb-5 border-b border-gray-200 pb-2 uppercase tracking-wide">
            I. Thông tin chuyến thăm
          </h4>
          <div className="space-y-6">

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {/* Delegation name */}
              <FormField
                label="Tên đoàn khách"
                required
                error={e.delegationName?.message}
                isValid={touchedFields.delegationName && !e.delegationName}
              >
                <input
                  {...register('delegationName')}
                  placeholder="VD: Đoàn Đại học XYZ"
                  className={inputCls(!!e.delegationName, touchedFields.delegationName && !e.delegationName)}
                />
              </FormField>

              {/* Visit mode */}
              <FormField label="Cơ sở muốn tới thăm" required>
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
                        className="w-full px-4 py-2.5 pr-9 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none bg-white text-sm font-medium text-gray-900 shadow-sm appearance-none"
                      >
                        <option value="single">Chỉ một cơ sở</option>
                        <option value="multiple">Liên cơ sở</option>
                      </select>
                    )}
                  />
                  <ChevronDown className="w-4 h-4 text-gray-500 absolute right-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
                </div>
              </FormField>
            </div>

            {/* Visit slots */}
            <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm">
              <FormField label="Thời gian dự kiến thăm FPTU" required>
                <div className="space-y-4 mt-2">
                  {visitFields.fields.map((field, index) => {
                    const slotErrors = e.visits?.[index];
                    const isOverlap = overlaps.some(o => o.firstIndex === index || o.secondIndex === index);
                    const isDuplicateCampus = duplicateCampusIndexes.has(index);
                    
                    const shouldShowStartError = showErrors || touchedFields.visits?.[index]?.startDatetime;
                    const shouldShowEndError = showErrors || touchedFields.visits?.[index]?.endDatetime;
                    const startHasError = shouldShowStartError && !!slotErrors?.startDatetime;
                    const endHasError = shouldShowEndError && !!slotErrors?.endDatetime;

                    return (
                      <div
                        key={field.id}
                        className={[
                          'flex flex-col xl:flex-row items-start gap-3 w-full pb-4 border-b last:border-b-0 last:pb-0 relative rounded-xl transition-colors',
                          isDuplicateCampus
                            ? 'border-red-200 bg-red-50/60 p-3 -mx-3'
                            : isOverlap 
                              ? 'border-amber-200 bg-amber-50/50 p-3 -mx-3' 
                              : 'border-gray-100'
                        ].join(' ')}
                      >
                        {visitMode === 'multiple' && visitFields.fields.length > 1 && (
                          <button
                            type="button"
                            onClick={() => visitFields.remove(index)}
                            className="absolute -right-2 -top-2 w-6 h-6 bg-red-50 text-red-500 rounded-full flex items-center justify-center hover:bg-red-500 hover:text-white transition-colors z-10"
                          >
                            <X className="w-3 h-3" />
                          </button>
                        )}

                        {/* Campus */}
                        <div className="flex-[1.2] w-full xl:w-auto">
                          {index === 0 && (
                            <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">Cơ sở</label>
                          )}
                          <div className="relative">
                            <select
                              {...register(`visits.${index}.campus`)}
                              className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm font-medium bg-white shadow-sm appearance-none pr-8"
                            >
                              {CAMPUS_OPTIONS.map((c) => (
                                <option key={c.value} value={c.value}>{c.label}</option>
                              ))}
                            </select>
                            <ChevronDown className="absolute right-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
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
                            <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">Thời Gian Bắt đầu</label>
                          )}
                          <div className="relative">
                            <input
                              type="datetime-local"
                              {...register(`visits.${index}.startDatetime`)}
                              className={[
                                'w-full px-4 py-2.5 pl-10 rounded-xl border outline-none text-sm font-medium bg-white shadow-sm',
                                startHasError
                                  ? 'border-red-400 focus:border-red-500 focus:ring-1 focus:ring-red-400'
                                  : 'border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021]',
                              ].join(' ')}
                            />
                            <Calendar className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91] pointer-events-none" />
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
                            <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">Thời Gian Kết thúc</label>
                          )}
                          <div className="relative">
                            <input
                              type="datetime-local"
                              {...register(`visits.${index}.endDatetime`)}
                              className={[
                                'w-full px-4 py-2.5 pl-10 rounded-xl border outline-none text-sm font-medium bg-white shadow-sm',
                                endHasError
                                  ? 'border-red-400 focus:border-red-500 focus:ring-1 focus:ring-red-400'
                                  : 'border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021]',
                              ].join(' ')}
                            />
                            <Clock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91] pointer-events-none" />
                          </div>
                          <div className="min-h-[20px] mt-1">
                            {endHasError && (
                              <p className="text-xs text-red-600 font-medium leading-5">⚠ {slotErrors.endDatetime.message}</p>
                            )}
                          </div>
                        </div>

                        {/* Timezone badge — labelled on the first row so it aligns with the inputs */}
                        <div className="flex-[0.8] w-full xl:w-auto">
                          {index === 0 && (
                            <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">Múi giờ</label>
                          )}
                          <div className="flex items-center justify-center h-[42px] px-3 bg-gray-50 rounded-xl border border-gray-200 select-none">
                            <span className="text-[#004c91] text-sm font-bold whitespace-nowrap">VN (GMT+7)</span>
                          </div>
                          <div className="min-h-[20px] mt-1" />
                        </div>
                      </div>
                    );
                  })}
                </div>
              </FormField>

              {overlaps.length > 0 && (
                <p className="mt-3 text-xs text-amber-600 font-medium flex items-center gap-1 bg-amber-50 p-2.5 rounded-lg border border-amber-200">
                  <span className="shrink-0">⚠</span>
                  Một số cơ sở trong lịch trình có thời gian bắt đầu/kết thúc bị chồng lên nhau. Vui lòng kiểm tra lại trước khi tiếp tục.
                </p>
              )}

              {showErrors && visitsMessage && visitsMessage !== 'OVERLAP_UNCONFIRMED' && (
                <p className="mt-3 text-xs text-red-600 font-medium flex items-center gap-1">
                  <span className="shrink-0">⚠</span>{visitsMessage}
                </p>
              )}

              {visitMode === 'multiple' && (
                <button
                  type="button"
                  onClick={() =>
                    visitFields.append({ campus: 'HN', startDatetime: '', endDatetime: '' })
                  }
                  className="w-full mt-4 flex items-center justify-center gap-2 py-2.5 border-2 border-dashed border-[#f37021]/30 hover:border-[#f37021] text-[#f37021] rounded-xl text-sm font-bold transition-colors bg-orange-50/50 hover:bg-orange-50"
                >
                  <Plus className="w-4 h-4" /> Thêm cơ sở
                </button>
              )}
            </div>

            {/* Purpose */}
            <FormField
              label="Mục đích thăm FPTU"
              required
              error={e.purpose?.message}
              isValid={touchedFields.purpose && !e.purpose}
            >
              <textarea
                {...register('purpose')}
                rows={3}
                placeholder="Nhập mục đích chuyến thăm..."
                className={[
                  'w-full px-4 py-3 rounded-xl border outline-none transition-all bg-white text-sm shadow-sm resize-none font-medium text-gray-900',
                  e.purpose
                    ? 'border-red-400 focus:border-red-500 focus:ring-1 focus:ring-red-400'
                    : 'border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021]',
                ].join(' ')}
              />
            </FormField>

            {/* Working content */}
            <FormField
              label="Nội dung làm việc tại FPTU"
              required
              error={e.workingContent?.message}
              isValid={touchedFields.workingContent && !e.workingContent}
            >
              <textarea
                {...register('workingContent')}
                rows={3}
                placeholder="Nhập nội dung làm việc cụ thể..."
                className={[
                  'w-full px-4 py-3 rounded-xl border outline-none transition-all bg-white text-sm shadow-sm resize-none font-medium text-gray-900',
                  e.workingContent
                    ? 'border-red-400 focus:border-red-500 focus:ring-1 focus:ring-red-400'
                    : 'border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021]',
                ].join(' ')}
              />
            </FormField>
          </div>
        </div>
      </div>
    </section>
  );
};
