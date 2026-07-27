/**
 * Shape of one line in the real-stack email sink (`PEMS_E2E_TEST_SINK_PATH`), written by
 * `FileSinkEmailService`.
 *
 * The envelope is recorded as three separate groups rather than a single address, because the property
 * the suite needs to be able to assert is not "who got it" but "who could SEE who got it": a blind copy
 * must appear in `bcc` and nowhere a TO/CC recipient could read.
 */
export interface SinkAddress {
  email?: string;
  displayName?: string | null;
}

export interface SinkRecord {
  to?: SinkAddress[];
  cc?: SinkAddress[];
  bcc?: SinkAddress[];
  templateCode?: string | null;
  subject?: string;
  body?: string;
  bodyFormat?: string;
  /** Template code when there is one, otherwise 'GENERIC'. */
  kind?: string;
  /** OTP recovered from the rendered body, when the message contains one. */
  code?: string | null;
  /** First actionable link (claim/transfer/confirm-email/email-action) found in the body. */
  link?: string | null;
  at?: string;
  status?: string;
}

const has = (group: SinkAddress[] | undefined, target: string) =>
  (group ?? []).some(a => (a.email ?? '').trim().toLowerCase() === target);

/** True when the address is a visible (TO or CC) recipient of the record. */
export function sinkAddressed(rec: SinkRecord, target: string): boolean {
  return has(rec.to, target) || has(rec.cc, target);
}

/** True when the address received the message as a BLIND copy. */
export function sinkBlindCopied(rec: SinkRecord, target: string): boolean {
  return has(rec.bcc, target);
}

/** True when the address is anywhere on the envelope. */
export function sinkAnyRecipient(rec: SinkRecord, target: string): boolean {
  return sinkAddressed(rec, target) || sinkBlindCopied(rec, target);
}
