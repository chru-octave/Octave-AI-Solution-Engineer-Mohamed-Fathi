import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box, Card, CardContent, Chip, CircularProgress, Grid,
  Table, TableBody, TableCell, TableContainer, TableHead,
  TableRow, Typography
} from '@mui/material';
import {
  Assignment, CheckCircle, Error, HourglassEmpty, RateReview
} from '@mui/icons-material';
import {
  Bar, BarChart, Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis
} from 'recharts';
import { analyticsApi } from '../api/client';
import type { Statistics } from '../types';
import { statusColor, statusLabel, confidenceColor } from '../utils/formatters';

const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884D8', '#82CA9D', '#FFC0CB', '#A0522D', '#FF6B6B', '#4ECDC4'];

export default function Dashboard() {
  const [stats, setStats] = useState<Statistics | null>(null);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    analyticsApi.getStatistics()
      .then(r => setStats(r.data))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <Box display="flex" justifyContent="center" mt={8}><CircularProgress /></Box>;
  if (!stats) return <Typography color="error">Failed to load statistics</Typography>;

  const statCards = [
    { label: 'Total Submissions', value: stats.total, icon: <Assignment />, color: '#1976d2' },
    { label: 'This Week', value: stats.thisWeek, icon: <HourglassEmpty />, color: '#388e3c' },
    { label: 'Needs Review', value: stats.needsReview, icon: <RateReview />, color: '#f57c00' },
    { label: 'Failed', value: stats.failed, icon: <Error />, color: '#d32f2f' },
    { label: 'Processed', value: stats.processed, icon: <CheckCircle />, color: '#7b1fa2' },
  ];

  const monthLabels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const monthData = stats.byMonth.map(m => ({
    name: `${monthLabels[m.month - 1]} ${m.year}`,
    count: m.count
  }));

  return (
    <Box>
      <Typography variant="h4" fontWeight={700} mb={3}>Dashboard</Typography>

      {/* Stat Cards */}
      <Grid container spacing={3} mb={4}>
        {statCards.map(card => (
          <Grid size={{ xs: 12, sm: 6, md: 2.4 }} key={card.label}>
            <Card elevation={2}>
              <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                <Box sx={{ color: card.color, fontSize: 40 }}>{card.icon}</Box>
                <Box>
                  <Typography variant="h4" fontWeight={700}>{card.value}</Typography>
                  <Typography variant="body2" color="text.secondary">{card.label}</Typography>
                </Box>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {/* Charts Row */}
      <Grid container spacing={3} mb={4}>
        <Grid size={{ xs: 12, md: 5 }}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" mb={2}>Coverage Lines</Typography>
              {stats.lobBreakdown.length === 0
                ? <Typography color="text.secondary">No coverage data yet</Typography>
                : (() => {
                    const total = stats.lobBreakdown.reduce((s, d) => s + d.count, 0);
                    return (
                      <Box>
                        <ResponsiveContainer width="100%" height={220}>
                          <PieChart>
                            <Pie
                              data={stats.lobBreakdown}
                              dataKey="count"
                              nameKey="lob"
                              cx="50%"
                              cy="50%"
                              innerRadius={55}
                              outerRadius={90}
                              paddingAngle={3}
                            >
                              {stats.lobBreakdown.map((_, i) => (
                                <Cell key={i} fill={COLORS[i % COLORS.length]} />
                              ))}
                            </Pie>
                            <Tooltip
                              formatter={(value: number, name: string) => [
                                `${value} (${((value / total) * 100).toFixed(0)}%)`,
                                name,
                              ]}
                            />
                          </PieChart>
                        </ResponsiveContainer>
                        {/* Custom legend */}
                        <Box display="flex" flexWrap="wrap" gap={1} justifyContent="center" mt={1}>
                          {stats.lobBreakdown.map((entry, i) => (
                            <Box key={i} display="flex" alignItems="center" gap={0.5}>
                              <Box sx={{ width: 10, height: 10, borderRadius: '50%', bgcolor: COLORS[i % COLORS.length], flexShrink: 0 }} />
                              <Typography variant="caption" color="text.secondary">
                                {entry.lob} <strong>({entry.count})</strong>
                              </Typography>
                            </Box>
                          ))}
                        </Box>
                      </Box>
                    );
                  })()
              }
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 7 }}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" mb={2}>Submissions by Month</Typography>
              {monthData.length === 0
                ? <Typography color="text.secondary">No monthly data yet</Typography>
                : (
                  <ResponsiveContainer width="100%" height={260}>
                    <BarChart data={monthData}>
                      <XAxis dataKey="name" />
                      <YAxis />
                      <Tooltip />
                      <Bar dataKey="count" fill="#1976d2" />
                    </BarChart>
                  </ResponsiveContainer>
                )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Recent Submissions */}
      <Card elevation={2}>
        <CardContent>
          <Typography variant="h6" mb={2}>Recent Submissions</Typography>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Company</TableCell>
                  <TableCell>Broker</TableCell>
                  <TableCell>Date</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Confidence</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {stats.recent.map(s => (
                  <TableRow
                    key={s.submissionId}
                    hover
                    sx={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/submissions/${s.submissionId}`)}
                  >
                    <TableCell>{s.insuredName ?? s.emailSubject ?? '—'}</TableCell>
                    <TableCell>{s.brokerName ?? '—'}</TableCell>
                    <TableCell>{new Date(s.submissionDate).toLocaleDateString()}</TableCell>
                    <TableCell>
                      <Chip
                        label={statusLabel(s.status)}
                        color={statusColor(s.status) as any}
                        size="small"
                      />
                    </TableCell>
                    <TableCell>
                      <Chip
                        label={`${(s.extractionConfidence * 100).toFixed(0)}%`}
                        size="small"
                        sx={{ bgcolor: confidenceColor(s.extractionConfidence), color: 'white' }}
                      />
                    </TableCell>
                  </TableRow>
                ))}
                {stats.recent.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} align="center" sx={{ py: 3, color: 'text.secondary' }}>
                      No submissions yet. Upload .eml files to get started.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </CardContent>
      </Card>
    </Box>
  );
}
