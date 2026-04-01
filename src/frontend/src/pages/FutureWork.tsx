import {
  Box, Card, CardContent, Chip, Grid, Stack, Typography,
} from '@mui/material';
import {
  AccountTree, AutoAwesome, CloudQueue, Extension, IntegrationInstructions,
  ManageSearch, Policy, QueryStats, RocketLaunch, Security, Speed, Tune,
} from '@mui/icons-material';

// ─── Data ────────────────────────────────────────────────────────────────────

interface Enhancement {
  title: string;
  description: string;
  impact: 'High' | 'Medium' | 'Low';
  effort: 'High' | 'Medium' | 'Low';
  tags: string[];
}

interface Category {
  label: string;
  icon: React.ReactNode;
  color: string;
  items: Enhancement[];
}

const CATEGORIES: Category[] = [
  {
    label: 'AI & Extraction Quality',
    icon: <AutoAwesome />,
    color: '#7b1fa2',
    items: [
      {
        title: 'Streaming AI Responses',
        description:
          'Stream tokens from Claude in the Ask AI tab so answers appear progressively instead of waiting for the full response — dramatically improves perceived speed for long answers.',
        impact: 'High',
        effort: 'Low',
        tags: ['UX', 'Claude API', 'WebSockets'],
      },
      {
        title: 'Human-in-the-Loop Feedback Loop',
        description:
          'Let underwriters mark extracted fields as correct or wrong. Feed corrections back as fine-tuning examples or few-shot prompts to continuously raise extraction accuracy.',
        impact: 'High',
        effort: 'Medium',
        tags: ['ML', 'Data Quality', 'Fine-tuning'],
      },
      {
        title: 'Multi-Model Fallback',
        description:
          'Support GPT-4o and Gemini alongside Claude. Route requests to the cheapest model that meets a confidence threshold; fall back to a stronger model on low-confidence extractions.',
        impact: 'Medium',
        effort: 'Medium',
        tags: ['Resilience', 'Cost Optimisation', 'LLM'],
      },
      {
        title: 'OCR for Scanned Documents',
        description:
          'Integrate Azure Document Intelligence or Tesseract to extract text from scanned PDF attachments and image-based forms before passing content to Claude.',
        impact: 'High',
        effort: 'Medium',
        tags: ['OCR', 'PDF', 'Azure'],
      },
      {
        title: 'Domain-Specific Validation Rules',
        description:
          'Define per-line-of-business validation schemas (e.g. trucking must have vehicle count, GL must have revenue). Flag extractions that violate rules before storing them.',
        impact: 'High',
        effort: 'Medium',
        tags: ['Data Quality', 'Rules Engine', 'Insurance'],
      },
    ],
  },
  {
    label: 'Workflow & Business Logic',
    icon: <AccountTree />,
    color: '#1565c0',
    items: [
      {
        title: 'Underwriter Review Workflow',
        description:
          'Add an approval queue where underwriters can accept, edit, or reject extracted submissions. Track reviewer identity, timestamps, and comments for a full audit trail.',
        impact: 'High',
        effort: 'High',
        tags: ['Workflow', 'UX', 'Audit'],
      },
      {
        title: 'Automated Risk Scoring',
        description:
          'Use extracted data (loss history, revenue, vehicle count, etc.) to compute a preliminary risk score per submission using a rules engine or lightweight ML model.',
        impact: 'High',
        effort: 'High',
        tags: ['Analytics', 'ML', 'Underwriting'],
      },
      {
        title: 'Renewal Detection & Tracking',
        description:
          'Detect when an incoming submission is a renewal of an existing account by matching insured name, FEIN, or policy number. Link renewals to their prior-year records.',
        impact: 'Medium',
        effort: 'Medium',
        tags: ['Business Logic', 'Deduplication'],
      },
      {
        title: 'Email Notifications & Alerts',
        description:
          'Send automated emails to brokers confirming receipt, to underwriters when a high-priority submission arrives, and to admins when extraction fails repeatedly.',
        impact: 'Medium',
        effort: 'Low',
        tags: ['Email', 'Notifications', 'SMTP'],
      },
    ],
  },
  {
    label: 'Integrations',
    icon: <IntegrationInstructions />,
    color: '#00695c',
    items: [
      {
        title: 'Policy Management System Integration',
        description:
          'Push accepted submissions directly into Applied Epic, Guidewire, or AMS360 via their APIs, eliminating manual re-entry and removing the biggest friction in the current workflow.',
        impact: 'High',
        effort: 'High',
        tags: ['Applied Epic', 'Guidewire', 'API'],
      },
      {
        title: 'Direct Mailbox Ingestion',
        description:
          'Poll an IMAP/Exchange inbox or subscribe to a Microsoft 365 webhook so submissions are ingested automatically the moment an email arrives — no manual upload needed.',
        impact: 'High',
        effort: 'Medium',
        tags: ['IMAP', 'Microsoft 365', 'Automation'],
      },
      {
        title: 'Slack / Teams Notifications',
        description:
          'Post a structured message to a Slack channel or Teams channel when a new submission is processed, including key fields and a link to the detail page.',
        impact: 'Medium',
        effort: 'Low',
        tags: ['Slack', 'Teams', 'Webhooks'],
      },
    ],
  },
  {
    label: 'Search & Analytics',
    icon: <QueryStats />,
    color: '#e65100',
    items: [
      {
        title: 'Full-Text Search with Filters',
        description:
          'Add advanced search across all extracted fields (insured name, broker, lines, loss description) with date-range, status, and confidence filters. Back it with SQLite FTS5 or Elasticsearch.',
        impact: 'High',
        effort: 'Medium',
        tags: ['Search', 'UX', 'SQLite FTS5'],
      },
      {
        title: 'Saved Ask-AI Queries',
        description:
          'Allow users to bookmark frequently asked questions and replay them with one click — useful for standard daily reports like "all trucking submissions this week".',
        impact: 'Medium',
        effort: 'Low',
        tags: ['UX', 'Productivity', 'Ask AI'],
      },
      {
        title: 'Exportable Analytics Reports',
        description:
          'Let users download the dashboard charts and underlying data as Excel or PDF reports, scheduled on a weekly cadence and emailed to stakeholders automatically.',
        impact: 'Medium',
        effort: 'Medium',
        tags: ['Reporting', 'Excel', 'Scheduling'],
      },
    ],
  },
  {
    label: 'Infrastructure & Scale',
    icon: <CloudQueue />,
    color: '#0277bd',
    items: [
      {
        title: 'Async Processing with a Message Queue',
        description:
          'Move email parsing and Claude extraction off the HTTP request path into a background worker fed by RabbitMQ or Azure Service Bus. Enables horizontal scaling and prevents timeouts on large batches.',
        impact: 'High',
        effort: 'High',
        tags: ['RabbitMQ', 'Azure Service Bus', 'Scalability'],
      },
      {
        title: 'Cloud Storage for Attachments',
        description:
          'Store raw .eml files and PDF attachments in Azure Blob Storage or S3 instead of the local filesystem — makes the service stateless and ready for multi-instance deployment.',
        impact: 'High',
        effort: 'Medium',
        tags: ['Azure Blob', 'S3', 'Stateless'],
      },
      {
        title: 'Containerisation & Kubernetes',
        description:
          'Dockerise the API and frontend, define Helm charts, and deploy to AKS or EKS. Enables zero-downtime deployments, auto-scaling, and consistent environments across dev/staging/prod.',
        impact: 'Medium',
        effort: 'High',
        tags: ['Docker', 'Kubernetes', 'DevOps'],
      },
      {
        title: 'Production-Grade Database',
        description:
          'Replace SQLite with PostgreSQL or Azure SQL for concurrent writes, connection pooling, point-in-time recovery, and row-level security — critical at any meaningful submission volume.',
        impact: 'High',
        effort: 'Medium',
        tags: ['PostgreSQL', 'Azure SQL', 'EF Core'],
      },
    ],
  },
  {
    label: 'Security & Compliance',
    icon: <Security />,
    color: '#b71c1c',
    items: [
      {
        title: 'Role-Based Access Control (RBAC)',
        description:
          'Introduce user roles (Admin, Underwriter, Read-Only). Gate API endpoints and UI actions by role so junior staff cannot approve submissions or access raw email content.',
        impact: 'High',
        effort: 'Medium',
        tags: ['Auth', 'RBAC', 'Azure AD'],
      },
      {
        title: 'PII Masking & Data Retention',
        description:
          'Automatically detect and mask sensitive PII (SSN, DOB, bank details) in stored extraction text. Implement configurable retention policies that purge old submissions per regulatory requirements.',
        impact: 'High',
        effort: 'High',
        tags: ['Compliance', 'PII', 'GDPR'],
      },
      {
        title: 'Full Audit Log',
        description:
          'Record every state change — who uploaded, who edited a field, who approved — to an append-only audit table. Surface it in the UI and make it exportable for compliance audits.',
        impact: 'Medium',
        effort: 'Medium',
        tags: ['Audit', 'Compliance', 'SOC 2'],
      },
    ],
  },
  {
    label: 'Developer Experience',
    icon: <Extension />,
    color: '#37474f',
    items: [
      {
        title: 'Automated Test Suite',
        description:
          'Add xUnit integration tests that spin up the API against a real SQLite database and run the full extraction pipeline against fixture .eml files to catch regressions automatically.',
        impact: 'High',
        effort: 'Medium',
        tags: ['xUnit', 'CI/CD', 'Testing'],
      },
      {
        title: 'OpenAPI / Swagger Documentation',
        description:
          'Enrich the existing Swagger spec with request/response examples, error codes, and authentication details. Publish it to an API portal so integrators can self-serve.',
        impact: 'Medium',
        effort: 'Low',
        tags: ['OpenAPI', 'Documentation', 'DX'],
      },
      {
        title: 'Prompt Versioning & A/B Testing',
        description:
          'Store Claude system prompts in the database with version numbers. Run A/B experiments to measure which prompt variant yields higher extraction confidence scores on held-out emails.',
        impact: 'Medium',
        effort: 'Medium',
        tags: ['Prompt Engineering', 'Experimentation', 'LLMOps'],
      },
    ],
  },
];

