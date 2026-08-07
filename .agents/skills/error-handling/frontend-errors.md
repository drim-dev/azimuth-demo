# Frontend Error Handling

Complete guide to frontend error handling with ApiError, error boundaries, and retry logic in drim-dev.

## Error Response Type

```typescript
// lib/types/error.ts
export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance?: string;
  errorCode?: string;
  traceId?: string;
  errors?: Record<string, string[]>;  // For validation errors
  errorCodes?: Record<string, string[]>;  // For validation error codes
}

export class ApiError extends Error {
  constructor(
    public status: number,
    public problemDetails: ProblemDetails
  ) {
    super(problemDetails.detail);
    this.name = 'ApiError';
  }

  get errorCode(): string | undefined {
    return this.problemDetails.errorCode;
  }

  get isValidationError(): boolean {
    return this.status === 400 && !!this.problemDetails.errors;
  }

  get isNotFound(): boolean {
    return this.status === 404;
  }

  get isForbidden(): boolean {
    return this.status === 403;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isServerError(): boolean {
    return this.status >= 500;
  }
}
```

## API Client with Error Handling

```typescript
// lib/api/client.ts
import { ApiError, type ProblemDetails } from '@/lib/types/error';

export async function apiRequest<T>(
  url: string,
  options?: RequestInit
): Promise<T> {
  const response = await fetch(url, options);

  if (!response.ok) {
    const problemDetails: ProblemDetails = await response.json();
    throw new ApiError(response.status, problemDetails);
  }

  return response.json();
}

export const api = {
  get: <T>(url: string) => apiRequest<T>(url, { method: 'GET' }),

  post: <T>(url: string, data: unknown) =>
    apiRequest<T>(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data)
    }),

  put: <T>(url: string, data: unknown) =>
    apiRequest<T>(url, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data)
    }),

  delete: <T>(url: string) => apiRequest<T>(url, { method: 'DELETE' })
};
```

## Component Error Handling

```typescript
// components/DeletePostButton.tsx
'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { api } from '@/lib/api/client';
import { ApiError } from '@/lib/types/error';
import { toast } from 'sonner';  // or your toast library

interface DeletePostButtonProps {
  postId: string;
}

export function DeletePostButton({ postId }: DeletePostButtonProps) {
  const router = useRouter();
  const [isDeleting, setIsDeleting] = useState(false);

  async function handleDelete() {
    if (!confirm('Are you sure you want to delete this post?')) {
      return;
    }

    setIsDeleting(true);

    try {
      await api.delete(`/api/posts/${postId}`);
      toast.success('Post deleted successfully');
      router.push('/posts');
    } catch (error) {
      if (error instanceof ApiError) {
        // Handle specific error types
        if (error.isNotFound) {
          toast.error('Post not found');
        } else if (error.isForbidden) {
          toast.error('You do not have permission to delete this post');
        } else if (error.errorCode === 'blog:post:delete:is_published') {
          toast.error('Cannot delete a published post. Unpublish it first.');
        } else {
          toast.error(error.message);
        }
      } else {
        console.error('Unexpected error:', error);
        toast.error('An unexpected error occurred');
      }
    } finally {
      setIsDeleting(false);
    }
  }

  return (
    <button
      onClick={handleDelete}
      disabled={isDeleting}
      className="btn-danger"
    >
      {isDeleting ? 'Deleting...' : 'Delete Post'}
    </button>
  );
}
```

## Error Boundary for Unexpected Errors

```typescript
// components/ErrorBoundary.tsx
'use client';

import { Component, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error?: Error;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: any) {
    console.error('Error boundary caught error:', error, errorInfo);
    // Log to error tracking service (Sentry, etc.)
  }

  render() {
    if (this.state.hasError) {
      return this.props.fallback || (
        <div className="error-container">
          <h2>Something went wrong</h2>
          <p>We're sorry for the inconvenience. Please try refreshing the page.</p>
          <button onClick={() => this.setState({ hasError: false })}>
            Try again
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
```

**Usage:**

```typescript
// app/layout.tsx
import { ErrorBoundary } from '@/components/ErrorBoundary';

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="ru">
      <body>
        <ErrorBoundary>
          {children}
        </ErrorBoundary>
      </body>
    </html>
  );
}
```

## Global Error Page (Next.js)

```typescript
// app/error.tsx
'use client';

import { useEffect } from 'react';

export default function Error({
  error,
  reset
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error('Global error:', error);
    // Log to error tracking service
  }, [error]);

  return (
    <div className="error-page">
      <h1>Something went wrong!</h1>
      <p>We're sorry for the inconvenience.</p>
      <button onClick={reset}>Try again</button>
    </div>
  );
}
```

## Not Found Page (Next.js)

```typescript
// app/not-found.tsx
import Link from 'next/link';

export default function NotFound() {
  return (
    <div className="not-found-page">
      <h1>404</h1>
      <h2>Page Not Found</h2>
      <p>The page you're looking for doesn't exist.</p>
      <Link href="/">Go back home</Link>
    </div>
  );
}
```

## Retry Logic

```typescript
// lib/api/retry.ts
export async function withRetry<T>(
  fn: () => Promise<T>,
  maxRetries = 3,
  delayMs = 1000
): Promise<T> {
  let lastError: Error | undefined;

  for (let attempt = 1; attempt <= maxRetries; attempt++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error as Error;

      // Don't retry client errors (4xx)
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        throw error;
      }

      // Don't retry on last attempt
      if (attempt === maxRetries) {
        break;
      }

      // Exponential backoff
      const delay = delayMs * Math.pow(2, attempt - 1);
      await new Promise(resolve => setTimeout(resolve, delay));

      console.log(`Retrying (attempt ${attempt + 1}/${maxRetries})...`);
    }
  }

  throw lastError;
}
```

**Usage:**

```typescript
try {
  const post = await withRetry(() => api.get(`/api/posts/${id}`));
} catch (error) {
  // Handle error after all retries failed
}
```

## Error Code i18n Mapping

```typescript
// lib/i18n/error-messages.ts
export const ERROR_MESSAGES: Record<string, string> = {
  // Auth
  'auth:unauthorized': 'Требуется авторизация',
  'auth:forbidden': 'Доступ запрещен',

  // Blog posts
  'blog:post:delete:not_found': 'Пост не найден',
  'blog:post:delete:forbidden': 'У вас нет прав на удаление этого поста',
  'blog:post:delete:is_published': 'Невозможно удалить опубликованный пост. Сначала снимите его с публикации.',
  'blog:post:publish:not_draft': 'Можно публиковать только черновики',

  // Skills
  'skills:skill:create:slug_exists': 'Навык с таким slug уже существует',
  'skills:skill:learn:already_learning': 'Вы уже изучаете этот навык',
  'skills:skill:learn:not_found': 'Навык не найден',

  // Users
  'users:profile:update:not_found': 'Пользователь не найден',
  'users:profile:update:forbidden': 'У вас нет прав на редактирование этого профиля',

  // Server
  'server:internal_error': 'Произошла непредвиденная ошибка. Пожалуйста, попробуйте позже.',
  'bff:backend:unavailable': 'Сервис временно недоступен. Пожалуйста, попробуйте позже.'
};

export function getErrorMessage(errorCode?: string, fallback?: string): string {
  if (!errorCode) return fallback || 'Произошла ошибка';
  return ERROR_MESSAGES[errorCode] || fallback || 'Произошла ошибка';
}
```

**Usage:**

```typescript
catch (error) {
  if (error instanceof ApiError) {
    const message = getErrorMessage(error.errorCode, error.message);
    toast.error(message);
  }
}
```
