import { describe, it, expect } from 'vitest';
import { resolveEffectiveRole, ALL_EFFECTIVE_ROLES } from '../resolveEffectiveRole';
import type { AuthUser } from '../../../features/authentication/types/authentication.types';

function user(roleCode: string, subRole?: string | null): AuthUser {
  return {
    userId: 'u-1',
    fullName: 'Test User',
    email: 'test@fpt.edu.vn',
    roleCode,
    subRole: subRole ?? null,
    mustChangePassword: false,
    mustSetPassword: false,
    effectiveRole: '',
    status: 'ACTIVE',
  };
}

describe('resolveEffectiveRole — the 8 valid mappings', () => {
  it.each([
    ['ADMIN', null, 'ADMIN'],
    ['HO', null, 'HO'],
    ['STAFF', 'LEADER', 'STAFF_LEADER'],
    ['STAFF', 'STAFF', 'STAFF'],
    ['DEPARTMENT', 'LEADER', 'DEPARTMENT_LEAD'],
    ['DEPARTMENT', 'STAFF', 'DEPARTMENT'],
    ['STUDENT', null, 'STUDENT'],
    ['VISITOR', null, 'VISITOR'],
  ] as const)('%s + %s -> %s', (roleCode, subRole, expected) => {
    expect(resolveEffectiveRole(user(roleCode, subRole))).toBe(expected);
  });

  it('covers every role in ALL_EFFECTIVE_ROLES', () => {
    // Guards against adding a role to the union without a mapping to produce it.
    const produced = new Set(
      (
        [
          ['ADMIN', null],
          ['HO', null],
          ['STAFF', 'LEADER'],
          ['STAFF', 'STAFF'],
          ['DEPARTMENT', 'LEADER'],
          ['DEPARTMENT', 'STAFF'],
          ['STUDENT', null],
          ['VISITOR', null],
        ] as const
      ).map(([r, s]) => resolveEffectiveRole(user(r, s))),
    );
    expect([...produced].sort()).toEqual([...ALL_EFFECTIVE_ROLES].sort());
  });
});

describe('resolveEffectiveRole — Leader and Staff must never collapse together', () => {
  // This is the regression that made every effective-role guard useless: both sub-roles
  // resolved to the same value, so 'STAFF_LEADER only' screens could not be expressed.
  it('separates Staff Leader from Staff', () => {
    expect(resolveEffectiveRole(user('STAFF', 'LEADER'))).not.toBe(
      resolveEffectiveRole(user('STAFF', 'STAFF')),
    );
  });

  it('separates Department Lead from Department staff', () => {
    expect(resolveEffectiveRole(user('DEPARTMENT', 'LEADER'))).not.toBe(
      resolveEffectiveRole(user('DEPARTMENT', 'STAFF')),
    );
  });
});

describe('resolveEffectiveRole — fail-closed', () => {
  it('returns null for a null/undefined user', () => {
    expect(resolveEffectiveRole(null)).toBeNull();
    expect(resolveEffectiveRole(undefined)).toBeNull();
  });

  it('returns null for STAFF with no sub-role — never assumes plain Staff', () => {
    expect(resolveEffectiveRole(user('STAFF', null))).toBeNull();
    expect(resolveEffectiveRole(user('STAFF', ''))).toBeNull();
    expect(resolveEffectiveRole(user('STAFF', 'NONE'))).toBeNull();
  });

  it('returns null for DEPARTMENT with no sub-role', () => {
    expect(resolveEffectiveRole(user('DEPARTMENT', null))).toBeNull();
    expect(resolveEffectiveRole(user('DEPARTMENT', 'NONE'))).toBeNull();
  });

  it('returns null for an unrecognised sub-role — never falls back to Leader', () => {
    expect(resolveEffectiveRole(user('STAFF', 'SUPERVISOR'))).toBeNull();
    expect(resolveEffectiveRole(user('DEPARTMENT', 'HEAD'))).toBeNull();
  });

  it('returns null for an unknown role code', () => {
    expect(resolveEffectiveRole(user('SUPERUSER'))).toBeNull();
    expect(resolveEffectiveRole(user(''))).toBeNull();
  });
});

describe('resolveEffectiveRole — normalisation', () => {
  it('is case-insensitive and trims whitespace', () => {
    expect(resolveEffectiveRole(user('  staff  ', ' leader '))).toBe('STAFF_LEADER');
    expect(resolveEffectiveRole(user('Ho'))).toBe('HO');
    expect(resolveEffectiveRole(user('department', 'staff'))).toBe('DEPARTMENT');
  });

  it('accepts the legacy DEPT alias', () => {
    expect(resolveEffectiveRole(user('DEPT', 'LEADER'))).toBe('DEPARTMENT_LEAD');
    expect(resolveEffectiveRole(user('dept', 'staff'))).toBe('DEPARTMENT');
  });

  it('does not grant on DEPT alias without a sub-role', () => {
    expect(resolveEffectiveRole(user('DEPT', null))).toBeNull();
  });
});
