import { useCallback, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Alert, Box, Button, Card, CardContent, Chip, CircularProgress,
  Divider, FormControlLabel, IconButton, LinearProgress, List,
  ListItem, ListItemIcon, ListItemText, Stack, Switch, Tooltip,
  Typography,
} from '@mui/material';
import {
  AttachFile, CheckCircle, CloudUpload, Error, OpenInNew, Star, StarBorder,
} from '@mui/icons-material';
import { uploadApi } from '../api/client';

// ─── File type colour chip ────────────────────────────────────────────────────

function FileTypeChip({ fileName }: { fileName: string }) {
  const ext = fileName.split('.').pop()?.toLowerCase() ?? '';
  const colorMap: Record<string, 'default' | 'primary' | 'secondary' | 'success' | 'warning' | 'error' | 'info'> = {
    eml: 'primary', msg: 'primary',
    pdf: 'error',
    docx: 'info', doc: 'info',
    xlsx: 'success', xls: 'success',
    txt: 'default', csv: 'default',
    zip: 'warning',
  };
  return (
    <Chip
      label={`.${ext}`}
      size="small"
      color={colorMap[ext] ?? 'default'}
      sx={{ mr: 0.5, fontFamily: 'monospace', fontSize: '0.7rem' }}
    />
  );
}

// ─── Types ────────────────────────────────────────────────────────────────────

const EMAIL_EXTS = ['.eml', '.msg'];
const ACCEPTED_EXTENSIONS = ['.eml', '.msg', '.pdf', '.docx', '.doc', '.xlsx', '.xls', '.txt', '.csv', '.zip'];