// ─── Helpers ──────────────────────────────────────────────────────────────────

const IMPACT_COLOR: Record<string, 'error' | 'warning' | 'success'> = {
  High: 'error',
  Medium: 'warning',
  Low: 'success',
};

const EFFORT_COLOR: Record<string, 'error' | 'warning' | 'success'> = {
  High: 'error',
  Medium: 'warning',
  Low: 'success',
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function FutureWork() {
  const totalItems = CATEGORIES.reduce((sum, c) => sum + c.items.length, 0);
  const highImpact = CATEGORIES.flatMap(c => c.items).filter(i => i.impact === 'High').length;

  return (
    <Box>
      {/* Header */}
      <Stack direction="row" alignItems="center" spacing={1.5} mb={1}>
        <RocketLaunch color="primary" sx={{ fontSize: 32 }} />
        <Typography variant="h4" fontWeight={700}>Future Work</Typography>
      </Stack>
      <Typography variant="body1" color="text.secondary" mb={1}>
        Possible enhancements to evolve this proof-of-concept into a production-ready
        insurance submission platform.
      </Typography>

      {/* Summary chips */}
      <Stack direction="row" spacing={1} mb={4} flexWrap="wrap">
        <Chip icon={<ManageSearch />} label={`${totalItems} enhancements`} variant="outlined" />
        <Chip icon={<Speed />} label={`${highImpact} high-impact`} color="error" variant="outlined" />
        <Chip icon={<Tune />} label={`${CATEGORIES.length} categories`} color="primary" variant="outlined" />
        <Chip icon={<Policy />} label="Ordered by category" variant="outlined" />
      </Stack>

      {/* Categories */}
      {CATEGORIES.map(category => (
        <Box key={category.label} mb={5}>
          {/* Category header */}
          <Stack direction="row" alignItems="center" spacing={1} mb={2}>
            <Box sx={{ color: category.color }}>{category.icon}</Box>
            <Typography variant="h6" fontWeight={700} sx={{ color: category.color }}>
              {category.label}
            </Typography>
            <Chip
              label={`${category.items.length} items`}
              size="small"
              sx={{ bgcolor: category.color, color: 'white', fontWeight: 600 }}
            />
          </Stack>

          {/* Enhancement cards */}
          <Grid container spacing={2}>
            {category.items.map(item => (
              <Grid size={{ xs: 12, md: 6, lg: 4 }} key={item.title}>
                <Card
                  elevation={2}
                  sx={{
                    height: '100%',
                    display: 'flex',
                    flexDirection: 'column',
                    borderTop: `3px solid ${category.color}`,
                    transition: 'box-shadow 0.2s',
                    '&:hover': { boxShadow: 6 },
                  }}
                >
                  <CardContent sx={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                    <Typography variant="subtitle1" fontWeight={700}>
                      {item.title}
                    </Typography>

                    <Typography variant="body2" color="text.secondary" sx={{ flex: 1 }}>
                      {item.description}
                    </Typography>

                    {/* Impact / Effort badges */}
                    <Stack direction="row" spacing={1} flexWrap="wrap">
                      <Chip
                        label={`Impact: ${item.impact}`}
                        size="small"
                        color={IMPACT_COLOR[item.impact]}
                        variant="outlined"
                        sx={{ fontWeight: 600, fontSize: '0.65rem' }}
                      />
                      <Chip
                        label={`Effort: ${item.effort}`}
                        size="small"
                        color={EFFORT_COLOR[item.effort]}
                        variant="filled"
                        sx={{ fontWeight: 600, fontSize: '0.65rem' }}
                      />
                    </Stack>

                    {/* Tags */}
                    <Stack direction="row" spacing={0.5} flexWrap="wrap" gap={0.5}>
                      {item.tags.map(tag => (
                        <Chip
                          key={tag}
                          label={tag}
                          size="small"
                          sx={{ fontSize: '0.6rem', height: 18, bgcolor: 'grey.100' }}
                        />
                      ))}
                    </Stack>
                  </CardContent>
                </Card>
              </Grid>
            ))}
          </Grid>
        </Box>
      ))}
    </Box>
  );
}
