export type SubmissionStatus = 'Pending' | 'Processed' | 'Failed' | 'NeedsReview';

export interface SubmissionSummary {
  submissionId: number;
  emailSubject: string | null;
  emailFrom: string | null;
  submissionDate: string;
  processedDate: string;
  status: SubmissionStatus;
  extractionConfidence: number;
  failureReason: string | null;
  insuredName: string | null;
  brokerName: string | null;
  agencyName: string | null;
  attachmentList: string | null;
}

export interface Insured {
  insuredId: number;
  companyName: string;
  address: string | null;
  city: string | null;
  state: string | null;
  zipCode: string | null;
  industry: string | null;
  yearsInBusiness: number | null;
  dotNumber: string | null;
  mcNumber: string | null;
  annualRevenue: number | null;
}

export interface Broker {
  brokerId: number;
  brokerName: string;
  agencyName: string;
  email: string | null;
  phone: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  zipCode: string | null;
  licenseNumber: string | null;
}

export interface CoverageLine {
  coverageId: number;
  lineOfBusiness: string;
  requestedLimit: string | null;
  targetPremium: number | null;
  currentPremium: number | null;
  effectiveDate: string | null;
  expirationDate: string | null;
  notes: string | null;
}

export interface Exposure {
  exposureId: number;
  exposureType: string;
  quantity: number;
  description: string | null;
}

export interface LossHistory {
  lossId: number;
  lossDate: string | null;
  lossAmount: number;
  lossType: string | null;
  description: string | null;
  status: string | null;
  claimNumber: string | null;
  policyYear: string | null;
  paidAmount: number | null;
  reserveAmount: number | null;
  isClosed: boolean;
}

export interface SubmissionDetail {
  submissionId: number;
  emailFilePath: string;
  emailSubject: string | null;
  emailFrom: string | null;
  attachmentList: string | null;
  submissionDate: string;
  processedDate: string;
  status: SubmissionStatus;
  extractionConfidence: number;
  failureReason: string | null;
  insured: Insured | null;
  broker: Broker | null;
  coverageLines: CoverageLine[];
  exposures: Exposure[];
  losses: LossHistory[];
}

export interface PagedResult<T> {
  total: number;
  page: number;
  pageSize: number;
  items: T[];
}

export interface SearchRequest {
  query?: string;
  status?: SubmissionStatus;
  dateFrom?: string;
  dateTo?: string;
  page: number;
  pageSize: number;
}

export interface Statistics {
  total: number;
  thisWeek: number;
  needsReview: number;
  failed: number;
  processed: number;
  avgConfidence: number;
  lobBreakdown: { lob: string; count: number }[];
  premiumByLob: { lob: string; totalPremium: number }[];
  byMonth: { year: number; month: number; count: number }[];
  recent: SubmissionSummary[];
}
