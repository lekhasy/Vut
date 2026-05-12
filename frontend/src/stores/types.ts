export interface User {
  userId: string;
  providerId: string;
  displayName: string;
  avatarUrl: string;
  email?: string;
  isEmailVerified: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface Organization {
  orgId: string;
  name: string;
  role: 'Owner' | 'Member';
  isDeleted: boolean;
}

export interface Member {
  userId: string;
  displayName: string;
  avatarUrl: string;
  role: 'Owner' | 'Member';
  joinedAt: string;
}

export interface Invitation {
  orgId: string;
  orgName: string;
  email: string;
  role: 'Owner' | 'Member';
  status: 'Pending' | 'Accepted' | 'Declined';
  invitedAt: string;
}