interface UploadItem {
  file: File;
  status: 'pending' | 'uploading' | 'success' | 'error';
  submissionId?: number;
  error?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export default function Upload() {
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [items, setItems] = useState<UploadItem[]>([]);
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [dragOver, setDragOver] = useState(false);

  // Bundle mode state
  const [bundleMode, setBundleMode] = useState(false);
  const [primaryFileName, setPrimaryFileName] = useState<string | null>(null);
  const [bundleResult, setBundleResult] = useState<{ submissionId: number; primaryFile: string; attachmentFiles: string[] } | null>(null);

  // ── File ingestion ─────────────────────────────────────────────────────────

  const addFiles = useCallback((newFiles: File[]) => {
    const valid = newFiles.filter(f =>
      ACCEPTED_EXTENSIONS.some(ext => f.name.toLowerCase().endsWith(ext))
    );
    if (valid.length === 0) return;

    setItems(prev => {
      const updated = [...prev, ...valid.map(f => ({ file: f, status: 'pending' as const }))];

      // Auto-select primary: first email file, else first file overall
      if (bundleMode && primaryFileName === null) {
        const allFiles = updated.map(i => i.file);
        const emailFile = allFiles.find(f => EMAIL_EXTS.some(e => f.name.toLowerCase().endsWith(e)));
        setPrimaryFileName((emailFile ?? allFiles[0]).name);
      }
      return updated;
    });
  }, [bundleMode, primaryFileName]);

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    addFiles(Array.from(e.dataTransfer.files));
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) addFiles(Array.from(e.target.files));
  };

  const handleClear = () => {
    setItems([]);
    setPrimaryFileName(null);
    setBundleResult(null);
  };

  // When bundle mode is toggled, auto-assign primary for existing files
  const handleBundleModeChange = (on: boolean) => {
    setBundleMode(on);
    setBundleResult(null);
    if (on && items.length > 0) {
      const files = items.map(i => i.file);
      const emailFile = files.find(f => EMAIL_EXTS.some(e => f.name.toLowerCase().endsWith(e)));
      setPrimaryFileName((emailFile ?? files[0]).name);
    } else {
      setPrimaryFileName(null);
    }
  };

  // ── Upload ─────────────────────────────────────────────────────────────────

  const handleUpload = async () => {
    const pending = items.filter(i => i.status === 'pending');
    if (pending.length === 0) return;

    setUploading(true);
    setProgress(0);
    setBundleResult(null);

    setItems(prev => prev.map(i => i.status === 'pending' ? { ...i, status: 'uploading' } : i));

    if (bundleMode) {
      await handleBundleUpload(pending);
    } else {
      await handleIndividualUpload(pending);
    }

    setUploading(false);
    setProgress(0);
  };

  const handleIndividualUpload = async (pending: UploadItem[]) => {
    try {
      const res = await uploadApi.upload(pending.map(i => i.file), setProgress);
      setItems(prev => {
        const updated = [...prev];
        res.data.forEach(result => {
          const idx = updated.findIndex(i => i.file.name === result.fileName && i.status === 'uploading');
          if (idx >= 0) {
            updated[idx] = {
              ...updated[idx],
              status: result.error ? 'error' : 'success',
              submissionId: result.submissionId,
              error: result.error,
            };
          }
        });
        return updated;
      });
    } catch (err: any) {
      setItems(prev => prev.map(i =>
        i.status === 'uploading' ? { ...i, status: 'error', error: err.message } : i
      ));
    }
  };

  const handleBundleUpload = async (pending: UploadItem[]) => {
    const primary = primaryFileName ?? pending[0].file.name;
    try {
      const res = await uploadApi.uploadBundle(pending.map(i => i.file), primary, setProgress);
      setBundleResult({
        submissionId: res.data.submissionId,
        primaryFile: res.data.primaryFile,
        attachmentFiles: res.data.attachmentFiles,
      });
      setItems(prev => prev.map(i =>
        i.status === 'uploading' ? { ...i, status: 'success', submissionId: res.data.submissionId } : i
      ));
    } catch (err: any) {
      const msg = err.response?.data?.error ?? err.message;
      setItems(prev => prev.map(i =>
        i.status === 'uploading' ? { ...i, status: 'error', error: msg } : i
      ));
    }
  };

  // ── Derived state ──────────────────────────────────────────────────────────

  const pendingCount = items.filter(i => i.status === 'pending').length;
  const successCount = items.filter(i => i.status === 'success').length;

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <Box>
      <Stack direction="row" alignItems="center" justifyContent="space-between" mb={3}>
        <Typography variant="h4" fontWeight={700}>Upload Submissions</Typography>
        <FormControlLabel
          control={
            <Switch
              checked={bundleMode}
              onChange={e => handleBundleModeChange(e.target.checked)}
              disabled={uploading}
              color="secondary"
            />
          }
          label={
            <Stack direction="row" alignItems="center" spacing={0.5}>
              <Typography variant="body2" fontWeight={bundleMode ? 700 : 400}>
                Bundle Mode
              </Typography>
              <Chip
                label={bundleMode ? 'ON' : 'OFF'}
                size="small"
                color={bundleMode ? 'secondary' : 'default'}
                sx={{ fontSize: '0.65rem', height: 18 }}
              />
            </Stack>
          }
        />
      </Stack>

      {/* Bundle mode explainer */}
      {bundleMode && (
        <Alert severity="info" sx={{ mb: 2 }}>
          <strong>Bundle Mode:</strong> All files in the queue are sent as <strong>one submission</strong>.
          The <Star fontSize="small" sx={{ verticalAlign: 'middle' }} /> Primary file is the email / main document —
          all other files become its attachments. Claude sees everything together.
        </Alert>
      )}

      {/* Drop Zone */}
      <Card
        elevation={2}
        sx={{
          border: '2px dashed',
          borderColor: dragOver ? (bundleMode ? 'secondary.main' : 'primary.main') : 'divider',
          bgcolor: dragOver ? 'action.hover' : 'background.paper',
          transition: 'all 0.2s',
          cursor: 'pointer',
          mb: 3,
        }}
        onDragOver={e => { e.preventDefault(); setDragOver(true); }}
        onDragLeave={() => setDragOver(false)}
        onDrop={handleDrop}
        onClick={() => fileInputRef.current?.click()}
      >
        <CardContent sx={{ py: 5, textAlign: 'center' }}>
          <CloudUpload sx={{ fontSize: 56, color: bundleMode ? 'secondary.main' : 'primary.main', mb: 1.5 }} />
          <Typography variant="h6" mb={0.5}>
            {bundleMode ? 'Add files to bundle' : 'Drag & drop submission files here'}
          </Typography>
          <Typography color="text.secondary" variant="body2" mb={0.5}>or click to browse</Typography>
          <Typography variant="caption" color="text.secondary" display="block" mb={2}>
            .eml · .msg · .pdf · .docx · .xlsx · .txt · .csv · .zip
          </Typography>
          <Button
            variant="outlined"
            color={bundleMode ? 'secondary' : 'primary'}
            component="span"
            onClick={e => { e.stopPropagation(); fileInputRef.current?.click(); }}
          >
            Browse Files
          </Button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".eml,.msg,.pdf,.docx,.doc,.xlsx,.xls,.txt,.csv,.zip"
            multiple
            hidden
            onChange={handleFileChange}
          />
        </CardContent>
      </Card>

      {/* File List */}
      {items.length > 0 && (
        <Card elevation={2} sx={{ mb: 3 }}>
          <CardContent>
            <Stack direction="row" justifyContent="space-between" alignItems="center" mb={2}>
              <Stack direction="row" spacing={1} alignItems="center">
                <Typography variant="h6">Files ({items.length})</Typography>
                {bundleMode && (
                  <Chip label="→ 1 submission" size="small" color="secondary" variant="outlined" />
                )}
              </Stack>
              <Stack direction="row" spacing={1}>
                <Button variant="outlined" size="small" onClick={handleClear} disabled={uploading}>
                  Clear
                </Button>
                <Button
                  variant="contained"
                  color={bundleMode ? 'secondary' : 'primary'}
                  onClick={handleUpload}
                  disabled={uploading || pendingCount === 0}
                  startIcon={uploading ? <CircularProgress size={16} color="inherit" /> : <CloudUpload />}
                >
                  {uploading
                    ? 'Processing…'
                    : bundleMode
                      ? `Upload Bundle (${pendingCount} files)`
                      : `Upload ${pendingCount} file${pendingCount !== 1 ? 's' : ''}`}
                </Button>
              </Stack>
            </Stack>

            {uploading && <LinearProgress variant="determinate" value={progress} color={bundleMode ? 'secondary' : 'primary'} sx={{ mb: 2 }} />}

            <List dense disablePadding>
              {items.map((item, i) => {
                const isPrimary = bundleMode && item.file.name === primaryFileName;
                return (
                  <ListItem
                    key={i}
                    divider={i < items.length - 1}
                    sx={{ bgcolor: isPrimary ? 'action.selected' : undefined, borderRadius: 1 }}
                    secondaryAction={
                      item.status === 'success' && item.submissionId ? (
                        <Button size="small" startIcon={<OpenInNew />}
                          onClick={() => navigate(`/submissions/${item.submissionId}`)}>
                          View
                        </Button>
                      ) : bundleMode && item.status === 'pending' && !isPrimary ? (
                        <Tooltip title="Set as Primary">
                          <IconButton size="small" onClick={() => setPrimaryFileName(item.file.name)}>
                            <StarBorder fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      ) : null
                    }
                  >
                    <ListItemIcon sx={{ minWidth: 36 }}>
                      {item.status === 'pending' && <AttachFile color="action" fontSize="small" />}
                      {item.status === 'uploading' && <CircularProgress size={18} />}
                      {item.status === 'success' && <CheckCircle color="success" fontSize="small" />}
                      {item.status === 'error' && <Error color="error" fontSize="small" />}
                    </ListItemIcon>
                    <ListItemText
                      primary={
                        <Box display="flex" alignItems="center" flexWrap="wrap" gap={0.5}>
                          <FileTypeChip fileName={item.file.name} />
                          {bundleMode && isPrimary && (
                            <Chip
                              icon={<Star sx={{ fontSize: '0.75rem !important' }} />}
                              label="Primary"
                              size="small"
                              color="secondary"
                              sx={{ fontSize: '0.68rem', height: 20 }}
                            />
                          )}
                          {bundleMode && !isPrimary && item.status === 'pending' && (
                            <Chip label="Attachment" size="small" variant="outlined" sx={{ fontSize: '0.68rem', height: 20 }} />
                          )}
                          <Typography variant="body2">{item.file.name}</Typography>
                        </Box>
                      }
                      secondary={
                        item.status === 'error' ? item.error :
                        item.status === 'success' && !bundleMode ? `Submission ID: ${item.submissionId}` :
                        `${(item.file.size / 1024).toFixed(0)} KB`
                      }
                      secondaryTypographyProps={{ color: item.status === 'error' ? 'error' : 'text.secondary' }}
                    />
                  </ListItem>
                );
              })}
            </List>
          </CardContent>
        </Card>
      )}

      {/* Bundle success summary */}
      {bundleMode && bundleResult && (
        <Alert
          severity="success"
          sx={{ mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={() => navigate(`/submissions/${bundleResult.submissionId}`)}>
              View Submission
            </Button>
          }
        >
          <strong>Bundle processed as Submission #{bundleResult.submissionId}</strong>
          <Box mt={0.5}>
            <Chip icon={<Star />} label={bundleResult.primaryFile} size="small" color="secondary" sx={{ mr: 0.5 }} />
            {bundleResult.attachmentFiles.map(f => (
              <Chip key={f} label={f} size="small" variant="outlined" sx={{ mr: 0.5 }} />
            ))}
          </Box>
        </Alert>
      )}

      {/* Individual mode success */}
      {!bundleMode && successCount > 0 && (
        <Alert
          severity="success"
          action={<Button color="inherit" size="small" onClick={() => navigate('/submissions')}>View All</Button>}
        >
          Successfully processed {successCount} submission{successCount !== 1 ? 's' : ''}!
        </Alert>
      )}

      {/* How it works */}
      <Card elevation={1} sx={{ mt: 3, bgcolor: 'grey.50' }}>
        <CardContent>
          <Typography variant="subtitle2" gutterBottom>Modes</Typography>
          <Stack spacing={1.5}>
            <Box>
              <Typography variant="body2" fontWeight={600}>Individual Mode (default)</Typography>
              <Typography variant="body2" color="text.secondary">
                Each file is its own submission. Useful when uploading many independent emails or documents.
              </Typography>
            </Box>
            <Divider />
            <Box>
              <Typography variant="body2" fontWeight={600}>Bundle Mode</Typography>
              <Typography variant="body2" color="text.secondary">
                All files are grouped as <strong>one submission</strong>. The <Star fontSize="small" sx={{ verticalAlign: 'middle' }} /> Primary
                file is the email/main doc; all others are injected as its attachments before Claude processes them.
                Perfect for: <em>email.eml + loss_run.pdf + acord_form.docx</em>.
              </Typography>
            </Box>
            <Divider />
            <Box>
              <Typography variant="body2" color="text.secondary">
                Supported: .eml · .msg · .pdf · .docx · .xlsx · .txt · .csv · .zip
                &nbsp;·&nbsp; Confidence &lt;70% → flagged as <strong>Needs Review</strong>
              </Typography>
            </Box>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
