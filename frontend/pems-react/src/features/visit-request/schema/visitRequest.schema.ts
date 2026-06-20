import { z } from 'zod';
import { isValidPhoneNumber } from 'libphonenumber-js';

const MIN_ADVANCE_HOURS = 72;
const MIN_DURATION_HOURS = 3;

export type VisitCampusRow = {
  campus: string;
  startDatetime: string;
  endDatetime: string;
};

export function isTimeOverlap(a: VisitCampusRow, b: VisitCampusRow): boolean {
  if (!a.startDatetime || !a.endDatetime || !b.startDatetime || !b.endDatetime) return false;
  const startA = new Date(a.startDatetime).getTime();
  const endA = new Date(a.endDatetime).getTime();
  const startB = new Date(b.startDatetime).getTime();
  const endB = new Date(b.endDatetime).getTime();
  return startA < endB && startB < endA;
}

export function findCampusTimeOverlaps(visits: VisitCampusRow[]) {
  const conflicts: Array<{ firstIndex: number; secondIndex: number; campusId: string }> = [];
  for (let i = 0; i < visits.length; i++) {
    for (let j = i + 1; j < visits.length; j++) {
      const a = visits[i];
      const b = visits[j];
      if (!a.campus || !b.campus) continue;
      // Duplicate campus is a hard error handled separately. Track overlap for DIFFERENT campuses.
      if (a.campus === b.campus) continue;
      if (isTimeOverlap(a, b)) {
        conflicts.push({ firstIndex: i, secondIndex: j, campusId: a.campus });
      }
    }
  }
  return conflicts;
}

const phoneSchema = z
  .string()
  .min(1, 'Số điện thoại không được để trống')
  .refine(
    (val) => {
      try {
        return isValidPhoneNumber(val);
      } catch {
        return false;
      }
    },
    { message: 'Số điện thoại không hợp lệ' }
  );

const emailSchema = z
  .string()
  .min(1, 'Email không được để trống')
  .email('Email không đúng định dạng (RFC 5322)');

const visitorSchema = z.object({
  fullName: z.string().min(1, 'Họ tên không được để trống').max(100, 'Tối đa 100 ký tự'),
  jobTitle: z.string().optional().default(''),
  organization: z.string().optional().default(''),
  nationality: z.string().min(1, 'Quốc tịch không được để trống'),
  email: emailSchema,
});

const supportTeamSchema = z.object({
  fullName: z.string().min(1, 'Họ tên không được để trống').max(100),
  jobTitle: z.string().min(1, 'Chức vụ không được để trống'),
  organization: z.string().min(1, 'Đơn vị công tác không được để trống'),
  nationality: z.string().min(1, 'Quốc tịch không được để trống'),
});

const visitSlotSchema = z
  .object({
    campus: z.string().min(1, 'Vui lòng chọn cơ sở'),
    startDatetime: z.string().min(1, 'Thời gian bắt đầu không được để trống'),
    endDatetime: z.string().min(1, 'Thời gian kết thúc không được để trống'),
  })
  .superRefine((data, ctx) => {
    if (!data.startDatetime || !data.endDatetime) return;

    const start = new Date(data.startDatetime);
    const end = new Date(data.endDatetime);
    const minStart = new Date(Date.now() + MIN_ADVANCE_HOURS * 60 * 60 * 1000);

    if (start < minStart) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: `Thời gian bắt đầu phải ít nhất ${MIN_ADVANCE_HOURS} giờ so với thời điểm hiện tại`,
        path: ['startDatetime'],
      });
    }

    if (end <= start) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Thời gian kết thúc phải sau thời gian bắt đầu',
        path: ['endDatetime'],
      });
      return;
    }

    const durationHours = (end.getTime() - start.getTime()) / (1000 * 60 * 60);
    if (durationHours < MIN_DURATION_HOURS) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: `Thời gian tham quan tối thiểu ${MIN_DURATION_HOURS} giờ`,
        path: ['endDatetime'],
      });
    }
  });

export const visitRequestSchema = z.object({
  registerInfo: z.object({
    fullName: z.string().min(1, 'Họ tên không được để trống').max(100),
    organization: z.string().min(1, 'Đơn vị công tác không được để trống'),
    jobTitle: z.string().min(1, 'Chức danh/phòng ban không được để trống'),
    phone: phoneSchema,
    email: emailSchema,
    nationality: z.string().min(1, 'Quốc tịch không được để trống'),
  }),
  delegationName: z.string().min(1, 'Tên đoàn không được để trống'),
  visitMode: z.enum(['single', 'multiple']),
  visits: z.array(visitSlotSchema).min(1),
  purpose: z.string().min(1, 'Mục đích thăm không được để trống'),
  workingContent: z.string().min(1, 'Nội dung làm việc không được để trống'),
  visitors: z.array(visitorSchema).min(1, 'Phải có ít nhất 1 khách trong danh sách'),
  supportTeam: z.array(supportTeamSchema).min(1, 'Phải có ít nhất 1 nhân sự hỗ trợ'),
  contactPoint: z.object({
    fullName: z.string().min(1, 'Họ tên không được để trống'),
    organization: z.string().min(1, 'Đơn vị không được để trống'),
    phone: phoneSchema,
    email: emailSchema,
  }),
  language: z.enum(['english', 'vietnamese'], 'Vui lòng chọn ngôn ngữ sử dụng'),
  vehicle: z.string().optional().default(''),
  notes: z.string().optional().default(''),
  timeOverlapConfirmed: z.boolean().optional().default(false),
}).superRefine((data, ctx) => {
  // Campus count must match the chosen scope. MULTI_CAMPUS never auto-downgrades —
  // it stays "Liên cơ sở" and the user is told to add a second campus.
  const codes = data.visits.map((v) => v.campus?.trim()).filter(Boolean);
  const distinct = new Set(codes);

  if (data.visitMode === 'multiple') {
    const hasDuplicateCampus = codes.length !== distinct.size;

    if (hasDuplicateCampus) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['visits'],
        message: 'Không được chọn trùng cơ sở trong yêu cầu liên cơ sở. Vui lòng chọn cơ sở khác.',
      });
    } else if (distinct.size < 2) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['visits'],
        message: 'Yêu cầu liên cơ sở cần ít nhất 2 cơ sở. Vui lòng thêm cơ sở thứ hai hoặc đổi sang Một cơ sở.',
      });
    }
  }

  if (data.visitMode === 'single' && distinct.size !== 1) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['visits'],
      message: 'Yêu cầu một cơ sở chỉ được chọn đúng 1 cơ sở.',
    });
  }

});

export type VisitRequestSchema = z.infer<typeof visitRequestSchema>;
