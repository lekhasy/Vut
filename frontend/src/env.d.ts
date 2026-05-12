/// <reference path="../.astro/types.d.ts" />

declare namespace App {
  interface Locals {
    userId: string;
    providerId: string;
    displayName: string;
    avatarUrl: string;
    isEmailVerified: boolean;
  }
}
