import { useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { jsPDF } from 'jspdf';
import {
  Box, Button, Chip, CircularProgress, Collapse, Divider, IconButton,
  InputAdornment, Paper, Stack, TextField, Tooltip, Typography,
} from '@mui/material';
import { ExpandLess, ExpandMore, PictureAsPdf, Send, SmartToy, Person, Build } from '@mui/icons-material';
import { chatApi } from '../api/client';
import type { ToolCallRecord } from '../types/chat';

// ─── Suggested starter questions ────────────────────────────────────────────

const SUGGESTIONS = [
  'Show me all submissions that need review',
  'Which brokers have submitted the most this month?',
  'What is the total loss amount across all submissions?',
  'Compare submissions 1 and 2',
  'Show me all trucking submissions with more than 10 trucks',
  'What are the most common lines of business?',
  'Which submissions have open claims?',
  'Show me analytics for all submissions',
];

// ─── Tool call accordion ─────────────────────────────────────────────────────

function ToolCallDetail({ record }: { record: ToolCallRecord }) {
  const [open, setOpen] = useState(false);

  let parsedOutput: unknown = record.output;
  let isJson = false;
  try {
    parsedOutput = JSON.parse(record.output);
    isJson = true;
  } catch { /* keep as string */ }

  let parsedInput: unknown = record.input;
  try { parsedInput = JSON.parse(record.input); } catch { /* keep as string */ }

  return (
    <Box
      sx={{
        border: '1px solid',
        borderColor: 'divider',
        borderRadius: 1,
        overflow: 'hidden',
        mt: 0.5,
      }}
    >
      <Stack
        direction="row"
        alignItems="center"
        spacing={1}
        px={1.5}
        py={0.5}
        sx={{ bgcolor: 'grey.100', cursor: 'pointer' }}
        onClick={() => setOpen(v => !v)}
      >
        <Build sx={{ fontSize: 14, color: 'text.secondary' }} />
        <Typography variant="caption" fontFamily="monospace" fontWeight={600} color="text.secondary">
          {record.toolName}
        </Typography>
        <Box flex={1} />
        {open ? <ExpandLess fontSize="small" /> : <ExpandMore fontSize="small" />}
      </Stack>

      <Collapse in={open}>
        <Box px={1.5} py={1}>
          {record.input && (
            <>
              <Typography variant="caption" color="text.secondary" fontWeight={600}>Input</Typography>
              <Box
                component="pre"
                sx={{
                  fontSize: '0.7rem',
                  bgcolor: 'grey.50',
                  p: 1,
                  borderRadius: 0.5,
                  overflow: 'auto',
                  maxHeight: 120,
                  mt: 0.5,
                  mb: 1,
                }}
              >
                {typeof parsedInput === 'object'
                  ? JSON.stringify(parsedInput, null, 2)
                  : String(parsedInput)}
              </Box>
            </>
          )}
          <Typography variant="caption" color="text.secondary" fontWeight={600}>
            Output {isJson && `(${Array.isArray((parsedOutput as any)?.rows ?? parsedOutput) ? (parsedOutput as any[]).length : (parsedOutput as any)?.rowCount ?? ''} rows)`}
          </Typography>
          <Box
            component="pre"
            sx={{
              fontSize: '0.7rem',
              bgcolor: 'grey.50',
              p: 1,
              borderRadius: 0.5,
              overflow: 'auto',
              maxHeight: 200,
              mt: 0.5,
            }}
          >
            {typeof parsedOutput === 'object'
              ? JSON.stringify(parsedOutput, null, 2)
              : String(parsedOutput)}
          </Box>
        </Box>
      </Collapse>
    </Box>
  );
}

// ─── Single message bubble ────────────────────────────────────────────────────

function MessageBubble({
  role,
  content,
  toolCalls,
}: {
  role: 'user' | 'assistant';
  content: string;
  toolCalls?: ToolCallRecord[];
}) {
  const isUser = role === 'user';

  return (
    <Stack
      direction="row"
      spacing={1.5}
      alignItems="flex-start"
      justifyContent={isUser ? 'flex-end' : 'flex-start'}
    >
      {!isUser && (
        <Box
          sx={{
            width: 32,
            height: 32,
            borderRadius: '50%',
            bgcolor: 'primary.main',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
            mt: 0.5,
          }}
        >
          <SmartToy sx={{ fontSize: 18, color: 'white' }} />
        </Box>
      )}

      <Box maxWidth="80%">
        <Paper
          elevation={0}
          sx={{
            px: 2,
            py: 1.5,
            bgcolor: isUser ? 'primary.main' : 'background.paper',
            color: isUser ? 'primary.contrastText' : 'text.primary',
            border: isUser ? 'none' : '1px solid',
            borderColor: 'divider',
            borderRadius: isUser ? '16px 16px 4px 16px' : '16px 16px 16px 4px',
          }}
        >
          {isUser ? (
            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>{content}</Typography>
          ) : (
            <Box
              sx={{
                '& p': { mt: 0, mb: 1, '&:last-child': { mb: 0 } },
                '& table': { borderCollapse: 'collapse', width: '100%', fontSize: '0.8rem', my: 1 },
                '& th, & td': { border: '1px solid', borderColor: 'divider', px: 1, py: 0.5 },
                '& th': { bgcolor: 'grey.100', fontWeight: 700 },
                '& ul, & ol': { pl: 2, mt: 0 },
                '& code': { fontFamily: 'monospace', bgcolor: 'grey.100', px: 0.5, borderRadius: 0.5 },
                '& pre': { bgcolor: 'grey.100', p: 1, borderRadius: 1, overflow: 'auto', fontSize: '0.8rem' },
                '& h1, & h2, & h3': { mt: 1, mb: 0.5 },
              }}
            >
              <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>
            </Box>
          )}
        </Paper>

        {/* Tool calls accordion */}
        {toolCalls && toolCalls.length > 0 && (
          <Box mt={0.5}>
            <Stack direction="row" spacing={0.5} flexWrap="wrap" mb={0.5}>
              {toolCalls.map((tc, i) => (
                <Chip
                  key={i}
                  icon={<Build sx={{ fontSize: '0.75rem !important' }} />}
                  label={tc.toolName}
                  size="small"
                  variant="outlined"
                  color="primary"
                  sx={{ fontSize: '0.65rem', height: 20 }}
                />
              ))}
            </Stack>
            {toolCalls.map((tc, i) => (
              <ToolCallDetail key={i} record={tc} />
            ))}
          </Box>
        )}
      </Box>

      {isUser && (
        <Box
          sx={{
            width: 32,
            height: 32,
            borderRadius: '50%',
            bgcolor: 'grey.300',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
            mt: 0.5,
          }}
        >
          <Person sx={{ fontSize: 18, color: 'grey.700' }} />
        </Box>
      )}
    </Stack>
  );
}

// ─── Main component ───────────────────────────────────────────────────────────

interface LocalMessage {
  role: 'user' | 'assistant';
  content: string;
  toolCalls?: ToolCallRecord[];
}

export default function Chat() {
  const [messages, setMessages] = useState<LocalMessage[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom on new messages
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, loading]);

  const handleSend = async (text?: string) => {
    const userText = (text ?? input).trim();
    if (!userText || loading) return;

    const newUserMsg: LocalMessage = { role: 'user', content: userText };
    const updatedMessages = [...messages, newUserMsg];

    setMessages(updatedMessages);
    setInput('');
    setLoading(true);

    try {
      // Build the full conversation history for the API (no toolCalls in API messages)
      const apiMessages = updatedMessages.map(m => ({ role: m.role, content: m.content }));
      const res = await chatApi.send(apiMessages);

      setMessages(prev => [
        ...prev,
        {
          role: 'assistant',
          content: res.data.answer,
          toolCalls: res.data.toolCalls,
        },
      ]);
    } catch (err: any) {
      const errMsg = err.response?.data?.error ?? err.message ?? 'Unknown error';
      setMessages(prev => [
        ...prev,
        { role: 'assistant', content: `❌ Error: ${errMsg}` },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleExportPdf = () => {
    const doc = new jsPDF({ unit: 'mm', format: 'a4' });
    const pageW = doc.internal.pageSize.getWidth();
    const pageH = doc.internal.pageSize.getHeight();
    const margin = 15;
    const maxW = pageW - margin * 2;
    let y = margin;

    const addPage = () => {
      doc.addPage();
      y = margin;
    };

    const writeLines = (lines: string[], fontSize: number, color: [number, number, number]) => {
      doc.setFontSize(fontSize);
      doc.setTextColor(...color);
      for (const line of lines) {
        if (y + fontSize * 0.4 > pageH - margin) addPage();
        doc.text(line, margin, y);
        y += fontSize * 0.5;
      }
    };

    // Title
    doc.setFont('helvetica', 'bold');
    writeLines(['Ask AI — Conversation Export'], 16, [30, 80, 162]);
    doc.setFont('helvetica', 'normal');
    writeLines([`Exported: ${new Date().toLocaleString()}`], 9, [120, 120, 120]);
    y += 4;

    // Messages
    for (const msg of messages) {
      const isUser = msg.role === 'user';

      // Role label
      doc.setFont('helvetica', 'bold');
      writeLines([isUser ? 'You' : 'AI'], 10, isUser ? [30, 80, 162] : [46, 125, 50]);

      // Strip markdown for clean PDF output
      const plainText = msg.content
        .replace(/#{1,6}\s+/g, '')
        .replace(/\*\*(.+?)\*\*/g, '$1')
        .replace(/\*(.+?)\*/g, '$1')
        .replace(/`(.+?)`/g, '$1')
        .replace(/\[(.+?)\]\(.+?\)/g, '$1')
        .replace(/^\s*[-*]\s+/gm, '• ')
        .replace(/\n{3,}/g, '\n\n');

      doc.setFont('helvetica', 'normal');
      const wrapped = doc.splitTextToSize(plainText, maxW);
      writeLines(wrapped, 10, [30, 30, 30]);
      y += 5;

      // Tool calls summary
      if (msg.toolCalls && msg.toolCalls.length > 0) {
        const toolNames = msg.toolCalls.map(tc => tc.toolName).join(', ');
        doc.setFont('helvetica', 'italic');
        writeLines([`[Tools used: ${toolNames}]`], 8, [130, 130, 130]);
        doc.setFont('helvetica', 'normal');
        y += 2;
      }

      // Separator
      if (y + 4 < pageH - margin) {
        doc.setDrawColor(220, 220, 220);
        doc.line(margin, y, pageW - margin, y);
        y += 5;
      } else {
        addPage();
      }
    }

    doc.save(`ask-ai-conversation-${Date.now()}.pdf`);
  };

  return (
    <Box display="flex" flexDirection="column" height="calc(100vh - 112px)">
      <Stack direction="row" alignItems="center" spacing={1} mb={2}>
        <SmartToy color="primary" />
        <Typography variant="h4" fontWeight={700}>AI Insights</Typography>
        <Chip label="Claude Tool Use" size="small" color="primary" variant="outlined" />
        <Box flex={1} />
        <Tooltip title="Export conversation as PDF">
          <span>
            <Button
              variant="outlined"
              size="small"
              startIcon={<PictureAsPdf />}
              onClick={handleExportPdf}
              disabled={messages.length === 0}
            >
              Export PDF
            </Button>
          </span>
        </Tooltip>
      </Stack>

      {/* Messages area */}
      <Paper
        elevation={2}
        sx={{
          flex: 1,
          overflow: 'auto',
          p: 3,
          display: 'flex',
          flexDirection: 'column',
          gap: 2,
          mb: 2,
          bgcolor: 'grey.50',
        }}
      >
        {messages.length === 0 && (
          <Box textAlign="center" mt={4}>
            <SmartToy sx={{ fontSize: 64, color: 'primary.main', opacity: 0.3, mb: 2 }} />
            <Typography variant="h6" color="text.secondary" mb={1}>
              Ask anything about your insurance submissions
            </Typography>
            <Typography variant="body2" color="text.secondary" mb={3}>
              Claude will automatically query the database using tools to answer your questions.
            </Typography>
            <Stack direction="row" flexWrap="wrap" justifyContent="center" gap={1}>
              {SUGGESTIONS.map(s => (
                <Chip
                  key={s}
                  label={s}
                  onClick={() => handleSend(s)}
                  color="primary"
                  variant="outlined"
                  size="small"
                  sx={{ cursor: 'pointer' }}
                />
              ))}
            </Stack>
          </Box>
        )}

        {messages.map((msg, i) => (
          <MessageBubble
            key={i}
            role={msg.role}
            content={msg.content}
            toolCalls={msg.toolCalls}
          />
        ))}

        {loading && (
          <Stack direction="row" spacing={1.5} alignItems="center">
            <Box
              sx={{
                width: 32, height: 32, borderRadius: '50%', bgcolor: 'primary.main',
                display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
              }}
            >
              <SmartToy sx={{ fontSize: 18, color: 'white' }} />
            </Box>
            <Paper
              elevation={0}
              sx={{ px: 2, py: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: '16px 16px 16px 4px' }}
            >
              <Stack direction="row" spacing={1} alignItems="center">
                <CircularProgress size={14} />
                <Typography variant="body2" color="text.secondary">
                  Querying database…
                </Typography>
              </Stack>
            </Paper>
          </Stack>
        )}

        <div ref={bottomRef} />
      </Paper>

      {/* Input area */}
      <Paper elevation={2} sx={{ p: 1.5 }}>
        {messages.length > 0 && (
          <>
            <Stack direction="row" spacing={0.5} flexWrap="wrap" mb={1}>
              {SUGGESTIONS.slice(0, 4).map(s => (
                <Chip
                  key={s}
                  label={s}
                  onClick={() => handleSend(s)}
                  size="small"
                  variant="outlined"
                  sx={{ cursor: 'pointer', fontSize: '0.65rem' }}
                  disabled={loading}
                />
              ))}
            </Stack>
            <Divider sx={{ mb: 1 }} />
          </>
        )}
        <Stack direction="row" spacing={1}>
          <TextField
            fullWidth
            multiline
            maxRows={4}
            placeholder="Ask about submissions, losses, brokers, risk exposure… (Enter to send, Shift+Enter for new line)"
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            disabled={loading}
            variant="outlined"
            size="small"
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <Tooltip title="Send (Enter)">
                    <span>
                      <IconButton
                        onClick={() => handleSend()}
                        disabled={!input.trim() || loading}
                        color="primary"
                        size="small"
                      >
                        {loading ? <CircularProgress size={18} /> : <Send />}
                      </IconButton>
                    </span>
                  </Tooltip>
                </InputAdornment>
              ),
            }}
          />
        </Stack>
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
          Claude uses tool calls to query your live submission data — it never makes up answers.
        </Typography>
      </Paper>
    </Box>
  );
}
