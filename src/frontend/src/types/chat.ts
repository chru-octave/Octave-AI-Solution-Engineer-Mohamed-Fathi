export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ToolCallRecord {
  toolName: string;
  input: string;
  output: string;
}

export interface ChatResponse {
  answer: string;
  toolCalls: ToolCallRecord[];
}
