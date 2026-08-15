export interface LoginRequest {
  emailOrUsername: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  username: string;
  password: string;
  phoneNumber?: string | null;
}

export interface UserResponse {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  username: string;
  phoneNumber: string | null;
  roleName: string;
  organizationId: string | null;
  isActive: boolean;
  isEmailVerified: boolean;
  isFirstLogin: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface VerifyEmailRequest {
  code: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
  confirmPassword: string;
}

/** The access/refresh tokens never appear here — the backend writes them
 * straight to httpOnly cookies. This mirrors the backend's actual
 * `LoginResponse { user }` shape. */
export interface LoginResponse {
  user: UserResponse;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  username: string;
  phoneNumber?: string | null;
}

/** The `{ code, message }` business-failure shape from the Result/Error
 * pattern, distinct from ASP.NET Core's ValidationProblem shape below. */
export interface ApiError {
  code: string;
  message: string;
}

/** ASP.NET Core's standard ValidationProblem shape (FluentValidation errors). */
export interface ValidationProblem {
  title: string;
  status: number;
  errors: Record<string, string[]>;
}
