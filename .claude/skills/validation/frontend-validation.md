# Frontend Validation with Zod + React Hook Form

Complete guide to implementing frontend validation using Zod and React Hook Form in drim-dev.

## Basic Zod Schema

Mirror backend FluentValidation rules in Zod for client-side validation.

```typescript
// lib/validations/post.ts
import { z } from 'zod';

export const createPostSchema = z.object({
  title: z
    .string()
    .min(1, 'Title is required')
    .max(200, 'Title must be 200 characters or less'),

  slug: z
    .string()
    .min(1, 'Slug is required')
    .regex(/^[a-z0-9-]+$/, 'Slug must be lowercase letters, numbers, and hyphens only'),

  content: z
    .string()
    .min(1, 'Content is required'),

  authorEmail: z
    .string()
    .email('Invalid email format'),

  website: z
    .string()
    .url('Invalid URL format')
    .optional()
    .or(z.literal('')),  // Allow empty string
});

// Infer TypeScript type from Zod schema
export type CreatePostInput = z.infer<typeof createPostSchema>;
```

## React Hook Form Integration

```typescript
// components/CreatePostForm.tsx
'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { createPostSchema, type CreatePostInput } from '@/lib/validations/post';

export function CreatePostForm() {
  const form = useForm<CreatePostInput>({
    resolver: zodResolver(createPostSchema),
    defaultValues: {
      title: '',
      slug: '',
      content: '',
      authorEmail: '',
      website: ''
    }
  });

  async function onSubmit(data: CreatePostInput) {
    try {
      const response = await fetch('/api/posts', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
      });

      if (!response.ok) {
        // Handle validation errors from backend
        if (response.status === 400) {
          const problem = await response.json();
          handleProblemDetails(problem);
          return;
        }

        throw new Error('Failed to create post');
      }

      const result = await response.json();
      // Handle success
    } catch (error) {
      console.error('Error creating post:', error);
    }
  }

  function handleProblemDetails(problem: any) {
    // Map backend errors to form fields
    if (problem.errors) {
      Object.entries(problem.errors).forEach(([field, messages]) => {
        form.setError(field as keyof CreatePostInput, {
          type: 'server',
          message: (messages as string[])[0]  // Take first error message
        });
      });
    }
  }

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <div>
        <label htmlFor="title">Title</label>
        <input
          id="title"
          {...form.register('title')}
          aria-invalid={form.formState.errors.title ? 'true' : 'false'}
        />
        {form.formState.errors.title && (
          <p className="error">{form.formState.errors.title.message}</p>
        )}
      </div>

      <div>
        <label htmlFor="slug">Slug</label>
        <input
          id="slug"
          {...form.register('slug')}
          aria-invalid={form.formState.errors.slug ? 'true' : 'false'}
        />
        {form.formState.errors.slug && (
          <p className="error">{form.formState.errors.slug.message}</p>
        )}
      </div>

      <button type="submit" disabled={form.formState.isSubmitting}>
        {form.formState.isSubmitting ? 'Creating...' : 'Create Post'}
      </button>
    </form>
  );
}
```

## File Upload Validation

```typescript
// lib/validations/avatar.ts
import { z } from 'zod';

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB
const ALLOWED_MIME_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

export const uploadAvatarSchema = z.object({
  avatar: z
    .instanceof(File)
    .refine((file) => file.size > 0, 'Avatar is required')
    .refine((file) => file.size <= MAX_FILE_SIZE, 'File size must not exceed 5 MB')
    .refine(
      (file) => ALLOWED_MIME_TYPES.includes(file.type),
      'File must be JPEG, PNG, or WebP'
    )
});

export type UploadAvatarInput = z.infer<typeof uploadAvatarSchema>;
```

```typescript
// components/UploadAvatarForm.tsx
'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { uploadAvatarSchema, type UploadAvatarInput } from '@/lib/validations/avatar';

export function UploadAvatarForm() {
  const form = useForm<UploadAvatarInput>({
    resolver: zodResolver(uploadAvatarSchema)
  });

  async function onSubmit(data: UploadAvatarInput) {
    const formData = new FormData();
    formData.append('avatar', data.avatar);

    const response = await fetch('/api/users/avatar', {
      method: 'POST',
      body: formData  // Don't set Content-Type, browser sets it with boundary
    });

    if (!response.ok) {
      if (response.status === 400) {
        const problem = await response.json();
        handleProblemDetails(problem);
        return;
      }
      throw new Error('Failed to upload avatar');
    }

    // Handle success
  }

  function handleProblemDetails(problem: any) {
    if (problem.errors?.avatar) {
      form.setError('avatar', {
        type: 'server',
        message: problem.errors.avatar[0]
      });
    }
  }

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <input
        type="file"
        accept="image/jpeg,image/png,image/webp"
        {...form.register('avatar')}
      />
      {form.formState.errors.avatar && (
        <p className="error">{form.formState.errors.avatar.message}</p>
      )}
      <button type="submit">Upload</button>
    </form>
  );
}
```

