import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box, Button, Chip, CircularProgress, FormControl, InputLabel,
  MenuItem, Pagination, Paper, Select, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, TextField, Tooltip, Typography
} from '@mui/material';
import { Delete, Search } from '@mui/icons-material';
import { submissionsApi } from '../api/client';
import type { SearchRequest, SubmissionStatus, SubmissionSummary } from '../types';
import { confidenceColor, statusColor, statusLabel } from '../utils/formatters';

const PAGE_SIZE = 20;

export default function SubmissionsList() {
  const navigate = useNavigate();
  const [submissions, setSubmissions] = useState<SubmissionSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<SubmissionStatus | ''>('');

  const load = useCallback(async (p = page) => {
    setLoading(true);
    try {
      const req: SearchRequest = {
        query: query || undefined,
        status: statusFilter || undefined,
        page: p,
        pageSize: PAGE_SIZE,
      };
      const res = await submissionsApi.search(req);
      setSubmissions(res.data.items);
      setTotal(res.data.total);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }, [page, query, statusFilter]);

  useEffect(() => { load(page); }, [page]);

  const handleSearch = () => { setPage(1); load(1); };

  const handleDelete = async (id: number, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!window.confirm('Delete this submission?')) return;
    await submissionsApi.delete(id);
    load(page);
  };

  return (
    <Box>
      <Typography variant="h4" fontWeight={700} mb={3}>Submissions</Typography>

      {/* Filters */}
      <Paper elevation={1} sx={{ p: 2, mb: 3 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems="center">
          <TextField
            label="Search company / broker / subject"
            value={query}
            onChange={e => setQuery(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSearch()}
            size="small"
            sx={{ flex: 1, minWidth: 200 }}
          />
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel>Status</InputLabel>
            <Select
              value={statusFilter}
              label="Status"
              onChange={e => setStatusFilter(e.target.value as any)}
            >
              <MenuItem value="">All</MenuItem>
              <MenuItem value="Processed">Processed</MenuItem>
              <MenuItem value="NeedsReview">Needs Review</MenuItem>
              <MenuItem value="Failed">Failed</MenuItem>
              <MenuItem value="Pending">Pending</MenuItem>
            </Select>
          </FormControl>
          <Button variant="contained" startIcon={<Search />} onClick={handleSearch}>
            Search
          </Button>
        </Stack>
      </Paper>

      {loading
        ? <Box display="flex" justifyContent="center" mt={4}><CircularProgress /></Box>
        : (
          <>
            <TableContainer component={Paper} elevation={2}>
              <Table>
                <TableHead>
                  <TableRow>
                    <TableCell>Company</TableCell>
                    <TableCell>Broker / Agency</TableCell>
                    <TableCell>Date</TableCell>
                    <TableCell>Status</TableCell>
                    <TableCell>Confidence</TableCell>
                    <TableCell>Attachments</TableCell>
                    <TableCell align="center">Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {submissions.map(s => (
                    <TableRow
                      key={s.submissionId}
                      hover
                      sx={{ cursor: 'pointer' }}
                      onClick={() => navigate(`/submissions/${s.submissionId}`)}
                    >
                      <TableCell>
                        <Typography fontWeight={600}>{s.insuredName ?? '—'}</Typography>
                        <Typography variant="caption" color="text.secondary">{s.emailSubject}</Typography>
                      </TableCell>
                      <TableCell>
                        <div>{s.brokerName ?? '—'}</div>
                        <Typography variant="caption" color="text.secondary">{s.agencyName}</Typography>
                      </TableCell>
                      <TableCell>{new Date(s.submissionDate).toLocaleDateString()}</TableCell>
                      <TableCell>
                        <Tooltip
                          title={s.status === 'Failed' && s.failureReason ? s.failureReason : ''}
                          placement="top"
                          arrow
                          disableHoverListener={s.status !== 'Failed' || !s.failureReason}
                        >
                          <Chip
                            label={statusLabel(s.status)}
                            color={statusColor(s.status) as any}
                            size="small"
                          />
                        </Tooltip>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={`${(s.extractionConfidence * 100).toFixed(0)}%`}
                          size="small"
                          sx={{ bgcolor: confidenceColor(s.extractionConfidence), color: 'white' }}
                        />
                      </TableCell>
                      <TableCell>
                        <Typography variant="caption" color="text.secondary">
                          {s.attachmentList || '—'}
                        </Typography>
                      </TableCell>
                      <TableCell align="center" onClick={e => e.stopPropagation()}>
                        <Button
                          size="small"
                          color="error"
                          startIcon={<Delete />}
                          onClick={e => handleDelete(s.submissionId, e)}
                        >
                          Delete
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                  {submissions.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                        No submissions found
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>

            {total > PAGE_SIZE && (
              <Box display="flex" justifyContent="center" mt={3}>
                <Pagination
                  count={Math.ceil(total / PAGE_SIZE)}
                  page={page}
                  onChange={(_, p) => setPage(p)}
                  color="primary"
                />
              </Box>
            )}
            <Typography variant="caption" color="text.secondary" mt={1} display="block" textAlign="right">
              {total} total submission{total !== 1 ? 's' : ''}
            </Typography>
          </>
        )}
    </Box>
  );
}
