import { useState } from 'react';
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import {
  AppBar, Box, Drawer, IconButton, List, ListItem, ListItemButton,
  ListItemIcon, ListItemText, Toolbar, Typography, useTheme
} from '@mui/material';
import { Assignment, CloudUpload, Dashboard, Menu, RocketLaunch, SmartToy } from '@mui/icons-material';
import DashboardPage from './pages/Dashboard';
import SubmissionsList from './pages/SubmissionsList';
import SubmissionDetail from './pages/SubmissionDetail';
import Upload from './pages/Upload';
import Chat from './pages/Chat';
import FutureWork from './pages/FutureWork';

const DRAWER_WIDTH = 220;

const navItems = [
  { path: '/', label: 'Dashboard', icon: <Dashboard /> },
  { path: '/submissions', label: 'Submissions', icon: <Assignment /> },
  { path: '/upload', label: 'Upload', icon: <CloudUpload /> },
  { path: '/chat', label: 'AI Insights', icon: <SmartToy /> },
  { path: '/future-work', label: 'Future Work', icon: <RocketLaunch /> },
];

function NavDrawer() {
  const location = useLocation();
  return (
    <Drawer
      variant="permanent"
      sx={{
        width: DRAWER_WIDTH,
        flexShrink: 0,
        display: { xs: 'none', sm: 'block' },
        '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box', top: 64 },
      }}
    >
      <List>
        {navItems.map(item => (
          <ListItem key={item.path} disablePadding>
            <ListItemButton
              component={Link}
              to={item.path}
              selected={
                item.path === '/'
                  ? location.pathname === '/'
                  : location.pathname.startsWith(item.path)
              }
            >
              <ListItemIcon>{item.icon}</ListItemIcon>
              <ListItemText primary={item.label} />
            </ListItemButton>
          </ListItem>
        ))}
      </List>
    </Drawer>
  );
}

export default function App() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const theme = useTheme();

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" sx={{ zIndex: theme.zIndex.drawer + 1 }}>
        <Toolbar>
          <IconButton
            color="inherit"
            onClick={() => setMobileOpen(!mobileOpen)}
            sx={{ mr: 1, display: { sm: 'none' } }}
          >
            <Menu />
          </IconButton>
          <Typography variant="h6" fontWeight={700} noWrap>
            Insurance Extraction
          </Typography>
          <Typography variant="caption" sx={{ ml: 1, opacity: 0.7 }}>
            AI-Powered
          </Typography>
        </Toolbar>
      </AppBar>

      <NavDrawer />

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          p: 3,
          mt: '64px',
          ml: { sm: `${DRAWER_WIDTH}px` },
          bgcolor: 'grey.50',
          minHeight: 'calc(100vh - 64px)',
        }}
      >
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/submissions" element={<SubmissionsList />} />
          <Route path="/submissions/:id" element={<SubmissionDetail />} />
          <Route path="/upload" element={<Upload />} />
          <Route path="/chat" element={<Chat />} />
          <Route path="/future-work" element={<FutureWork />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Box>
    </Box>
  );
}
