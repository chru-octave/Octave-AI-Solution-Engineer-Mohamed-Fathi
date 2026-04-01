import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Alert, Box, Button, Card, CardContent, Chip, CircularProgress,
  Divider, Grid, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Typography
} from '@mui/material';
import { ArrowBack, ErrorOutline } from '@mui/icons-material';
import { submissionsApi } from '../api/client';
import type { SubmissionDetail as SubmissionDetailType } from '../types';
import { confidenceColor, formatCurrency, formatDate, statusColor, statusLabel } from '../utils/formatters';

function InfoRow({ label, value }: { label: string; value: string | number | null | undefined }) {
  return (
    <Box display="flex" gap={1} mb={0.5}>
      <Typography variant="body2" color="text.secondary" minWidth={140}>{label}:</Typography>
      <Typography variant="body2" fontWeight={500}>{value ?? '—'}</Typography>
    </Box>
  );
}

export default function SubmissionDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [submission, setSubmission] = useState<SubmissionDetailType | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) return;
    submissionsApi.getById(Number(id))
      .then(r => setSubmission(r.data))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) return <Box display="flex" justifyContent="center" mt={8}><CircularProgress /></Box>;
  if (!submission) return <Typography color="error">Submission not found</Typography>;

  const totalLoss = submission.losses.reduce((sum, l) => sum + l.lossAmount, 0);

  return (
    <Box>
      <Stack direction="row" alignItems="center" gap={2} mb={3}>
        <Button startIcon={<ArrowBack />} onClick={() => navigate('/submissions')}>Back</Button>
        <Typography variant="h4" fontWeight={700} flex={1}>
          {submission.insured?.companyName ?? submission.emailSubject ?? `Submission #${submission.submissionId}`}
        </Typography>
        <Chip
          label={statusLabel(submission.status)}
          color={statusColor(submission.status) as any}
        />
        <Chip
          label={`${(submission.extractionConfidence * 100).toFixed(0)}% confidence`}
          sx={{ bgcolor: confidenceColor(submission.extractionConfidence), color: 'white' }}
        />
      </Stack>

      {/* Failure Reason Banner */}
      {submission.status === 'Failed' && (
        <Alert
          severity="error"
          icon={<ErrorOutline />}
          sx={{ mb: 3 }}
        >
          <Typography variant="subtitle2" fontWeight={700} gutterBottom>
            Processing Failed
          </Typography>
          <Typography variant="body2">
            {submission.failureReason ?? 'An unknown error occurred during processing.'}
          </Typography>
        </Alert>
      )}

      <Grid container spacing={3}>
        {/* Insured Card */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" fontWeight={700} mb={2}>Insured</Typography>
              {submission.insured ? (
                <>
                  <InfoRow label="Company" value={submission.insured.companyName} />
                  <InfoRow label="Address" value={[submission.insured.address, submission.insured.city, submission.insured.state, submission.insured.zipCode].filter(Boolean).join(', ')} />
                  <InfoRow label="Industry" value={submission.insured.industry} />
                  <InfoRow label="Years in Business" value={submission.insured.yearsInBusiness} />
                  <InfoRow label="DOT #" value={submission.insured.dotNumber} />
                  <InfoRow label="MC #" value={submission.insured.mcNumber} />
                  <InfoRow label="Annual Revenue" value={formatCurrency(submission.insured.annualRevenue)} />
                </>
              ) : <Typography color="text.secondary">No insured data extracted</Typography>}
            </CardContent>
          </Card>
        </Grid>

        {/* Broker Card */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" fontWeight={700} mb={2}>Broker</Typography>
              {submission.broker ? (
                <>
                  <InfoRow label="Broker" value={submission.broker.brokerName} />
                  <InfoRow label="Agency" value={submission.broker.agencyName} />
                  <InfoRow label="Email" value={submission.broker.email} />
                  <InfoRow label="Phone" value={submission.broker.phone} />
                  <InfoRow label="Address" value={[submission.broker.address, submission.broker.city, submission.broker.state].filter(Boolean).join(', ')} />
                  <InfoRow label="License #" value={submission.broker.licenseNumber} />
                </>
              ) : (
                <>
                  <InfoRow label="Email From" value={submission.emailFrom} />
                  <Typography color="text.secondary" mt={1}>No broker data extracted</Typography>
                </>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Coverage Lines */}
        <Grid size={{ xs: 12 }}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" fontWeight={700} mb={2}>
                Coverage Lines ({submission.coverageLines.length})
              </Typography>
              {submission.coverageLines.length === 0
                ? <Typography color="text.secondary">No coverage lines extracted</Typography>
                : (
                  <TableContainer>
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell>Line of Business</TableCell>
                          <TableCell>Requested Limit</TableCell>
                          <TableCell>Target Premium</TableCell>
                          <TableCell>Current Premium</TableCell>
                          <TableCell>Effective Date</TableCell>
                          <TableCell>Expiration Date</TableCell>
                          <TableCell>Notes</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {submission.coverageLines.map(cl => (
                          <TableRow key={cl.coverageId}>
                            <TableCell><Chip label={cl.lineOfBusiness} size="small" color="primary" /></TableCell>
                            <TableCell>{cl.requestedLimit ?? '—'}</TableCell>
                            <TableCell>{formatCurrency(cl.targetPremium)}</TableCell>
                            <TableCell>{formatCurrency(cl.currentPremium)}</TableCell>
                            <TableCell>{formatDate(cl.effectiveDate)}</TableCell>
                            <TableCell>{formatDate(cl.expirationDate)}</TableCell>
                            <TableCell>{cl.notes ?? '—'}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </TableContainer>
                )}
            </CardContent>
          </Card>
        </Grid>

        {/* Exposures */}
        <Grid size={{ xs: 12 }}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" fontWeight={700} mb={2}>
                Exposures ({submission.exposures.length})
              </Typography>
              {submission.exposures.length === 0
                ? <Typography color="text.secondary">No exposures extracted</Typography>
                : (
                  <TableContainer>
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell>Type</TableCell>
                          <TableCell align="right">Quantity</TableCell>
                          <TableCell>Description</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {submission.exposures.map(exp => (
                          <TableRow key={exp.exposureId}>
                            <TableCell>
                              <Chip label={exp.exposureType} size="small" color="secondary" />
                            </TableCell>
                            <TableCell align="right">
                              <Typography fontWeight={600} variant="body2">
                                {exp.quantity.toLocaleString()}
                              </Typography>
                            </TableCell>
                            <TableCell>
                              <Typography variant="body2" color="text.secondary">
                                {exp.description ?? '—'}
                              </Typography>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </TableContainer>
                )}
            </CardContent>
          </Card>
        </Grid>

        {/* Source Info */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" fontWeight={700} mb={2}>Source Information</Typography>
              <InfoRow label="Email From" value={submission.emailFrom} />
              <InfoRow label="Subject" value={submission.emailSubject} />
              <InfoRow label="Submission Date" value={formatDate(submission.submissionDate)} />
              <InfoRow label="Processed Date" value={formatDate(submission.processedDate)} />
              <Divider sx={{ my: 1 }} />
              <Typography variant="body2" color="text.secondary" mb={0.5}>Attachments:</Typography>
              {submission.attachmentList
                ? submission.attachmentList.split(',').map(a => (
                  <Chip key={a} label={a.trim()} size="small" sx={{ mr: 0.5, mb: 0.5 }} />
                ))
                : <Typography variant="body2">None</Typography>
              }
            </CardContent>
          </Card>
        </Grid>

        {/* Loss History */}
        <Grid size={{ xs: 12 }}>
          <Card elevation={2}>
            <CardContent>
              <Stack direction="row" alignItems="center" justifyContent="space-between" mb={2}>
                <Typography variant="h6" fontWeight={700}>
                  Loss History ({submission.losses.length})
                </Typography>
                {submission.losses.length > 0 && (
                  <Chip
                    label={`Total: ${formatCurrency(totalLoss)}`}
                    color="error"
                    variant="outlined"
                  />
                )}
              </Stack>
              {submission.losses.length === 0
                ? <Typography color="text.secondary">No loss history extracted</Typography>
                : (
                  <TableContainer>
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell>Date</TableCell>
                          <TableCell>Type</TableCell>
                          <TableCell>Claim #</TableCell>
                          <TableCell>Policy Year</TableCell>
                          <TableCell align="right">Loss Amount</TableCell>
                          <TableCell align="right">Paid</TableCell>
                          <TableCell align="right">Reserve</TableCell>
                          <TableCell>Status</TableCell>
                          <TableCell>Description</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {submission.losses.map(loss => (
                          <TableRow key={loss.lossId}>
                            <TableCell>{formatDate(loss.lossDate)}</TableCell>
                            <TableCell>{loss.lossType ?? '—'}</TableCell>
                            <TableCell>{loss.claimNumber ?? '—'}</TableCell>
                            <TableCell>{loss.policyYear ?? '—'}</TableCell>
                            <TableCell align="right">{formatCurrency(loss.lossAmount)}</TableCell>
                            <TableCell align="right">{formatCurrency(loss.paidAmount)}</TableCell>
                            <TableCell align="right">{formatCurrency(loss.reserveAmount)}</TableCell>
                            <TableCell>
                              <Chip
                                label={loss.isClosed ? 'Closed' : (loss.status ?? 'Open')}
                                size="small"
                                color={loss.isClosed ? 'default' : 'warning'}
                              />
                            </TableCell>
                            <TableCell sx={{ maxWidth: 200 }}>
                              <Typography variant="caption">{loss.description ?? '—'}</Typography>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </TableContainer>
                )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
}