## Password Validation

```typescript
// lib/validations/auth.ts
import { z } from 'zod';

export const registerSchema = z.object({
  email: z
    .string()
    .email('Invalid email format'),

  password: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
    .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
    .regex(/[0-9]/, 'Password must contain at least one number'),

  passwordConfirmation: z.string()
}).refine((data) => data.password === data.passwordConfirmation, {
  message: 'Passwords do not match',
  path: ['passwordConfirmation']  // Error shows on passwordConfirmation field
});

export type RegisterInput = z.infer<typeof registerSchema>;
```

## Error Code Mapping for i18n

```typescript
// lib/i18n/error-messages.ts
export const ERROR_MESSAGES: Record<string, string> = {
  // Blog posts
  'blog:post:title:required': 'Заголовок обязателен',
  'blog:post:title:too_long': 'Заголовок должен быть не более 200 символов',
  'blog:post:slug:required': 'Slug обязателен',
  'blog:post:slug:invalid_format': 'Slug должен содержать только строчные буквы, цифры и дефисы',
  'blog:post:slug:already_exists': 'Пост с таким slug уже существует',

  // Users
  'users:email:required': 'Email обязателен',
  'users:email:invalid_format': 'Неверный формат email',
  'users:password:required': 'Пароль обязателен',
  'users:password:too_short': 'Пароль должен быть не менее 8 символов',
  'users:password:missing_uppercase': 'Пароль должен содержать хотя бы одну заглавную букву',
  'users:password_confirmation:mismatch': 'Пароли не совпадают',
  'users:avatar:file_too_large': 'Размер файла не должен превышать 5 МБ',
  'users:avatar:invalid_mime_type': 'Файл должен быть JPEG, PNG или WebP'
};

export function getErrorMessage(errorCode: string, fallback: string): string {
  return ERROR_MESSAGES[errorCode] || fallback;
}
```

```typescript
// Updated handleProblemDetails
function handleProblemDetails(problem: any) {
  if (problem.errors) {
    Object.entries(problem.errors).forEach(([field, messages]) => {
      const errorCodes = problem.errorCodes?.[field] || [];
      const errorCode = errorCodes[0];
      const message = (messages as string[])[0];

      form.setError(field as keyof CreatePostInput, {
        type: 'server',
        message: errorCode ? getErrorMessage(errorCode, message) : message
      });
    });
  }
}
```

## Frontend Tests

```typescript
// __tests__/CreatePostForm.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CreatePostForm } from '@/components/CreatePostForm';

describe('CreatePostForm', () => {
  it('shows client-side validation errors', async () => {
    const user = userEvent.setup();
    render(<CreatePostForm />);

    // Submit without filling form
    const submitButton = screen.getByRole('button', { name: /create/i });
    await user.click(submitButton);

    // Zod validation errors should show immediately
    await waitFor(() => {
      expect(screen.getByText('Title is required')).toBeInTheDocument();
      expect(screen.getByText('Slug is required')).toBeInTheDocument();
    });
  });

  it('shows server-side validation errors', async () => {
    const user = userEvent.setup();

    // Mock fetch to return ProblemDetails
    global.fetch = jest.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({
        type: 'https://tools.ietf.org/html/rfc7807',
        title: 'One or more validation errors occurred',
        status: 400,
        errors: {
          slug: ['A post with this slug already exists']
        },
        errorCodes: {
          slug: ['blog:post:slug:already_exists']
        }
      })
    });

    render(<CreatePostForm />);

    // Fill form
    await user.type(screen.getByLabelText(/title/i), 'Test Post');
    await user.type(screen.getByLabelText(/slug/i), 'existing-slug');
    await user.type(screen.getByLabelText(/content/i), 'Test content');

    // Submit
    await user.click(screen.getByRole('button', { name: /create/i }));

    // Server error should show
    await waitFor(() => {
      expect(screen.getByText('A post with this slug already exists')).toBeInTheDocument();
    });
  });
});
```
